using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterSwordAiCandidateTests
{
    private static readonly BoundedSwordWeapon Sword = new(new("test:roster-sword"),2);
    private static readonly RosterSwordAiScoringProfile Scoring = new(true,true,true);
    private static readonly TilePosition Origin = new(1,1,0), Target = new(2,1,0), Ally = new(1,2,0);
    private static Setup Sample(int hp=50,int evade=0,ulong ticks=20,RandomState? rng=null) => Bind(Make(
        [new("a",0,1,15,Move:2),new("b",1,0,95,Hp:hp,Facing:BattleFacing.West),new("c",2,1,Hp:31)],
        positions:[Origin,Target,Ally],ticks:ticks,rng:rng),A,B,Sword,evade);
    private static RosterSwordAiCandidateResult Query(Setup s,RandomState? rng=null,SwordAiCandidate? candidate=null,BoundedSwordWeapon? weapon=null) =>
        RosterSwordAiCandidate.Evaluate(s.State,candidate ?? new(s.Request.ActorId,s.Request.ActorId==A ? B : A,Origin,SwordAiCandidateOrder.ActThenMove),
            weapon ?? Sword,s.Simulation,Scoring,rng ?? Rng);

    [Fact]
    public void Thirteen_literal_descriptions_cover_formula_orders_peer_hp_status_clock_and_scores()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-sword-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");string actor=d.GetProperty("actor").GetString()!,target=d.GetProperty("target").GetString()!;
            var raw=d.GetProperty("units").EnumerateArray().ToArray();var rng=new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());
            var units=raw.Select(u=>new Unit(u.GetProperty("id").GetString()!,u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),u.GetProperty("ct").GetInt32(),
                u.GetProperty("hp").GetInt32(),u.GetProperty("maxHp").GetInt32(),u.GetProperty("speed").GetInt32(),
                Move:u.GetProperty("id").GetString()==actor ? d.GetProperty("move").GetInt32() : 1,
                Moved:u.GetProperty("id").GetString()==actor && d.GetProperty("moved").GetBoolean(),
                Facing:Enum.Parse<BattleFacing>(u.GetProperty("face").GetString()!),Pa:u.GetProperty("pa").GetInt32())).ToArray();
            var weapon=new BoundedSwordWeapon(Sword.Id,d.GetProperty("power").GetInt32());
            var s=Bind(Make(units,actor,positions:raw.Select(u=>Tile(u.GetProperty("tile"))).ToArray(),ticks:d.GetProperty("ticks").GetUInt64(),rng:rng),
                new(actor),new(target),weapon,raw.Single(u=>u.GetProperty("id").GetString()==target).GetProperty("evade").GetInt32());
            var before=Snapshot(s.State);var r=Query(s,rng,new(new(actor),new(target),Tile(d.GetProperty("destination")),Enum.Parse<SwordAiCandidateOrder>(d.GetProperty("order").GetString()!)),weapon);
            Assert.Equal(e.GetProperty("disposition").GetString(),r.Disposition.ToString());Assert.Equal(RosterSwordAiCandidateRejection.None,r.Rejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),r.RngState);
            if(r.Disposition==AiScoringDisposition.NotValuable){Assert.Null(r.Outcome);Assert.Null(r.Score);}
            else
            {
                var o=r.Outcome!;Assert.Equal(e.GetProperty("hit").GetInt32(),o.HitPercent);Assert.Equal(e.GetProperty("damage").GetInt32(),o.NormalDamage);
                Assert.Equal(e.GetProperty("endCt").GetInt32(),o.TurnEndCt);Assert.Equal(e.GetProperty("next").GetString(),o.MoveOpportunityUnitId.Value);
                Assert.Equal(e.GetProperty("elapsed").GetUInt64(),o.ElapsedTicks);Assert.Equal(e.GetProperty("ticks").GetUInt64(),o.Ticks);
                var expected=e.GetProperty("units").EnumerateArray().ToArray();Assert.Equal(expected.Length,o.Units.Length);
                for(int i=0;i<expected.Length;i++)
                {
                    var u=expected[i];var actual=o.Units[i];Assert.Equal(u.GetProperty("id").GetString(),actual.UnitId.Value);Assert.Equal(u.GetProperty("hp").GetInt32(),actual.Hp);
                    Assert.Equal(u.GetProperty("status").GetString(),actual.Status.ToString());Assert.Equal(u.GetProperty("counter").GetByte(),actual.DeathCounter);Assert.Equal(u.GetProperty("ct").GetInt32(),actual.Ct);
                    Assert.Equal(s.State.Combatants.Single(c=>c.UnitId==actual.UnitId).Facing,actual.Facing);
                }
                Assert.Equal(Tile(d.GetProperty("destination")),o.Units.Single(u=>u.UnitId.Value==actor).Position);
                var groups=o.UpkeepPicks.GroupBy(p=>(p.UnitId,p.Dead)).Select(g=>new{Id=g.Key.UnitId.Value,g.Key.Dead,Count=g.Count()}).ToArray();
                var expectedGroups=e.GetProperty("groups").EnumerateArray().ToArray();Assert.Equal(expectedGroups.Length,groups.Length);
                for(int i=0;i<groups.Length;i++)
                {Assert.Equal(expectedGroups[i].GetProperty("id").GetString(),groups[i].Id);Assert.Equal(expectedGroups[i].GetProperty("dead").GetBoolean(),groups[i].Dead);Assert.Equal(expectedGroups[i].GetProperty("count").GetInt32(),groups[i].Count);}
                Assert.Equal(e.GetProperty("priority").GetInt16(),r.Score!.Priority);
                Assert.Equal(e.GetProperty("running").EnumerateArray().Select(x=>x.GetInt16()),r.Score.Contributions.Select(c=>c.RunningPriority));
            }
            Assert.Equal(before,Snapshot(s.State));count++;
        }
        Assert.Equal(13,count);
    }

    [Fact]
    public void Pair_formula_projection_and_scoring_keep_exact_results_and_two_unit_admission()
    {
        foreach(int hp in new[]{5,50})
        foreach(var order in new[]{SwordAiCandidateOrder.ActThenMove,SwordAiCandidateOrder.MoveThenAct})
        {
            var s=Bind(Make([new("a",0,1,15,Move:2),new("b",1,0,95,Hp:hp,Facing:BattleFacing.West)],positions:[Origin,Target]),A,B,Sword,50);
            var candidate=new SwordAiCandidate(A,B,new(2,0,0),order);
            var profile=new SwordAiCandidateProfile(new(Ground),new(Ground,A,B),s.Simulation.Units);
            var score=new AiScoreProfile([new(A,AiScoreFacts.ClosedProfile),new(B,AiScoreFacts.ClosedProfile)]);
            var old=SwordAiCandidateScoring.Evaluate(s.State,candidate,Sword,profile,score,Rng);var current=Query(s,candidate:candidate);
            Assert.Equal(AiScoringDisposition.Scored,old.Disposition);Assert.Equal(old.Disposition,current.Disposition);Assert.Equal(old.RngState,current.RngState);
            Assert.Equal(JsonSerializer.Serialize(old.Projection!.Outcome),JsonSerializer.Serialize(current.Outcome));Assert.Equal(JsonSerializer.Serialize(old.Score),JsonSerializer.Serialize(current.Score));
        }
        var roster=Sample();var rejected=SwordAiFormulaProjection.Evaluate(roster.State,new(A,B),Sword,new(Ground));
        Assert.False(rejected.Accepted);Assert.Equal(AttackCommandRejection.UnsupportedSetup,rejected.Rejection);
        var formula=SwordAiFormulaProjection.EvaluateRoster(roster.State,new(A,B),Sword,roster.Simulation.Battle);
        Assert.True(formula.Accepted);Assert.Equal(new SwordAiFormulaOutcome(100,10,40),formula.Outcome);Assert.Equal(new RandomState(3295386429,11),formula.RngState);
    }

    [Fact]
    public void Candidate_admission_rejects_wrong_targets_endpoints_choices_context_and_weapon_width()
    {
        var s=Sample();var candidate=new SwordAiCandidate(A,B,Origin,SwordAiCandidateOrder.ActThenMove);
        foreach(var row in new[]{
            (candidate with{ActorId=new("absent")},RosterSwordAiCandidateRejection.UnknownActor),
            (candidate with{TargetId=new("absent")},RosterSwordAiCandidateRejection.UnknownTarget),
            (candidate with{TargetId=A},RosterSwordAiCandidateRejection.InvalidCandidate),
            (candidate with{TargetId=C},RosterSwordAiCandidateRejection.TargetNotEnemy),
            (candidate with{Order=(SwordAiCandidateOrder)7},RosterSwordAiCandidateRejection.InvalidCandidate),
            (candidate with{Destination=Target},RosterSwordAiCandidateRejection.InvalidCandidate),
            (candidate with{Destination=Ally},RosterSwordAiCandidateRejection.InvalidCandidate),
            (candidate with{Destination=new(0,1,0),Order=SwordAiCandidateOrder.MoveThenAct},RosterSwordAiCandidateRejection.OutOfRange)})
            Rejected(Query(s,candidate:row.Item1),row.Item2,Rng);
        Rejected(RosterSwordAiCandidate.Evaluate(s.State,candidate,null,s.Simulation,Scoring,Rng),RosterSwordAiCandidateRejection.UnsupportedProfile,Rng);
        Rejected(Query(s,weapon:new(new("test:wrong"),2)),RosterSwordAiCandidateRejection.UnsupportedProfile,Rng);
        Rejected(RosterSwordAiCandidate.Evaluate(s.State,candidate,Sword,s.Simulation,new(),Rng),RosterSwordAiCandidateRejection.UnsupportedScoring,Rng);
        Rejected(RosterSwordAiCandidate.Evaluate(s.State,candidate,Sword,new(s.Simulation.Battle,s.Simulation.Units),Scoring,Rng),RosterSwordAiCandidateRejection.UnsupportedProfile,Rng);
        var wide=Bind(Make([new("a",0,1,15,Pa:255),new("b",1,0,95)],positions:[Origin,Target]),A,B,new(Sword.Id,65));
        Rejected(Query(wide,weapon:new(Sword.Id,65)),RosterSwordAiCandidateRejection.UnsupportedProfile,Rng);
        var acted=Bind(Make([new("a",0,1,15,Acted:true),new("b",1,0,95)],positions:[Origin,Target]),A,B,Sword);
        Rejected(Query(acted),RosterSwordAiCandidateRejection.UnavailableAction,Rng);
        var corpse=Bind(Make([new("a",0,1,15),new("b",1,0,95),new("c",2,1,Hp:0)],positions:[Origin,Target,Ally]),A,B,Sword);
        Rejected(Query(corpse),RosterSwordAiCandidateRejection.TerminalInput,Rng);
    }

    [Fact]
    public void Zero_accuracy_keeps_four_draws_and_skips_clock_while_late_failures_restore_formula_rng()
    {
        var s=Sample(evade:100,ticks:ulong.MaxValue);var exact=new RandomState(1,ulong.MaxValue-4);var r=Query(s,exact);
        Assert.Equal(AiScoringDisposition.NotValuable,r.Disposition);Assert.Null(r.Outcome);Assert.Null(r.Score);Assert.Equal(ulong.MaxValue,r.RngState.DrawCount);
        var shortBudget=new RandomState(1,ulong.MaxValue-3);Rejected(Query(s,shortBudget),RosterSwordAiCandidateRejection.RandomDrawCountExhausted,shortBudget);
        var positive=Sample();Rejected(Query(positive,exact),RosterSwordAiCandidateRejection.RandomDrawCountExhausted,exact);
        Rejected(Query(Sample(ticks:ulong.MaxValue)),RosterSwordAiCandidateRejection.TickCountExhausted,Rng);
    }

    [Fact]
    public void Corpse_and_living_upkeep_budgets_do_not_increment_live_ko_or_decay_death_counter()
    {
        var s=Sample(hp:5);s=Bind(s,A,B,Sword,ko:255);var before=Snapshot(s.State);var exact=new RandomState(1,ulong.MaxValue-6);
        var ok=Query(s,exact);Assert.Equal(AiScoringDisposition.Scored,ok.Disposition);Assert.Equal(ulong.MaxValue,ok.RngState.DrawCount);
        Assert.Equal(new[]{true,false},ok.Outcome!.UpkeepPicks.Select(p=>p.Dead));Assert.Equal((byte)3,ok.Outcome.Units.Single(u=>u.UnitId==B).DeathCounter);
        var late=new RandomState(1,ulong.MaxValue-5);Rejected(Query(s,late),RosterSwordAiCandidateRejection.RandomDrawCountExhausted,late);
        Assert.Equal(255,s.State.Combatants.Single(u=>u.UnitId==B).KoCount);Assert.Equal(before,Snapshot(s.State));
        var ready=Bind(Make([new("a",0,1,15),new("b",1,0,100,Hp:5),new("c",2,1)],positions:[Origin,Target,Ally],ticks:ulong.MaxValue),A,B,Sword);
        Rejected(Query(ready),RosterSwordAiCandidateRejection.TickCountExhausted,Rng);
        var living=Bind(Make([new("a",0,1,15),new("b",1,0,100,Hp:5),new("c",2,1,100)],positions:[Origin,Target,Ally],ticks:ulong.MaxValue),A,B,Sword);
        var noTicks=Query(living);Assert.Equal(AiScoringDisposition.Scored,noTicks.Disposition);Assert.Equal((ulong)0,noTicks.Outcome!.ElapsedTicks);
        Assert.Equal(new[]{true,false},noTicks.Outcome.UpkeepPicks.Select(p=>p.Dead));Assert.Equal(C,noTicks.Outcome.MoveOpportunityUnitId);
    }

    [Fact]
    public void Compact_runtime_extent_preserves_uncapped_storage_and_capped_raw_movement()
    {
        foreach(int n in new[]{16,17})
        {
            var units=Enumerable.Range(0,n).Select(i=>new Unit(i==0 ? "a" : i==1 ? "b" : $"unit-{i}",i,i==0 ? 1 : 0,i==0 ? 15 : 95)).ToArray();
            var positions=Enumerable.Range(0,n).Select(i=>new TilePosition(i%8,i/8,0)).ToArray();var s=Bind(Make(units,positions:positions),A,B,Sword);
            var r=Query(s,candidate:new(A,B,positions[0],SwordAiCandidateOrder.ActThenMove));Assert.Equal(n==16,r.Disposition==AiScoringDisposition.Scored);Assert.Equal(n,s.Simulation.Units.Length);
        }
        var high=Bind(Make([new("a",0,1,15,Move:125),new("b",1,0,95)],positions:[Origin,Target]),A,B,Sword);
        Assert.Equal(AiScoringDisposition.Scored,Query(high).Disposition);
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_reversed_collections_preserve_peer_state_and_replay()
    {
        Unit[] units=[new("a",4,1,15),new("b",2,0,95,Facing:BattleFacing.West),new("c",0,1,Hp:31)];
        foreach(string actor in new[]{"a","b"})
        {
            var actorId=new BattleUnitId(actor);var target=actorId==A ? B : A;var destination=actorId==A ? Origin : Target;
            var a=Bind(Make(units,actor,positions:[Origin,Target,Ally]),actorId,target,Sword);
            var b=Bind(Make(units,actor,reverse:true,positions:[Origin,Target,Ally]),actorId,target,Sword);var before=Snapshot(a.State);
            var candidate=new SwordAiCandidate(actorId,target,destination,SwordAiCandidateOrder.ActThenMove);var x=Query(a,new(3,7),candidate);var y=Query(b,new(3,7),candidate);
            Assert.Equal(AiScoringDisposition.Scored,x.Disposition);Assert.Equal(x.RngState,y.RngState);Assert.Equal(JsonSerializer.Serialize(x.Outcome),JsonSerializer.Serialize(y.Outcome));
            Assert.Equal(JsonSerializer.Serialize(x.Score),JsonSerializer.Serialize(y.Score));Assert.Equal(31,x.Outcome!.Units.Single(u=>u.UnitId==C).Hp);
            Assert.Equal(actorId==A ? 462 : -410,x.Score!.Priority);
            Assert.Equal(before,Snapshot(a.State));Assert.Equal(before,Snapshot(b.State));
        }
    }

    [Fact]
    public void Midpoint_prediction_never_executes_critical_knockback_or_adopts_live_rng()
    {
        var s=Sample(rng:new(57,7));var before=Snapshot(s.State);var predicted=Query(s,new(57,7));Assert.Equal(AiScoringDisposition.Scored,predicted.Disposition);
        Assert.Equal(10,predicted.Outcome!.NormalDamage);Assert.Equal(Target,predicted.Outcome.Units.Single(u=>u.UnitId==B).Position);
        var actual=AttackBattleSimulator.AttackRoster(s.State,new(A,B),Sword,s.Simulation.Battle);Assert.True(actual.Accepted);
        var hit=Assert.Single(actual.Events.OfType<AttackResolvedEvent>());Assert.True(hit.Critical);Assert.Equal(18,hit.Damage);
        Assert.Single(actual.Events.OfType<SwordKnockbackEvent>());Assert.Equal(new RandomState(3448561432,14),actual.State.TurnBattle.Turns.RngState);
        Assert.Equal(before,Snapshot(s.State));Assert.NotEqual(actual.State.TurnBattle.Turns.RngState,predicted.RngState);
    }

    private static Setup Bind(Setup s,BattleUnitId actor,BattleUnitId target,BoundedSwordWeapon weapon,int evade=0,int ko=0) => s with {State=new(s.State.TurnBattle,
        s.State.Combatants.Select(u=>new AttackUnitState(u.UnitId,u.Hp,u.MaxHp,0,0,u.PhysicalAttack,u.UnitId==target ? evade : u.ClassPhysicalEvade,0,0,0,
            u.Brave,u.ZodiacSign,u.Facing,u.Status,u.UnitId==target ? ko : u.KoCount,u.UnitId==actor ? weapon.Id : u.RightHandWeaponId)))};
    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
    private static void Rejected(RosterSwordAiCandidateResult r,RosterSwordAiCandidateRejection reason,RandomState rng)
    {Assert.Equal(AiScoringDisposition.Rejected,r.Disposition);Assert.Equal(reason,r.Rejection);Assert.Null(r.Outcome);Assert.Null(r.Score);Assert.Equal(rng,r.RngState);}
}
