using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class RosterAiPreparationTests
{
    [Fact]
    public void Thirteen_authored_source_descriptions_match_clock_flags_team_and_per_unit_scores()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-preparation-contract-v1.json")));
        int count=0;
        foreach (var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var kind=row.GetProperty("kind").GetString();
            var units=d.GetProperty("units").EnumerateArray().Select(u=>new Unit(u.GetProperty("id").GetString()!,u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),
                u.GetProperty("ct").GetInt32(),u.GetProperty("hp").GetInt32(),u.GetProperty("maxHp").GetInt32(),u.GetProperty("speed").GetInt32(),u.GetProperty("move").GetInt32(),
                u.GetProperty("moved").GetBoolean(),u.GetProperty("acted").GetBoolean(),u.GetProperty("support").GetBoolean(),u.GetProperty("conserve").GetBoolean(),
                u.GetProperty("actions").EnumerateArray().Select(a=>a.GetString()!).ToArray())).ToArray();
            var positions=d.GetProperty("units").EnumerateArray().Select(u=>new TilePosition(u.GetProperty("tile")[0].GetInt32(),u.GetProperty("tile")[1].GetInt32(),0)).ToArray();
            var rng=new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());
            var s=Make(units,d.GetProperty("actor").GetString()!,ticks:d.GetProperty("ticks").GetUInt64(),rng:rng,positions:positions);var before=Snapshot(s.State);
            if (kind=="movement")
            {
                var dest=d.GetProperty("destination");var r=RosterAiMovementBaseline.Evaluate(s.State,new(s.Request.ActorId,new(dest[0].GetInt32(),dest[1].GetInt32(),0)),s.Simulation,Movement,rng);
                Assert.True(r.Accepted,r.Rejection.ToString());var o=r.Outcome!;
                Clock(e,o.TurnEndCt,o.MoveOpportunityUnitId,o.ElapsedTicks,o.Ticks,o.Units,r.RngState);
                Assert.Equal(e.GetProperty("priority").GetInt16(),o.Score.Priority);Assert.Equal((byte)100,o.Score.HitPercent);
                Assert.Equal(e.GetProperty("running").EnumerateArray().Select(v=>v.GetInt16()),o.Score.Contributions.Select(c=>c.RunningPriority));
            }
            else
            {
                RosterAiRefreshOutcome o;RandomState end;
                if (kind=="preparation")
                {
                    var r=RosterAiPreparation.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,rng);Assert.True(r.Accepted,r.Rejection.ToString());
                    var p=r.Outcome!;o=p.Refresh;end=r.RngState;
                    foreach (var unit in p.Units)
                    {
                        Assert.Equal(e.GetProperty("share").GetProperty(unit.UnitId.Value).GetInt32(),unit.HelpfulShare);
                        var expected=e.GetProperty("mostly").GetProperty(unit.UnitId.Value);
                        Assert.Equal(expected.ValueKind==JsonValueKind.Null ? (bool?)null : expected.GetBoolean(),unit.MostlySupport);
                    }
                    Assert.Equal(e.GetProperty("harmful").GetInt32(),p.EnemyHarmfulCount);Assert.Equal(p.EnemyHarmfulCount,p.EnemyPhysicalCount);
                    Assert.Equal(e.GetProperty("support").GetBoolean(),p.TeamSupport.Available);Assert.Equal(e.GetProperty("single").GetBoolean(),p.TeamSupport.SingleEnemyRemaining);
                    Assert.Equal(e.GetProperty("policyExit").GetBoolean(),p.TeamSupport.RetainedPolicyExit);
                    var counts=e.GetProperty("counts");
                    if (counts.ValueKind==JsonValueKind.Null) {Assert.Null(p.TeamSupport.AllyCount);Assert.Null(p.TeamSupport.EnemyCount);Assert.Null(p.TeamSupport.ImpairedAllyCount);}
                    else Assert.Equal(new int?[]{counts[0].GetInt32(),counts[1].GetInt32(),counts[2].GetInt32()},new[]{p.TeamSupport.AllyCount,p.TeamSupport.EnemyCount,p.TeamSupport.ImpairedAllyCount});
                    foreach (var policy in p.TeamSupport.Policies)Assert.Equal(e.GetProperty("policies").GetProperty(policy.UnitId.Value).GetBoolean(),policy.TeamSupportUnavailable);
                    string Name(AiHumanAbilityKind k)=>k==AiHumanAbilityKind.Accumulate ? "Self" : k==AiHumanAbilityKind.ThrowStone ? "Stone" : k.ToString();
                    Assert.Equal(e.GetProperty("usable").EnumerateArray().Select(v=>v.GetString()),p.ActorEntries.Where(x=>x.Usable).Select(x=>Name(x.Source.Kind)));
                    Assert.Equal(e.GetProperty("enemyReach").GetInt32(),p.EnemyAbilityReach);Assert.Equal(e.GetProperty("allyReach").GetInt32(),p.AllyAbilityReach);
                }
                else {var r=RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request,s.Simulation,Refresh,rng);Assert.True(r.Accepted,r.Rejection.ToString());o=r.Outcome!;end=r.RngState;}
                Clock(e,o.TurnEndCt,o.MoveOpportunityUnitId,o.ElapsedTicks,o.Ticks,o.Units,end);
                Assert.Equal(e.GetProperty("cachedDelay").GetByte(),o.CachedActorDelay);
                Assert.Equal(Strings(e,"enemy"),o.RefreshedUnits.Where(u=>u.Enemy).Select(u=>u.UnitId.Value));
                Assert.Equal(Strings(e,"belowHalf"),o.RefreshedUnits.Where(u=>u.HpBelowHalf).Select(u=>u.UnitId.Value));
                Assert.Equal(Strings(e,"belowThreeQuarters"),o.RefreshedUnits.Where(u=>u.HpBelowThreeQuarters).Select(u=>u.UnitId.Value));
                Assert.All(o.RefreshedUnits,u=>Assert.True(u.Considered));
            }
            Assert.Equal(before,Snapshot(s.State));count++;
        }
        Assert.Equal(13,count);
    }

    [Fact]
    public void Pair_refresh_and_move_scores_keep_the_old_streams_flags_and_results()
    {
        foreach (var mode in new[]{(Moved:false,Acted:false),(Moved:true,Acted:false),(Moved:false,Acted:true)})
        {
            var s=Make([new("a",0,0,15,Moved:mode.Moved,Acted:mode.Acted),new("b",1,1,95)]);
            var candidate=new SwordAiCandidateProfile(new(Ground),new(Ground,A,B),s.Simulation.Units);
            var score=new AiScoreProfile([new(A,AiScoreFacts.ClosedProfile),new(B,AiScoreFacts.ClosedProfile)]);
            var request=new SwordAiRefreshRequest(A,B,s.Request.Policies);
            var old=mode.Acted ? SwordAiRefresh.EvaluateAfterAct(s.State,request,candidate,score,new(true,true,true),Rng)
                : mode.Moved ? SwordAiRefresh.Evaluate(s.State,request,candidate,score,new(true,true,true),Rng)
                : SwordAiRefresh.EvaluateUnmoved(s.State,request,candidate,score,new(true,true,true),Rng);
            var current=RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request,s.Simulation,Refresh,Rng);Assert.True(current.Accepted);Assert.True(old.Accepted);
            Assert.Equal(old.RngState,current.RngState);Assert.Equal(JsonSerializer.Serialize(old.Outcome!.Units),JsonSerializer.Serialize(current.Outcome!.Units));
            Assert.Equal(JsonSerializer.Serialize(old.Outcome.RefreshedUnits),JsonSerializer.Serialize(current.Outcome.RefreshedUnits));
            var body=s.State.TurnBattle.Battle.Units.Single(u=>u.Id==A);
            var movement=RosterAiMovementBaseline.Evaluate(s.State,new(A,body.Position),s.Simulation,Movement,Rng);Assert.True(movement.Accepted);
            var legacy=mode.Acted ? SwordAiMovementBaseline.EvaluateAfterAct(s.State,new(A,B,body.Position),candidate,score,new(true,true,true,true),Rng)
                : SwordAiMovementBaseline.Evaluate(s.State,new(A,B,body.Position),candidate,score,new(true,true,true,true),Rng);
            Assert.True(legacy.Accepted);Assert.Equal(legacy.RngState,movement.RngState);
            Assert.Equal(JsonSerializer.Serialize(legacy.Outcome!.Score),JsonSerializer.Serialize(movement.Outcome!.Score));
        }
    }

    [Fact]
    public void Context_and_retained_policy_admission_reject_without_partial_refresh()
    {
        var s=Make();
        foreach (var p in new[]{new RosterAiRefreshProfile(),Refresh with{AllUnitsTargetable=false},Refresh with{CleanActorDelayCache=false},Refresh with{ZeroCachedMpAbilityModifiers=false}})
            Rejected(RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request,s.Simulation,p,Rng),RosterAiRejection.UnsupportedContext);
        foreach (var rows in new[]{default(ImmutableArray<AiUnitPolicy>),ImmutableArray.Create(new AiUnitPolicy(A,false,false)),ImmutableArray.Create(new AiUnitPolicy(A,false,false),new(A,false,false),new(C,false,false))})
            Rejected(RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request with{Policies=rows},s.Simulation,Refresh,Rng),RosterAiRejection.InvalidPolicies);
        Rejected(RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request with{ActorId=new("absent")},s.Simulation,Refresh,Rng),RosterAiRejection.UnknownActor);
        Rejected(RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request,new(s.Simulation.Battle,s.Simulation.Units),Refresh,Rng),RosterAiRejection.UnsupportedContext);
    }

    [Fact]
    public void Exact_refresh_budget_ready_peers_and_tick_exhaustion_use_one_clock_call()
    {
        var s=Make();var last=new RandomState(1,ulong.MaxValue-1);
        Assert.Equal(ulong.MaxValue,RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request,s.Simulation,Refresh,last).RngState.DrawCount);
        var empty=new RandomState(1,ulong.MaxValue);var failed=RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request,s.Simulation,Refresh,empty);
        Assert.False(failed.Accepted);Assert.Null(failed.Outcome);Assert.Equal(empty,failed.RngState);
        var max=Make(ticks:ulong.MaxValue);Rejected(RosterAiRefresh.DecisionFlagsRefresh(max.State,max.Request,max.Simulation,Refresh,Rng),RosterAiRejection.TickCountExhausted);
        var ready=Make([new("a",0,0,15),new("b",1,1,150),new("c",2,0,150)],ticks:ulong.MaxValue);
        var r=RosterAiRefresh.DecisionFlagsRefresh(ready.State,ready.Request,ready.Simulation,Refresh,Rng);Assert.True(r.Accepted);
        Assert.Equal((ulong)0,r.Outcome!.ElapsedTicks);Assert.Equal(B,r.Outcome.MoveOpportunityUnitId);
    }

    [Fact]
    public void Compact_runtime_extent_and_owned_baselines_do_not_limit_definition_storage()
    {
        foreach(int n in new[]{16,17})
        {
            var units=Enumerable.Range(0,n).Select(i=>new Unit(i==0 ? "a" : $"unit-{i}",i,i%4,i==0 ? 15 : 0)).ToArray();var s=Make(units);
            var result=RosterAiRefresh.DecisionFlagsRefresh(s.State,s.Request,s.Simulation,Refresh,Rng);Assert.Equal(n==16,result.Accepted);
            Assert.Equal(n,s.Simulation.Units.Length);
        }
        var sample=Make();var baselines=sample.Simulation.Units.ToArray();var profile=new RosterAiSimulationProfile(sample.Simulation.Battle,baselines,true);
        baselines[0]=new(A,0);Assert.Equal((byte)255,profile.Units[0].DeathCounter);
        Assert.Throws<ArgumentException>(()=>new RosterAiSimulationProfile(sample.Simulation.Battle,[new(A,0)]));
        Assert.Throws<ArgumentException>(()=>new AiScoreProfile([new(A,AiScoreFacts.ClosedProfile),new(B,AiScoreFacts.ClosedProfile),new(C,AiScoreFacts.ClosedProfile)]));
    }

    [Fact]
    public void Target_roster_uses_restored_live_delays_and_retained_policy_rows_stay_explicit()
    {
        var s=Make([new("a",0,1,15,Conserve:true),new("b",1,0,95),new("c",2,1,Conserve:true)]);
        var r=RosterAiPreparation.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,Rng);Assert.True(r.Accepted);
        Assert.Equal(17,r.Outcome!.Targets.ById[A].TurnDelay);Assert.Equal(1,r.Outcome.Targets.ById[B].TurnDelay);
        Assert.Equal(0,r.Outcome.Refresh.Units.Single(u=>u.UnitId==B).Ct);
        Assert.True(r.Outcome.TeamSupport.Available);Assert.All(r.Outcome.TeamSupport.Policies.Where(p=>p.UnitId!=B),p=>Assert.True(p.ConserveCt));
        Assert.Equal((byte)9,AiRosterProximity.Evaluate(r.Outcome.Targets,[r.Outcome.Targets.ById[A].Position]).WrittenValues[r.Outcome.Targets.ById[A].Position]);
    }

    [Fact]
    public void Lists_preserve_source_extent_and_reach_guards_without_requiring_executable_grants()
    {
        foreach(int n in new[]{0,32,33})
        {
            var s=Make([new("a",0,0,15,Actions:Enumerable.Repeat("Self",n).ToArray()),new("b",1,1,95),new("c",2,0)]);
            var r=RosterAiPreparation.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,Rng);Assert.Equal(n<=32,r.Accepted);
            if(n<=32)Assert.Equal(n+2,r.Outcome!.ActorEntries.Length);else {Assert.Null(r.Outcome);Assert.Equal(Rng,r.RngState);}
        }
        var high=Make([new("a",0,0,15,Move:125),new("b",1,1,95),new("c",2,0)]);
        Assert.Equal(RosterAiRejection.UnsupportedProfile,RosterAiPreparation.Evaluate(high.State,high.Request,high.Simulation,Refresh,high.Lists,Rng).Rejection);
        Assert.True(RosterAiMovementBaseline.Evaluate(high.State,new(A,high.State.TurnBattle.Battle.Units.Single(u=>u.Id==A).Position),high.Simulation,Movement,Rng).Accepted);
        var other=Make([new("a",0,0,15),new("b",1,1,95),new("different",2,0)]);
        var wrong=RosterAiPreparation.Evaluate(high.State,high.Request,high.Simulation,Refresh,other.Lists,Rng);
        Assert.Equal(RosterAiRejection.UnsupportedList,wrong.Rejection);Assert.Null(wrong.Outcome);Assert.Equal(Rng,wrong.RngState);
    }

    [Fact]
    public void Hypothetical_movement_keeps_facing_and_live_choices_and_scores_every_unit()
    {
        var s=Make([new("a",0,0,15,Acted:true),new("b",1,1,95),new("c",2,0,Hp:10)]);var before=Snapshot(s.State);
        var r=RosterAiMovementBaseline.Evaluate(s.State,new(A,new(0,2,0)),s.Simulation,Movement,Rng);Assert.True(r.Accepted);
        Assert.Equal(BattleFacing.North,FlatRosterMovement.Evaluate(s.State,A,s.Simulation.Battle,new(0,2,0)).Outcome!.Path!.FinalHeading);
        var actor=r.Outcome!.Units.Single(u=>u.UnitId==A);Assert.True(actor.MovementTaken);Assert.True(actor.ActionTaken);Assert.Equal(BattleFacing.South,actor.Facing);
        Assert.Equal(15,r.Outcome.TurnEndCt);Assert.Equal((short)350,r.Outcome.Score.Priority);Assert.All(r.Outcome.Score.Contributions,u=>Assert.Equal(357,u.EquipmentTerm));
        Assert.Equal(before,Snapshot(s.State));
    }

    [Fact]
    public void Movement_context_invalid_destination_and_late_clock_failures_are_atomic()
    {
        var s=Make();var dest=s.State.TurnBattle.Battle.Units.Single(u=>u.Id==A).Position;
        foreach(var p in new[]{new RosterAiMovementProfile(),Movement with{OrdinaryHumansUnchangedEquipment=false},Movement with{ZeroCurrentAndBaselineGolem=false}})
            Assert.False(RosterAiMovementBaseline.Evaluate(s.State,new(A,dest),s.Simulation,p,Rng).Accepted);
        var invalid=RosterAiMovementBaseline.Evaluate(s.State,new(A,s.State.TurnBattle.Battle.Units.Single(u=>u.Id==B).Position),s.Simulation,Movement,Rng);
        Assert.Equal(RosterAiRejection.InvalidDestination,invalid.Rejection);Assert.Null(invalid.Outcome);Assert.Equal(Rng,invalid.RngState);
        var empty=new RandomState(1,ulong.MaxValue);var failed=RosterAiMovementBaseline.Evaluate(s.State,new(A,dest),s.Simulation,Movement,empty);
        Assert.False(failed.Accepted);Assert.Null(failed.Outcome);Assert.Equal(empty,failed.RngState);
        var last=RosterAiMovementBaseline.Evaluate(s.State,new(A,dest),s.Simulation,Movement,new(1,ulong.MaxValue-1));Assert.True(last.Accepted);Assert.Equal(ulong.MaxValue,last.RngState.DrawCount);
    }

    [Fact] [Trait("Contract","QueryIsolation")]
    public void Either_actor_and_reversed_collections_replay_without_changing_live_state()
    {
        foreach(string actor in new[]{"a","b"})
        {
            var s=Make(actor:actor);var reversed=Make(actor:actor,reverse:true);var before=Snapshot(s.State);
            var r=RosterAiPreparation.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,Rng);
            var replay=RosterAiPreparation.Evaluate(reversed.State,reversed.Request,reversed.Simulation,Refresh,reversed.Lists,Rng);
            Assert.True(r.Accepted);Assert.True(replay.Accepted);
            string Comparable(RosterAiPreparationResult result)=>JsonSerializer.Serialize(new {result.RngState,result.Outcome!.Refresh,result.Outcome.Units,
                result.Outcome.ActorEntries,result.Outcome.EnemyHarmfulCount,result.Outcome.EnemyPhysicalCount,result.Outcome.EnemyAbilityReach,
                result.Outcome.AllyAbilityReach,result.Outcome.TeamSupport,Targets=result.Outcome.Targets.Units});
            Assert.Equal(Comparable(r),Comparable(replay));Assert.Equal(before,Snapshot(s.State));
            if(actor=="b")Assert.Equal(2,r.Outcome!.EnemyHarmfulCount);
        }
    }

    private static void Clock(JsonElement e,int end,BattleUnitId next,ulong elapsed,ulong ticks,ImmutableArray<AiProjectedUnit> units,RandomState rng)
    {
        Assert.Equal(e.GetProperty("turnEndCt").GetInt32(),end);Assert.Equal(e.GetProperty("next").GetString(),next.Value);
        Assert.Equal(e.GetProperty("elapsed").GetUInt64(),elapsed);Assert.Equal(e.GetProperty("ticks").GetUInt64(),ticks);
        Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),rng);
        Assert.Equal(e.GetProperty("ct").EnumerateArray().Select(u=>(u.GetProperty("id").GetString(),u.GetProperty("ct").GetInt32())),units.Select(u=>((string?)u.UnitId.Value,u.Ct)));
    }
    private static IEnumerable<string?> Strings(JsonElement e,string key)=>e.GetProperty(key).EnumerateArray().Select(u=>u.GetString());
    private static void Rejected(RosterAiRefreshResult r,RosterAiRejection reason)
    {Assert.False(r.Accepted);Assert.Equal(reason,r.Rejection);Assert.Null(r.Outcome);Assert.Equal(Rng,r.RngState);}
}
