using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterSwordAiAttackEntryTests
{
    private static readonly RosterAiGeneralProfile General = new(true,true);
    private static readonly BoundedSwordWeapon Sword = new(new("test:roster-attack-sword"),2);
    private static readonly TilePosition Origin = new(1,1,0), Target = new(2,1,0), Ally = new(1,2,0);
    private static Setup Sample(int move=1,int targetCt=95,bool acted=false,ulong ticks=20) => Bind(Make(
        [new("a",0,1,15,Move:move,Acted:acted),new("b",1,0,targetCt,Facing:BattleFacing.West),new("c",2,1)],
        positions:[Origin,Target,Ally],ticks:ticks));
    private static RosterSwordAiAttackResult Query(Setup s,RandomState? rng=null) =>
        RosterSwordAiAttackEntry.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,Movement,Sword,new(true),rng ?? Rng);

    [Fact]
    public void Ten_literal_tapes_match_origin_and_target_order_all_draws_and_best_comparisons()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-attack-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var raw=d.GetProperty("units").EnumerateArray().ToArray();
            var units=raw.Select(u=>new Unit(u.GetProperty("id").GetString()!,u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),u.GetProperty("ct").GetInt32(),
                u.GetProperty("hp").GetInt32(),u.GetProperty("maxHp").GetInt32(),u.GetProperty("speed").GetInt32(),u.GetProperty("move").GetInt32(),
                u.GetProperty("moved").GetBoolean(),u.GetProperty("acted").GetBoolean(),u.GetProperty("support").GetBoolean(),u.GetProperty("conserve").GetBoolean(),
                u.GetProperty("actions").EnumerateArray().Select(a=>a.GetString()!).ToArray(),Enum.Parse<BattleFacing>(u.GetProperty("face").GetString()!),u.GetProperty("pa").GetInt32())).ToArray();
            var rng=new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());
            var s=Bind(Make(units,d.GetProperty("actor").GetString()!,positions:raw.Select(u=>Tile(u.GetProperty("tile"))).ToArray(),ticks:d.GetProperty("ticks").GetUInt64()),
                raw.ToDictionary(u=>new BattleUnitId(u.GetProperty("id").GetString()!),u=>u.GetProperty("evade").GetInt32()));
            var before=Snapshot(s.State);var r=Query(s,rng);Assert.True(r.Accepted,row.GetProperty("id").GetString()+": "+r.Failure);var o=r.Outcome!;
            Assert.Equal(e.GetProperty("skipped").GetBoolean(),o.Skipped);Assert.Equal(e.GetProperty("origins").EnumerateArray().Select(Tile),o.CastOrigins);
            Assert.Equal(e.GetProperty("targets").EnumerateArray().Select(x=>x.GetString()),o.AimTargets.Select(id=>id.Value));
            var tape=e.GetProperty("candidates").EnumerateArray().ToArray();Assert.Equal(tape.Length,o.Candidates.Length);
            for(int i=0;i<tape.Length;i++)
            {
                var x=tape[i];var c=o.Candidates[i];Assert.Equal(Tile(x.GetProperty("origin")),c.Origin);Assert.Equal(x.GetProperty("target").GetString(),c.Candidate.TargetId.Value);
                Assert.Equal(Tile(x.GetProperty("destination")),c.Candidate.Destination);Assert.Equal(x.GetProperty("order").GetString(),c.Candidate.Order.ToString());
                Assert.Equal(x.GetProperty("disposition").GetString(),c.Evaluation.Disposition.ToString());
                Assert.Equal(NullableInt(x,"priority"),c.Evaluation.Score?.Priority);Assert.Equal(NullableInt(x,"hit"),c.Evaluation.Score?.HitPercent);
                Assert.Equal(x.GetProperty("proximity").GetByte(),c.Proximity);RngEquals(x,c.Evaluation.RngState);
            }
            Assert.Equal(NullableInt(e,"best"),o.BestCandidateIndex);
            Assert.Equal(e.GetProperty("comparison").ValueKind==JsonValueKind.Null ? null : e.GetProperty("comparison").GetString(),o.BaselineComparison?.ToString());
            RngEquals(e,r.RngState);Assert.Equal(before,Snapshot(s.State));count++;
        }
        Assert.Equal(10,count);
    }

    [Fact]
    public void Fresh_pair_prefix_and_exhaustive_scan_keep_old_results_and_rng()
    {
        foreach(var team in new[]{SwordAiScenarioTeam.Blue,SwordAiScenarioTeam.NonBlue})
        foreach(var target in new[]{new TilePosition(2,1,0),new TilePosition(4,1,0)})
        {
            var state=BasicSkillAiTestData.Ready(move:2,target:target).Battle;var config=BasicSkillAiListQueryTests.ListProfile([],team:team);
            var request=AccumulateAiTestData.Request();var turn=request.Turn;
            var battle=new FlatRosterBattleProfile(config.Preparation.Attack.Movement.Ground,state.TurnBattle.Turns.Units.Select(u=>
                new FlatRosterUnit(u.Id,u.Id==turn.ActorId ? team==SwordAiScenarioTeam.Blue ? 0 : 1 : team==SwordAiScenarioTeam.Blue ? 1 : 0,u.TieKey)));
            var simulation=new RosterAiSimulationProfile(battle,config.Preparation.Accumulate.Units,true);
            var prefix=BasicSkillAiListScenarios.Evaluate(state,request,config,Rng);Assert.True(prefix.Accepted);var m=prefix.Outcome!.Movement;
            var old=SwordAiCandidateScan.Evaluate(state,new(m.Scenario1,m.ActFirstDestination),request.Weapon,config.Preparation.Attack,config.Preparation.Scoring,
                new(turn.ActorId,turn.TargetId,true),new(true,true,true,true,true),prefix.RngState);
            var current=RosterSwordAiAttackEntry.Evaluate(state,new(turn.ActorId,turn.Policies),simulation,Refresh,config.Preparation.AbilityLists!,General,Movement,request.Weapon,new(true),Rng);
            Assert.True(current.Accepted,current.Failure?.ToString());Assert.NotEqual(SwordAiScanDisposition.Rejected,old.Disposition);
            Assert.Equal(old.RngState,current.RngState);Assert.Equal(old.Outcome!.RetainedCandidateIndex,current.Outcome!.BestCandidateIndex);
            Assert.Equal(old.Outcome.Candidates.Length,current.Outcome.Candidates.Length);
            for(int i=0;i<old.Outcome.Candidates.Length;i++)
            {
                var a=old.Outcome.Candidates[i];var b=current.Outcome.Candidates[i];Assert.Equal(a.Origin,b.Origin);Assert.Equal(a.Candidate,b.Candidate);
                Assert.Equal(JsonSerializer.Serialize(a.Evaluation.Score),JsonSerializer.Serialize(b.Evaluation.Score));Assert.Equal(a.Evaluation.RngState,b.Evaluation.RngState);
                Assert.Equal(JsonSerializer.Serialize(a.Evaluation.Projection!.Outcome),JsonSerializer.Serialize(b.Evaluation.Outcome));
            }
        }
    }

    [Fact]
    public void Unknown_priority_for_unentered_stone_does_not_gate_exhaustive_attack()
    {
        var s=Bind(Make([new("a",0,1,15,Move:1,Actions:["Stone"]),new("b",1,0,95),new("c",2,1)],positions:[Origin,new(5,1,0),Ally]));
        var allEntries=RosterAiCastOrigins.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,Movement,new(true),Rng);
        Assert.False(allEntries.Accepted);Assert.Equal(RosterAiCastOriginRejection.UnknownPriority,allEntries.Failure!.Reason);
        var attack=Query(s);Assert.True(attack.Accepted);Assert.Empty(attack.Outcome!.Candidates);
        Assert.Null(attack.Outcome.Scenarios.Cells.Single(c=>c.Tile==Origin).WrittenPriority);Assert.Equal(new RandomState(662824084,10),attack.RngState);
    }

    [Fact]
    public void Post_act_skips_weapon_invocation_but_entered_empty_scans_validate_bindings()
    {
        var acted=Sample(acted:true);
        var skipped=RosterSwordAiAttackEntry.Evaluate(acted.State,acted.Request,acted.Simulation,Refresh,acted.Lists,General,Movement,null,new(true),Rng);
        Assert.True(skipped.Accepted);Assert.True(skipped.Outcome!.Skipped);Assert.Empty(skipped.Outcome.Candidates);Assert.Equal(new RandomState(3295386429,11),skipped.RngState);
        var empty=Bind(Make([new("a",0,1,15,Move:1),new("b",1,1),new("c",2,1)],positions:[Origin,Target,Ally]));
        var invalid=RosterSwordAiAttackEntry.Evaluate(empty.State,empty.Request,empty.Simulation,Refresh,empty.Lists,General,Movement,null,new(true),Rng);
        Failed(invalid,RosterSwordAiAttackStage.Admission,Rng);Assert.Equal(RosterSwordAiCandidateRejection.UnsupportedProfile,invalid.Failure!.CandidateReason);
        var context=RosterSwordAiAttackEntry.Evaluate(empty.State,empty.Request,empty.Simulation,Refresh,empty.Lists,General,Movement,Sword,new(),Rng);
        Failed(context,RosterSwordAiAttackStage.Admission,Rng);
    }

    [Fact]
    public void Whole_query_budget_failures_discard_prefix_and_earlier_successful_candidates()
    {
        var s=Sample();var exact=new RandomState(1,ulong.MaxValue-9);var ok=Query(s,exact);Assert.True(ok.Accepted);Assert.Equal(ulong.MaxValue,ok.RngState.DrawCount);
        var shortClock=new RandomState(1,ulong.MaxValue-8);var failure=Query(s,shortClock);Failed(failure,RosterSwordAiAttackStage.Candidate,shortClock);
        Assert.Equal(0,failure.Failure!.CandidateIndex);Assert.Equal(RosterSwordAiCandidateRejection.RandomDrawCountExhausted,failure.Failure.CandidateReason);
        var multiple=Bind(Make([new("a",3,1,15,Move:0),new("b",0,0,95),new("c",2,2,95),new("d",4,1)],positions:[Origin,Target,new(1,0,0),new(0,1,0)]));
        var late=new RandomState(1,ulong.MaxValue-11);var last=Query(multiple,late);Failed(last,RosterSwordAiAttackStage.Candidate,late);Assert.Equal(1,last.Failure!.CandidateIndex);
        var noPrefix=new RandomState(1,ulong.MaxValue);Failed(Query(s,noPrefix),RosterSwordAiAttackStage.Prefix,noPrefix);
    }

    [Fact]
    public void Late_candidate_tick_failure_rolls_back_an_entire_completed_prefix()
    {
        var s=Bind(Make([new("a",0,1,15,Move:1),new("b",1,0,95,Hp:5),new("c",2,1)],positions:[Origin,Target,Ally],ticks:ulong.MaxValue-2));
        var r=Query(s);Failed(r,RosterSwordAiAttackStage.Candidate,Rng);Assert.Equal(RosterSwordAiCandidateRejection.TickCountExhausted,r.Failure!.CandidateReason);
        Assert.Equal(0,r.Failure.CandidateIndex);
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_reversed_collection_order_replay_the_same_complete_scan()
    {
        Unit[] units=[new("a",4,1,15,Move:2),new("b",2,0,95,Move:2,Facing:BattleFacing.West),new("c",0,1)];
        foreach(string actor in new[]{"a","b"})
        {
            var a=Bind(Make(units,actor,positions:[Origin,Target,Ally]));var b=Bind(Make(units,actor,reverse:true,positions:[Origin,Target,Ally]));var before=Snapshot(a.State);
            var x=Query(a);var y=Query(b);Assert.True(x.Accepted,x.Failure?.ToString());Assert.True(y.Accepted,y.Failure?.ToString());
            Assert.Equal(x.RngState,y.RngState);Assert.Equal(JsonSerializer.Serialize(x.Outcome!.Candidates),JsonSerializer.Serialize(y.Outcome!.Candidates));
            Assert.Equal(x.Outcome.BestCandidateIndex,y.Outcome.BestCandidateIndex);Assert.Equal(before,Snapshot(a.State));Assert.Equal(before,Snapshot(b.State));
        }
    }

    [Fact]
    public void Non_imminent_enemy_remains_an_active_instant_attack_target()
    {
        var s=Sample(targetCt:0);var r=Query(s);Assert.True(r.Accepted);Assert.Empty(r.Outcome!.Scenarios.Preparation.Proximity.TargetableEnemies);
        Assert.Equal(B,Assert.Single(r.Outcome.AimTargets));var candidate=Assert.Single(r.Outcome.Candidates);
        Assert.Equal(AiScoringDisposition.Scored,candidate.Evaluation.Disposition);Assert.Equal(A,candidate.Evaluation.Outcome!.MoveOpportunityUnitId);
        Assert.Equal(Origin,candidate.Candidate.Destination);Assert.Equal((ulong)13,candidate.Evaluation.Outcome.ElapsedTicks);
        Assert.Equal(65,candidate.Evaluation.Outcome.Units.Single(u=>u.UnitId==B).Ct);
        Assert.Equal(0,s.State.TurnBattle.Turns.Units.Single(u=>u.Id==B).Ct);
    }

    private static Setup Bind(Setup s,IReadOnlyDictionary<BattleUnitId,int>? evade=null) => s with{State=new(s.State.TurnBattle,
        s.State.Combatants.Select(u=>new AttackUnitState(u.UnitId,u.Hp,u.MaxHp,0,0,u.PhysicalAttack,evade?.GetValueOrDefault(u.UnitId) ?? 0,0,0,0,
            u.Brave,u.ZodiacSign,u.Facing,u.Status,u.KoCount,u.UnitId==s.Request.ActorId ? Sword.Id : u.RightHandWeaponId)))};
    private static void Failed(RosterSwordAiAttackResult r,RosterSwordAiAttackStage stage,RandomState rng)
    {Assert.False(r.Accepted);Assert.Null(r.Outcome);Assert.Equal(stage,r.Failure!.Stage);Assert.Equal(rng,r.RngState);}
    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
    private static int? NullableInt(JsonElement e,string key)=>e.GetProperty(key).ValueKind==JsonValueKind.Null ? null : e.GetProperty(key).GetInt32();
    private static void RngEquals(JsonElement e,RandomState actual)=>Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),actual);
}
