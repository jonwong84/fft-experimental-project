using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterAiActionTests
{
    private static readonly RosterAiGeneralProfile General = new(true,true);
    private static readonly AssetId Self = new("test:self");
    private static readonly TilePosition[] Positions = [new(1,1,0),new(3,1,0),new(1,2,0)];
    private static Setup Sample(bool stone=false,bool outside=false,bool acted=false, int pa=5, ulong ticks=20) =>
        Make([new("a",0,1,15,Move:1,Acted:acted,Actions:[stone ? "Stone" : "Self"],Pa:pa),new("b",1,0,95),new("c",2,1)],
            ticks:ticks,positions:[Positions[0],outside ? new(5,1,0) : Positions[1],Positions[2]]);
    private static RosterAccumulateAiProfile Ability(Setup s,bool granted=true) => new(new([new(Self)],
        s.Simulation.Battle.Members.Select(u=>new ImmediateAbilityUnitGrant(u.UnitId,granted ? [Self] : [],0))),true);
    private static RosterAiCastOriginResult Cast(Setup s,AiPriorityWorkspace? workspace=null,RandomState? rng=null) =>
        RosterAiCastOrigins.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,Movement,new(true),rng ?? Rng,workspace);
    private static RosterAccumulateAiEntryResult Entry(Setup s,RandomState? rng=null,int occurrence=1,RosterAccumulateAiProfile? profile=null) =>
        RosterAccumulateAiEntry.Evaluate(s.State,new(s.Request,occurrence),s.Simulation,Refresh,s.Lists,General,Movement,profile ?? Ability(s),rng ?? Rng);

    [Fact]
    public void Nineteen_literal_source_descriptions_match_cast_usefulness_and_fresh_self_entry()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-action-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var kind=row.GetProperty("kind").GetString();
            var units=d.GetProperty("units").EnumerateArray().Select(u=>new Unit(u.GetProperty("id").GetString()!,u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),
                u.GetProperty("ct").GetInt32(),u.GetProperty("hp").GetInt32(),u.GetProperty("maxHp").GetInt32(),u.GetProperty("speed").GetInt32(),u.GetProperty("move").GetInt32(),
                u.GetProperty("moved").GetBoolean(),u.GetProperty("acted").GetBoolean(),u.GetProperty("support").GetBoolean(),u.GetProperty("conserve").GetBoolean(),
                u.GetProperty("actions").EnumerateArray().Select(a=>a.GetString()!).ToArray(),Pa:u.GetProperty("pa").GetInt32())).ToArray();
            var positions=d.GetProperty("units").EnumerateArray().Select(u=>Tile(u.GetProperty("tile"))).ToArray();
            var s=Make(units,d.GetProperty("actor").GetString()!,positions:positions,ticks:d.GetProperty("ticks").GetUInt64());
            var rng=new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());var before=Snapshot(s.State);
            if(kind=="casts")
            {
                var workspace=d.GetProperty("retained").GetArrayLength()>0 ? SeedActor(s) : AiPriorityWorkspace.StartUnknown(s.State.TurnBattle.Battle.Map);
                var r=Cast(s,workspace,rng);var reject=Enum.Parse<RosterAiCastOriginRejection>(e.GetProperty("rejection").GetString()!);
                Assert.Equal(reject==RosterAiCastOriginRejection.None,r.Accepted);
                if(!r.Accepted)
                {
                    Assert.Equal(reject,r.Failure!.Reason);Assert.Equal(e.GetProperty("occurrence").GetInt32(),r.Failure.OccurrenceIndex);
                    Assert.Equal(Tile(e.GetProperty("tile")),r.Failure.MissingTile);Assert.Null(r.Outcome);Assert.Equal(rng,r.RngState);Assert.Same(workspace,r.Workspace);
                }
                else
                {
                    var expected=e.GetProperty("occurrences").EnumerateArray().ToArray();Assert.Equal(expected.Length,r.Outcome!.Occurrences.Length);
                    for(int i=0;i<expected.Length;i++)
                    {
                        var x=expected[i];var o=r.Outcome.Occurrences[i];Assert.Equal(Name(o.Entry.Source.Kind),x.GetProperty("action").GetString());
                        Assert.Equal(x.GetProperty("origins").EnumerateArray().Select(Tile),o.CastOrigins);
                        var keys=x.GetProperty("keys").EnumerateArray().ToArray();Assert.Equal(keys.Length,o.RankedOrigins.Length);
                        for(int j=0;j<keys.Length;j++)
                        {
                            Assert.Equal(Tile(keys[j].GetProperty("tile")),o.RankedOrigins[j].Tile);Assert.Equal(keys[j].GetProperty("priority").GetInt16(),o.RankedOrigins[j].Priority);
                            Assert.Equal(keys[j].GetProperty("proximity").GetByte(),o.RankedOrigins[j].Proximity);
                            Assert.Equal(keys[j].GetProperty("provenance").GetString(),o.RankedOrigins[j].Provenance.ToString());
                        }
                    }
                    RngEquals(e,r.RngState);
                }
            }
            else if(kind=="usefulness")
            {
                var active=d.GetProperty("active").EnumerateArray().Select(x=>new BattleUnitId(x.GetString()!)).ToImmutableArray();
                var r=RosterAccumulateAiUsefulness.Evaluate(s.State,new(s.Request.ActorId,Self,active),s.Simulation,Ability(s),rng);
                Assert.True(r.Accepted,r.Rejection.ToString());Targets(e,r.Outcome!.Targets);Assert.Equal(e.GetProperty("selfUseful").GetBoolean(),r.Outcome.SelfUseful);RngEquals(e,r.RngState);
            }
            else
            {
                var r=Entry(s,rng);Assert.True(r.Accepted,row.GetProperty("id").GetString()+": "+r.Failure);var o=r.Outcome!;
                Assert.Equal(e.GetProperty("skipped").GetBoolean(),o.Skipped);Targets(e,o.Usefulness?.Targets ?? []);
                var expected=e.GetProperty("attempts").EnumerateArray().ToArray();Assert.Equal(expected.Length,o.Candidates.Length);
                for(int i=0;i<expected.Length;i++)
                {
                    var x=expected[i];var a=o.Candidates[i];Assert.Equal(x.GetProperty("order").GetString()=="ActFirst",a.ActBeforeMove);
                    Assert.Equal(x.GetProperty("disposition").GetString(),a.Disposition.ToString());
                    if(a.Disposition==AiScoringDisposition.NotValuable){Assert.Null(a.Projection);Assert.Null(a.Score);continue;}
                    Assert.Equal(x.GetProperty("before").GetInt32(),a.Projection!.PhysicalAttackBefore);Assert.Equal(x.GetProperty("after").GetInt32(),a.Projection.PhysicalAttackAfter);
                    Assert.Equal(x.GetProperty("endCt").GetInt32(),a.Projection.TurnEndCt);Assert.Equal(x.GetProperty("priority").GetInt16(),a.Score!.Priority);
                    Assert.Equal(x.GetProperty("hit").GetByte(),a.Score.HitPercent);Assert.Equal(x.GetProperty("next").GetString(),a.Projection.MoveOpportunityUnitId.Value);
                    Assert.Equal(x.GetProperty("elapsed").GetUInt64(),a.Projection.ElapsedTicks);
                }
                Assert.Equal(e.GetProperty("best").ValueKind==JsonValueKind.Null ? (int?)null : e.GetProperty("best").GetInt32(),o.BestCandidateIndex);
                Assert.Equal(o.BestCandidateIndex is null ? (AiRankOrder?)null : AiRankOrder.Above,o.BaselineComparison);RngEquals(e,r.RngState);
            }
            Assert.Equal(before,Snapshot(s.State));count++;
        }
        Assert.Equal(19,count);
    }

    [Fact]
    public void Ranked_origins_union_every_enemy_team_and_preserve_duplicate_occurrences()
    {
        var s=Make([new("a",0,0,15,Move:3,Actions:["Stone","Stone"]),new("b",1,1,95),new("c",2,0),new("d",3,2,95)],
            positions:[Positions[0],new(5,1,0),Positions[2],new(1,3,0)]);
        var r=Cast(s);Assert.True(r.Accepted,r.Failure?.ToString());var o=r.Outcome!;
        Assert.Equal(new BattleUnitId("d"),o.Scenarios.Preparation.Focus.TargetId);
        var stones=o.Occurrences.Where(x=>x.Entry.Source.Kind==AiHumanAbilityKind.ThrowStone).ToArray();Assert.Equal(2,stones.Length);
        Assert.Contains(new TilePosition(4,1,0),stones[0].CastOrigins); // Five from focused d, one from b.
        Assert.Equal(stones[0].RankedOrigins.ToArray(),stones[1].RankedOrigins.ToArray());Assert.NotEqual(stones[0].Entry.Index,stones[1].Entry.Index);
        Assert.Empty(o.Occurrences[0].RankedOrigins);Assert.All(stones[0].RankedOrigins,k=>Assert.Equal(AiCastPriorityProvenance.Fresh,k.Provenance));
    }

    [Fact]
    public void Retained_priority_uses_current_proximity_and_failure_never_publishes_fresh_writes()
    {
        var s=Sample(stone:true,outside:true);var unknown=AiPriorityWorkspace.StartUnknown(s.State.TurnBattle.Battle.Map);
        var failed=Cast(s,unknown);Assert.False(failed.Accepted);Assert.Same(unknown,failed.Workspace);Assert.Empty(unknown.KnownPriorities);Assert.Equal(Rng,failed.RngState);
        var seeded=SeedActor(s);Assert.Equal(-421,seeded.KnownPriorities[Positions[0]]);
        var success=Cast(s,seeded);Assert.True(success.Accepted);var retained=success.Outcome!.Occurrences[1].RankedOrigins.Single(k=>k.Tile==Positions[0]);
        Assert.Equal((-421,(byte)6,AiCastPriorityProvenance.Retained),(retained.Priority,retained.Proximity,retained.Provenance));
        Assert.Equal(-485,success.Workspace.KnownPriorities[new(2,1,0)]);Assert.Single(seeded.KnownPriorities);
        Assert.False(Cast(s,seeded.Forget()).Accepted);
        Assert.Throws<ArgumentException>(()=>Cast(s,AiPriorityWorkspace.StartUnknown(Sample().State.TurnBattle.Battle.Map)));
        var empty=new RandomState(1,ulong.MaxValue);var exhausted=Cast(s,seeded,empty);
        Assert.False(exhausted.Accepted);Assert.Same(seeded,exhausted.Workspace);Assert.Equal(empty,exhausted.RngState);
    }

    [Fact]
    public void Roster_and_pair_self_filters_and_candidate_scores_share_old_streams_and_results()
    {
        var s=Make([new("a",0,1,15,Move:1,Actions:["Self"],Pa:99),new("b",1,0,95)],positions:[Positions[0],Positions[1]]);
        var ability=Ability(s);var pair=new AccumulateAiCandidateProfile(ability.Ability,new(Ground,A,B),s.Simulation.Units);
        var raw=RosterAccumulateAiUsefulness.Evaluate(s.State,new(A,Self,[A,B]),s.Simulation,ability,Rng);
        var oldFilter=AccumulateAiUsefulness.Evaluate(s.State,new(A,Self,B,Positions[0],true),pair,new(true,true,true),Rng);
        Assert.True(raw.Accepted);Assert.True(oldFilter.Accepted);Assert.Equal(raw.RngState,oldFilter.RngState);
        Assert.Equal(JsonSerializer.Serialize(raw.Outcome),JsonSerializer.Serialize(oldFilter.Outcome));
        var result=Entry(s);Assert.True(result.Accepted);Assert.Equal(2,result.Outcome!.Candidates.Length);
        var prefix=RosterAiGeneralScenarios.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,Movement,Rng);
        var u=RosterAccumulateAiUsefulness.Evaluate(s.State,new(A,Self,[A,B]),s.Simulation,ability,prefix.RngState);var current=u.RngState;
        var scores=new AiScoreProfile([new(A,AiScoreFacts.ClosedProfile),new(B,AiScoreFacts.ClosedProfile)]);
        foreach(var c in result.Outcome.Candidates)
        {
            var old=AccumulateAiCandidateScoring.Evaluate(s.State,new(A,Self,B,result.Outcome.Scenarios.ActFirstDestination,c.ActBeforeMove,true),pair,scores,current);
            Assert.Equal(AiScoringDisposition.Scored,old.Disposition);Assert.Equal(old.RngState,c.RngState);
            Assert.Equal(JsonSerializer.Serialize(old.Score),JsonSerializer.Serialize(c.Score));
            Assert.Equal(JsonSerializer.Serialize(old.Projection!.Outcome!.Units),JsonSerializer.Serialize(c.Projection!.Units));
            Assert.Equal(99,c.Projection.PhysicalAttackAfter);current=old.RngState;
        }
        Assert.Equal(result.RngState,current);
    }

    [Fact]
    public void Usefulness_requires_exact_bindings_grants_numeric_context_and_valid_active_membership()
    {
        var s=Sample();var profile=Ability(s);var request=new RosterAccumulateAiUsefulnessRequest(A,Self,[A,B,C]);
        void Reject(RosterAccumulateAiUsefulnessRequest req,RosterAccumulateAiProfile p,RosterAccumulateAiRejection reason)
        {var r=RosterAccumulateAiUsefulness.Evaluate(s.State,req,s.Simulation,p,Rng);Assert.False(r.Accepted);Assert.Equal(reason,r.Rejection);Assert.Null(r.Outcome);Assert.Equal(Rng,r.RngState);}
        Reject(request,profile with{UsableRetailAlliedMapStatEntry=false},RosterAccumulateAiRejection.UnsupportedContext);
        Reject(request,Ability(s,false),RosterAccumulateAiRejection.AbilityNotGranted);
        Reject(request with{AbilityId=new("test:absent")},profile,RosterAccumulateAiRejection.UnknownAbility);
        foreach(var mask in new[]{default(ImmutableArray<BattleUnitId>),ImmutableArray.Create(A,A),ImmutableArray.Create(new BattleUnitId("missing"))})
            Reject(request with{ActiveUnits=mask},profile,RosterAccumulateAiRejection.InvalidActiveMask);
        Reject(request,new(new([new(Self)],[new(A,[Self],0)]),true),RosterAccumulateAiRejection.IncompleteBindings);
        foreach(int pa in new[]{0,100})
        {var numeric=Sample(pa:pa);Assert.Equal(RosterAccumulateAiRejection.UnsupportedNumericProfile,
            RosterAccumulateAiUsefulness.Evaluate(numeric.State,request,numeric.Simulation,Ability(numeric),Rng).Rejection);}
    }

    [Fact]
    public void Late_ally_coin_exhaustion_rolls_back_the_entire_filter()
    {
        var s=Sample();var shortBudget=new RandomState(1,ulong.MaxValue-1);
        var fail=RosterAccumulateAiUsefulness.Evaluate(s.State,new(A,Self,[A,B,C]),s.Simulation,Ability(s),shortBudget);
        Assert.False(fail.Accepted);Assert.Equal(RosterAccumulateAiRejection.RandomDrawCountExhausted,fail.Rejection);Assert.Equal(shortBudget,fail.RngState);Assert.Null(fail.Outcome);
        var exact=RosterAccumulateAiUsefulness.Evaluate(s.State,new(A,Self,[A,B,C]),s.Simulation,Ability(s),new(1,ulong.MaxValue-2));
        Assert.True(exact.Accepted);Assert.Equal(ulong.MaxValue,exact.RngState.DrawCount);
    }

    [Fact]
    public void Isolated_entry_admission_and_post_act_skip_never_invoke_unentered_effects()
    {
        var s=Sample();foreach(int index in new[]{-1,0,2,100})
        {var invalid=Entry(s,occurrence:index);Assert.False(invalid.Accepted);Assert.Equal(RosterAccumulateAiEntryStage.Admission,invalid.Failure!.Stage);Assert.Equal(Rng,invalid.RngState);}
        var binding=Entry(s,profile:Ability(s,false));Assert.False(binding.Accepted);Assert.Null(binding.Outcome);
        Assert.Equal(RosterAccumulateAiEntryStage.Usefulness,binding.Failure!.Stage);Assert.Equal(Rng,binding.RngState);
        var acted=Sample(acted:true);var skipped=Entry(acted,profile:Ability(acted,false) with{UsableRetailAlliedMapStatEntry=false});
        Assert.True(skipped.Accepted);Assert.True(skipped.Outcome!.Skipped);Assert.Null(skipped.Outcome.Usefulness);Assert.Empty(skipped.Outcome.Candidates);
        Assert.Equal(new RandomState(4182499122,12),skipped.RngState);
    }

    [Fact]
    public void Full_entry_exact_and_late_budgets_count_all_allies_then_each_valuable_order()
    {
        var s=Sample();var exact=new RandomState(3,ulong.MaxValue-9);var ok=Entry(s,exact);Assert.True(ok.Accepted);Assert.Equal(ulong.MaxValue,ok.RngState.DrawCount);
        foreach(var failure in new (int Remaining,RosterAccumulateAiEntryStage Stage,int? Index)[]{(8,RosterAccumulateAiEntryStage.Candidate,1),(7,RosterAccumulateAiEntryStage.Candidate,0),(6,RosterAccumulateAiEntryStage.Usefulness,null)})
        {
            var rng=new RandomState(3,ulong.MaxValue-(ulong)failure.Remaining);var r=Entry(s,rng);Assert.False(r.Accepted);Assert.Null(r.Outcome);Assert.Equal(rng,r.RngState);
            Assert.Equal(failure.Stage,r.Failure!.Stage);Assert.Equal(failure.Index,r.Failure.CandidateIndex);Assert.Equal(RosterAccumulateAiRejection.RandomDrawCountExhausted,r.Failure.Reason);
        }
        var noClocks=Entry(s,new(1,ulong.MaxValue-7));Assert.True(noClocks.Accepted);Assert.Equal(ulong.MaxValue,noClocks.RngState.DrawCount);
        Assert.All(noClocks.Outcome!.Candidates,c=>Assert.Equal(AiScoringDisposition.NotValuable,c.Disposition));
    }

    [Fact]
    public void Late_self_candidate_tick_failure_discards_successful_prefix_and_usefulness()
    {
        var s=Make([new("a",0,1,15,Move:1,Actions:["Self"]),new("b",1,0),new("c",2,1)],positions:Positions,ticks:ulong.MaxValue-14);
        var r=Entry(s,new(3,7));Assert.False(r.Accepted);Assert.Equal(RosterAccumulateAiEntryStage.Candidate,r.Failure!.Stage);
        Assert.Equal(RosterAccumulateAiRejection.TickCountExhausted,r.Failure.Reason);Assert.Equal(0,r.Failure.CandidateIndex);Assert.Equal(new RandomState(3,7),r.RngState);Assert.Null(r.Outcome);
    }

    [Fact]
    public void Repeated_isolated_occurrences_restore_pa_and_do_not_claim_full_list_rng()
    {
        var s=Make([new("a",0,1,15,Move:1,Actions:["Self","Self"],Support:true),new("b",1,0,95),new("c",2,1)],positions:Positions);
        var a=Entry(s,new(3,7),1);var b=Entry(s,new(3,7),2);Assert.True(a.Accepted);Assert.True(b.Accepted);
        Assert.Equal(a.RngState,b.RngState);Assert.NotEqual(a.Outcome!.Entry.Index,b.Outcome!.Entry.Index);
        Assert.All(a.Outcome.Candidates.Concat(b.Outcome.Candidates),c=>Assert.Equal((5,6),(c.Projection!.PhysicalAttackBefore,c.Projection.PhysicalAttackAfter)));
        var first=RosterAccumulateAiUsefulness.Evaluate(s.State,new(A,Self,[A,B,C]),s.Simulation,Ability(s),Rng);
        var second=RosterAccumulateAiUsefulness.Evaluate(s.State,new(A,Self,[A,B,C]),s.Simulation,Ability(s),first.RngState);
        Assert.True(first.Outcome!.SelfUseful);Assert.False(second.Outcome!.SelfUseful);Assert.Equal(new RandomState(3295386429,11),second.RngState);
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_collection_order_replay_without_mutating_live_pa_facing_or_rng()
    {
        Unit[] units=[new("a",3,1,15,Move:1,Actions:["Self"]),new("b",1,0,95),new("c",0,1,Move:1,Actions:["Self"])];
        foreach(string actor in new[]{"a","c"})
        {
            var s=Make(units,actor,positions:Positions);var reversed=Make(units,actor,reverse:true,positions:Positions);var before=Snapshot(s.State);
            var a=Entry(s,new(3,7));var b=Entry(reversed,new(3,7));Assert.True(a.Accepted,a.Failure?.ToString());Assert.True(b.Accepted,b.Failure?.ToString());
            Assert.Equal(a.RngState,b.RngState);Assert.Equal(JsonSerializer.Serialize(a.Outcome!.Candidates),JsonSerializer.Serialize(b.Outcome!.Candidates));
            Assert.Equal(JsonSerializer.Serialize(a.Outcome.Usefulness),JsonSerializer.Serialize(b.Outcome.Usefulness));
            Assert.Equal(before,Snapshot(s.State));Assert.Equal(before,Snapshot(reversed.State));
            Assert.All(a.Outcome.Candidates.Where(c=>c.Projection is not null),c=>Assert.All(c.Projection!.Units,u=>Assert.Equal(BattleFacing.South,u.Facing)));
        }
    }

    [Fact]
    public void Self_score_keeps_every_allied_and_enemy_term_before_stat_credit_comparison()
    {
        var s=Make([new("a",0,1,15,Move:1,Actions:["Self"]),new("b",1,0,95),new("c",2,1),new("d",3,2,95)],
            positions:[Positions[0],Positions[1],Positions[2],new(7,3,0)]);
        var r=Entry(s,new(3,7));Assert.True(r.Accepted);Assert.Equal(2,r.Outcome!.Candidates.Length);
        Assert.All(r.Outcome.Candidates,c=>{
            Assert.Equal(3,c.Score!.Priority);Assert.Equal(new[]{false,true,false,true},c.Score.Contributions.Select(u=>u.Enemy));
            Assert.Equal(new short[]{488,3,488,3},c.Score.Contributions.Select(u=>u.RunningPriority));
        });
        Assert.True(r.Outcome.BeatsMovementBaseline);Assert.Equal(0,r.Outcome.BestCandidateIndex);
    }

    private static AiPriorityWorkspace SeedActor(Setup s)
    {
        var old=s.State.TurnBattle;var actor=s.Request.ActorId;
        var bodies=old.Battle.Units.Select(u=>new BattleUnitState(u.Id,u.Position,u.MovementProfile,u.Id==actor || u.HasMoved));
        var turns=old.Turns.Units.Select(u=>new TurnUnitState(u.Id,u.Speed,u.Ct,u.TieKey,u.HasTurn,u.Id==actor || u.MovementTaken,u.ActionTaken));
        var combat=s.State.Combatants.Select(u=>u.UnitId==C ? new AttackUnitState(u.UnitId,25,u.MaxHp,0,0,u.PhysicalAttack,0,0,0,0,u.Brave,u.ZodiacSign,u.Facing,AttackUnitStatus.None,0,u.RightHandWeaponId) : u);
        var state=new AttackBattleSnapshot(new(new(old.Battle.Map,bodies,old.Battle.RngState),new(turns,old.Turns.RngState,old.Turns.Ticks,old.Turns.Phase)),combat);
        var seed=Cast(s with{State=state});Assert.True(seed.Accepted,seed.Failure?.ToString());return seed.Workspace;
    }
    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
    private static string Name(AiHumanAbilityKind k)=>k==AiHumanAbilityKind.ThrowStone ? "Stone" : k==AiHumanAbilityKind.Accumulate ? "Self" : k==AiHumanAbilityKind.FrogAttack ? "Frog" : "Attack";
    private static void RngEquals(JsonElement e,RandomState actual)=>Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),actual);
    private static void Targets(JsonElement e,ImmutableArray<AccumulateAiUsefulnessTarget> actual)
    {
        var expected=e.GetProperty("targets").EnumerateArray().ToArray();Assert.Equal(expected.Length,actual.Length);
        for(int i=0;i<expected.Length;i++)
        {Assert.Equal(expected[i].GetProperty("id").GetString(),actual[i].UnitId.Value);Assert.Equal(expected[i].GetProperty("effect").GetString(),actual[i].Effect.ToString());
            Assert.Equal(expected[i].GetProperty("raw").ValueKind==JsonValueKind.Null ? (uint?)null : expected[i].GetProperty("raw").GetUInt32(),actual[i].Raw);}
    }
}
