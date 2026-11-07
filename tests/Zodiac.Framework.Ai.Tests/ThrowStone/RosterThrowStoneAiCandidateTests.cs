using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterThrowStoneAiCandidateTests
{
    private static readonly AssetId Stone = new("test:stone");
    private static readonly RosterThrowStoneAiCandidateProfile Profile = new(true,true,true,true);
    private static readonly TilePosition Origin = new(1,1,0), Target = new(2,1,0), Ally = new(1,2,0);
    private static TargetedAbilityBattleProfile Ability(Setup s,bool grant=true,TerrainSurfaceId? ground=null) => new(ground ?? Ground,
        [new(Stone)],s.Simulation.Units.Select(u=>new TargetedAbilityUnitGrant(u.UnitId,u.UnitId==s.Request.ActorId && grant ? [Stone] : [],0)));
    private static Setup Sample(int hp=50,ulong ticks=20) => Make([new("a",0,1,15,Move:2),new("b",1,0,95,Hp:hp,Facing:BattleFacing.West),new("c",2,1,Hp:31)],
        positions:[Origin,Target,Ally],ticks:ticks);
    private static RosterThrowStoneAiCandidateResult Query(Setup s,ThrowStoneAiCandidate? candidate=null,RandomState? rng=null,
        TargetedAbilityBattleProfile? ability=null,RosterThrowStoneAiCandidateProfile? profile=null) =>
        RosterThrowStoneAiCandidate.Evaluate(s.State,candidate ?? new(A,Stone,B,Origin,true),ability ?? Ability(s),s.Simulation,profile ?? Profile,rng ?? Rng);

    [Fact]
    public void Fourteen_source_tapes_cover_midpoint_range_diagonal_evade_peers_orders_ko_and_clock()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-stone-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("candidates").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");string actor=d.GetProperty("actor").GetString()!,target=d.GetProperty("target").GetString()!;
            var raw=d.GetProperty("units").EnumerateArray().ToArray();var rng=new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());
            var units=raw.Select(u=>new Unit(u.GetProperty("id").GetString()!,u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),u.GetProperty("ct").GetInt32(),
                u.GetProperty("hp").GetInt32(),u.GetProperty("maxHp").GetInt32(),u.GetProperty("speed").GetInt32(),
                Move:u.GetProperty("id").GetString()==actor ? d.GetProperty("move").GetInt32() : 1,
                Moved:u.GetProperty("id").GetString()==actor && d.GetProperty("moved").GetBoolean(),
                Facing:Enum.Parse<BattleFacing>(u.GetProperty("face").GetString()!),Pa:u.GetProperty("pa").GetInt32())).ToArray();
            var s=Make(units,actor,positions:raw.Select(u=>Tile(u.GetProperty("tile"))).ToArray(),ticks:d.GetProperty("ticks").GetUInt64(),rng:rng);
            var evades=raw.ToDictionary(u=>u.GetProperty("id").GetString()!,u=>u.GetProperty("evade").GetInt32());
            s=s with{State=new(s.State.TurnBattle,s.State.Combatants.Select(u=>new AttackUnitState(u.UnitId,u.Hp,u.MaxHp,0,0,u.PhysicalAttack,evades[u.UnitId.Value],0,0,0,
                u.Brave,u.ZodiacSign,u.Facing,u.Status,u.KoCount,u.RightHandWeaponId)))};
            var before=Snapshot(s.State);var r=Query(s,new(new(actor),Stone,new(target),Tile(d.GetProperty("destination")),d.GetProperty("order").GetString()=="ActThenMove"),rng);
            Assert.Equal(e.GetProperty("disposition").GetString(),r.Disposition.ToString());Assert.Equal(RosterThrowStoneAiCandidateRejection.None,r.Rejection);
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
        Assert.Equal(14,count);
    }

    [Fact]
    public void Pair_projection_and_score_remain_equal_and_public_command_keeps_two_unit_guard()
    {
        foreach(int hp in new[]{5,50})
        foreach(bool first in new[]{false,true})
        {
            var s=Make([new("a",0,1,15,Move:2),new("b",1,0,95,Hp:hp,Facing:BattleFacing.West)],positions:[Origin,Target]);
            var candidate=new ThrowStoneAiCandidate(A,Stone,B,new(2,0,0),first);var ability=Ability(s);
            var old=ThrowStoneAiCandidateScoring.Evaluate(s.State,candidate,new(ability,new(Ground,A,B),s.Simulation.Units),
                new([new(A,AiScoreFacts.ClosedProfile),new(B,AiScoreFacts.ClosedProfile)]),Rng);
            var current=Query(s,candidate);Assert.Equal(AiScoringDisposition.Scored,current.Disposition);Assert.Equal(old.Disposition,current.Disposition);
            Assert.Equal(old.RngState,current.RngState);Assert.Equal(JsonSerializer.Serialize(old.Projection!.Outcome),JsonSerializer.Serialize(current.Outcome));
            Assert.Equal(JsonSerializer.Serialize(old.Score),JsonSerializer.Serialize(current.Score));
        }
        var roster=Sample();var publicFormula=ThrowStoneAiFormulaProjection.Evaluate(roster.State,new(A,Stone,B),Ability(roster),Rng);
        Assert.False(publicFormula.Accepted);Assert.Equal(AttackCommandRejection.UnsupportedSetup,publicFormula.Rejection);Assert.Equal(Rng,publicFormula.RngState);
        Assert.Equal(AttackCommandRejection.UnsupportedSetup,TargetedAbilityBattleSimulator.ValidateCommand(roster.State,new(A,Stone,B),Ability(roster)));
    }

    [Fact]
    public void Exact_membership_real_grant_numeric_bounds_and_explicit_certificates_are_required()
    {
        var s=Sample();
        Rejected(Query(s,profile:Profile with{ClearDirectTrajectory=false}),RosterThrowStoneAiCandidateRejection.UnsupportedTrajectory);
        Rejected(Query(s,profile:Profile with{NormalScoring=false}),RosterThrowStoneAiCandidateRejection.UnsupportedScoring);
        Rejected(Query(s,ability:Ability(s,false)),RosterThrowStoneAiCandidateRejection.AbilityNotGranted);
        Rejected(Query(s,new(A,new("test:other"),B,Origin,true)),RosterThrowStoneAiCandidateRejection.UnknownAbility);
        Rejected(Query(s,ability:Ability(s,ground:new("test:other"))),RosterThrowStoneAiCandidateRejection.UnsupportedProfile);
        Rejected(Query(s,ability:new(Ground,[new(Stone)],[new(A,[Stone],0),new(B,[],0)])),RosterThrowStoneAiCandidateRejection.UnsupportedProfile);
        foreach(int pa in new[]{0,100})
        {
            var wrong=Make([new("a",0,1,15),new("b",1,0,95),new("c",2,1,Pa:pa)],positions:[Origin,Target,Ally]);
            Rejected(Query(wrong),RosterThrowStoneAiCandidateRejection.UnsupportedProfile);
        }
        var moved=Make([new("a",0,1,15,Moved:true),new("b",1,0,95),new("c",2,1)],positions:[Origin,Target,Ally]);
        Rejected(Query(moved,new(A,Stone,B,new(0,1,0),true)),RosterThrowStoneAiCandidateRejection.InvalidCandidate);
    }

    [Fact]
    public void Targets_endpoints_turn_choices_and_range_are_checked_before_formula_work()
    {
        var s=Sample();
        foreach(var row in new[]{
            (new ThrowStoneAiCandidate(new("absent"),Stone,B,Origin,true),RosterThrowStoneAiCandidateRejection.UnknownActor),
            (new ThrowStoneAiCandidate(A,Stone,new("absent"),Origin,true),RosterThrowStoneAiCandidateRejection.UnknownTarget),
            (new ThrowStoneAiCandidate(A,Stone,A,Origin,true),RosterThrowStoneAiCandidateRejection.InvalidCandidate),
            (new ThrowStoneAiCandidate(A,Stone,C,Origin,true),RosterThrowStoneAiCandidateRejection.TargetNotEnemy),
            (new ThrowStoneAiCandidate(A,Stone,B,Target,true),RosterThrowStoneAiCandidateRejection.InvalidCandidate),
            (new ThrowStoneAiCandidate(A,Stone,B,Ally,true),RosterThrowStoneAiCandidateRejection.InvalidCandidate)})Rejected(Query(s,row.Item1),row.Item2);
        var far=Make([new("a",0,1,15),new("b",1,0,95)],positions:[Origin,new(6,1,0)]);
        Rejected(Query(far),RosterThrowStoneAiCandidateRejection.OutOfRange);
        Assert.Equal(AiScoringDisposition.Scored,Query(far,new(A,Stone,B,new(2,1,0),false)).Disposition);
        var acted=Make([new("a",0,1,15,Acted:true),new("b",1,0,95)],positions:[Origin,Target]);
        Rejected(Query(acted),RosterThrowStoneAiCandidateRejection.UnavailableAction);
        Rejected(Query(Sample(0)),RosterThrowStoneAiCandidateRejection.TerminalInput);
    }

    [Fact]
    public void Formula_and_late_clock_exhaustion_restore_whole_query_rng_and_publish_no_partial_result()
    {
        var s=Sample();var before=Snapshot(s.State);
        foreach(ulong remaining in new ulong[]{0,3,4})
        {
            var rng=new RandomState(1,ulong.MaxValue-remaining);var r=Query(s,rng:rng);
            Rejected(r,RosterThrowStoneAiCandidateRejection.RandomDrawCountExhausted,rng);
        }
        var exact=new RandomState(1,ulong.MaxValue-5);var success=Query(s,rng:exact);
        Assert.Equal(AiScoringDisposition.Scored,success.Disposition);Assert.Equal(ulong.MaxValue,success.RngState.DrawCount);
        Rejected(Query(Sample(ticks:ulong.MaxValue)),RosterThrowStoneAiCandidateRejection.TickCountExhausted);
        Assert.Equal(before,Snapshot(s.State));
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Either_actor_reordered_storage_and_repeated_hypotheses_preserve_live_peers_and_replay()
    {
        Unit[] units=[new("a",4,1,15),new("b",2,0,95,Facing:BattleFacing.West),new("c",0,1,Hp:31)];
        foreach(string actor in new[]{"a","b"})
        {
            var id=new BattleUnitId(actor);var target=id==A ? B : A;var destination=id==A ? Origin : Target;
            var a=Make(units,actor,positions:[Origin,Target,Ally]);var b=Make(units,actor,reverse:true,positions:[Origin,Target,Ally]);
            var candidate=new ThrowStoneAiCandidate(id,Stone,target,destination,true);var before=Snapshot(a.State);
            var x=Query(a,candidate,new(3,7));var y=Query(b,candidate,new(3,7));var repeat=Query(a,candidate,new(3,7));
            Assert.Equal(AiScoringDisposition.Scored,x.Disposition);Assert.Equal(x.RngState,y.RngState);Assert.Equal(JsonSerializer.Serialize(x),JsonSerializer.Serialize(repeat));
            Assert.Equal(JsonSerializer.Serialize(x.Outcome),JsonSerializer.Serialize(y.Outcome));Assert.Equal(JsonSerializer.Serialize(x.Score),JsonSerializer.Serialize(y.Score));
            Assert.Equal(31,x.Outcome!.Units.Single(u=>u.UnitId==C).Hp);Assert.Equal(before,Snapshot(a.State));Assert.Equal(before,Snapshot(b.State));
        }
    }

    [Fact]
    public void Compact_runtime_extent_does_not_cap_profile_storage_or_broaden_legacy_commands()
    {
        foreach(int n in new[]{16,17})
        {
            var units=Enumerable.Range(0,n).Select(i=>new Unit(i==0 ? "a" : i==1 ? "b" : $"unit-{i}",i,i==0 ? 1 : 0,i==0 ? 15 : 95)).ToArray();
            var positions=Enumerable.Range(0,n).Select(i=>new TilePosition(i%8,i/8,0)).ToArray();var s=Make(units,positions:positions);
            var r=Query(s,new(A,Stone,B,positions[0],true));Assert.Equal(n==16,r.Disposition==AiScoringDisposition.Scored);Assert.Equal(n,Ability(s).Units.Count);
        }
    }

    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
    private static void Rejected(RosterThrowStoneAiCandidateResult r,RosterThrowStoneAiCandidateRejection reason,RandomState? rng=null)
    {Assert.Equal(AiScoringDisposition.Rejected,r.Disposition);Assert.Equal(reason,r.Rejection);Assert.Null(r.Outcome);Assert.Null(r.Score);Assert.Equal(rng ?? Rng,r.RngState);}
}
