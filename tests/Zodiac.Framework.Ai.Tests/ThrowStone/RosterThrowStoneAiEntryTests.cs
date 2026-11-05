using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterThrowStoneAiEntryTests
{
    private static readonly AssetId Stone = new("test:stone");
    private static readonly RosterAiGeneralProfile General = new(true,true);
    private static readonly TilePosition Origin = new(1,1,0);
    private static TargetedAbilityBattleProfile Ability(Setup s,bool grant=true) => new(Ground,[new(Stone)],
        s.Simulation.Units.Select(u=>new TargetedAbilityUnitGrant(u.UnitId,u.UnitId==s.Request.ActorId && grant ? [Stone] : [],0)));
    private static RosterAiProjectileHeights Heights(Setup s) => new(s.Simulation.Units.Select(u=>new RosterAiSpriteHeight(u.UnitId,30)));
    private static Setup Sample(int move=1,bool acted=false,bool moved=false, bool reverse=false) => Make(
        [new("a",0,1,15,Move:move,Acted:acted,Moved:moved,Actions:["Stone"]),new("b",1,0,95,Facing:BattleFacing.West),new("c",2,1)],
        positions:[Origin,new(3,1,0),new(1,2,0)],reverse:reverse);
    private static RosterThrowStoneAiEntryResult Query(Setup s,RandomState? rng=null,AiPriorityWorkspace? workspace=null,int occurrence=1,
        TargetedAbilityBattleProfile? ability=null,RosterAiProjectileHeights? heights=null) => RosterThrowStoneAiEntry.Evaluate(s.State,new(s.Request,occurrence),
            s.Simulation,Refresh,s.Lists,General,Movement,ability ?? Ability(s),heights ?? Heights(s),new(true),rng ?? Rng,workspace);

    [Fact]
    public void Eleven_source_tapes_cover_one_prefix_ranked_order_blocked_retry_consumption_and_restored_peers()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-stone-entry-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var raw=d.GetProperty("units").EnumerateArray().ToArray();string actor=d.GetProperty("actor").GetString()!;
            var units=raw.Select(u=>new Unit(u.GetProperty("id").GetString()!,u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),u.GetProperty("ct").GetInt32(),
                u.GetProperty("hp").GetInt32(),u.GetProperty("maxHp").GetInt32(),u.GetProperty("speed").GetInt32(),u.GetProperty("move").GetInt32(),
                u.GetProperty("moved").GetBoolean(),u.GetProperty("acted").GetBoolean(),u.GetProperty("support").GetBoolean(),u.GetProperty("conserve").GetBoolean(),
                u.GetProperty("actions").EnumerateArray().Select(a=>a.GetString()!).ToArray(),Enum.Parse<BattleFacing>(u.GetProperty("face").GetString()!),u.GetProperty("pa").GetInt32())).ToArray();
            var rng=new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());
            var s=Make(units,actor,positions:raw.Select(u=>Tile(u.GetProperty("tile"))).ToArray(),ticks:d.GetProperty("ticks").GetUInt64(),rng:rng);
            var evades=raw.ToDictionary(u=>u.GetProperty("id").GetString()!,u=>u.GetProperty("evade").GetInt32());
            s=s with{State=new(s.State.TurnBattle,s.State.Combatants.Select(u=>new AttackUnitState(u.UnitId,u.Hp,u.MaxHp,0,0,u.PhysicalAttack,evades[u.UnitId.Value],0,0,0,u.Brave,u.ZodiacSign,u.Facing,u.Status,u.KoCount,u.RightHandWeaponId)))};
            var heights=new RosterAiProjectileHeights(raw.Select(u=>new RosterAiSpriteHeight(new(u.GetProperty("id").GetString()!),u.GetProperty("height").GetByte())));
            var workspace=AiPriorityWorkspace.StartUnknown(s.State.TurnBattle.Battle.Map);var before=Snapshot(s.State);
            var r=Query(s,rng,workspace,d.GetProperty("occurrence").GetInt32(),heights:heights);RngEquals(e,r.RngState);
            if(e.GetProperty("rejection").GetString()=="UnknownPriority")
            {
                Assert.False(r.Accepted);Assert.Equal(RosterThrowStoneAiEntryStage.Priority,r.Failure!.Stage);Assert.Equal(Tile(e.GetProperty("tile")),r.Failure.MissingTile);
                Assert.Null(r.Outcome);Assert.Same(workspace,r.Workspace);
            }
            else
            {
                Assert.True(r.Accepted,row.GetProperty("id").GetString()+": "+r.Failure);var o=r.Outcome!;Assert.Equal(e.GetProperty("skipped").GetBoolean(),o.Skipped);
                Assert.Equal(e.GetProperty("ranked").EnumerateArray().Select(Tile),o.RankedOrigins.Select(k=>k.Tile));
                Assert.Equal(e.GetProperty("aims").EnumerateArray().Select(x=>x.GetString()),o.AimTargets.Select(x=>x.Value));
                Assert.Equal(e.GetProperty("origins").EnumerateArray().Select(Tile),o.EvaluatedOrigins);
                Assert.Equal(e.GetProperty("remainingOrigins").GetInt32(),o.RemainingOriginCount);Assert.Equal(e.GetProperty("remainingAims").GetInt32(),o.RemainingAimCount);
                var visits=e.GetProperty("visits").EnumerateArray().ToArray();Assert.Equal(visits.Length,o.Visits.Length);
                for(int i=0;i<visits.Length;i++)
                {
                    var v=visits[i];var a=o.Visits[i];Assert.Equal(Tile(v.GetProperty("origin")),a.Origin);Assert.Equal(v.GetProperty("target").GetString(),a.TargetId.Value);
                    Assert.Equal(v.GetProperty("clear").GetBoolean(),a.Trajectory.Clear);Assert.Equal(NullableInt(v.GetProperty("candidate")),a.CandidateIndex);
                    Assert.Equal(v.GetProperty("remainingAims").GetInt32(),a.RemainingAimCount);RngEquals(v,a.RngState);
                }
                var candidates=e.GetProperty("candidates").EnumerateArray().ToArray();Assert.Equal(candidates.Length,o.Candidates.Length);
                for(int i=0;i<candidates.Length;i++)
                {
                    var c=candidates[i];var a=o.Candidates[i];Assert.Equal(Tile(c.GetProperty("origin")),a.Origin);Assert.Equal(c.GetProperty("target").GetString(),a.Candidate.TargetId.Value);
                    Assert.Equal(Tile(c.GetProperty("destination")),a.Candidate.Destination);Assert.Equal(c.GetProperty("actFirst").GetBoolean(),a.Candidate.ActBeforeMove);
                    Assert.Equal(c.GetProperty("disposition").GetString(),a.Evaluation.Disposition.ToString());Assert.Equal(NullableInt(c.GetProperty("priority")),a.Evaluation.Score?.Priority);
                    Assert.Equal(NullableInt(c.GetProperty("hit")),a.Evaluation.Score?.HitPercent);Assert.Equal(c.GetProperty("proximity").GetByte(),a.Proximity);RngEquals(c,a.Evaluation.RngState);
                }
                Assert.Equal(NullableInt(e.GetProperty("best")),o.BestCandidateIndex);Assert.Equal(e.GetProperty("comparison").GetString(),o.BaselineComparison?.ToString());
                Assert.Equal(o.Scenarios.Movement.Select(x=>x.Outcome.Destination).OrderBy(x=>x.Y).ThenBy(x=>x.X),r.Workspace.KnownPriorities.Keys.OrderBy(x=>x.Y).ThenBy(x=>x.X));
            }
            Assert.Empty(workspace.KnownPriorities);Assert.Equal(before,Snapshot(s.State));count++;
        }
        Assert.Equal(11,count);
    }

    [Fact]
    public void Selected_occurrence_preserves_duplicates_and_rejects_other_structural_kinds()
    {
        var s=Make([new("a",0,1,15,Move:1,Actions:["Stone","Stone"]),new("b",1,0,95),new("c",2,1)],positions:[Origin,new(3,1,0),new(1,2,0)]);
        var first=Query(s);var second=Query(s,occurrence:2);Assert.True(first.Accepted);Assert.True(second.Accepted);Assert.Equal(first.RngState,second.RngState);
        Assert.Equal(1,first.Outcome!.Entry.Index);Assert.Equal(2,second.Outcome!.Entry.Index);
        Assert.Equal(JsonSerializer.Serialize(first.Outcome.Candidates),JsonSerializer.Serialize(second.Outcome.Candidates));
        foreach(int index in new[]{-1,0,3,4})Rejected(Query(s,occurrence:index),RosterThrowStoneAiEntryStage.Admission,Rng);
    }

    [Fact]
    public void Entered_empty_scans_validate_effect_and_heights_but_post_act_skips_before_invocation()
    {
        var s=Sample();var noGrant=Query(s,ability:Ability(s,false));Rejected(noGrant,RosterThrowStoneAiEntryStage.Admission,Rng);
        Assert.Equal(RosterThrowStoneAiCandidateRejection.AbilityNotGranted,noGrant.Failure!.CandidateReason);
        var noHeights=Query(s,heights:new([]));Rejected(noHeights,RosterThrowStoneAiEntryStage.Admission,Rng);Assert.True(noHeights.Failure!.MissingSpriteHeights);
        var acted=Sample(acted:true);var skipped=Query(acted,ability:Ability(acted,false),heights:new([]));Assert.True(skipped.Accepted);Assert.True(skipped.Outcome!.Skipped);Assert.Empty(skipped.Outcome.Visits);
        var empty=Make([new("a",0,1,15,Move:0,Actions:["Stone"]),new("b",1,1,95)],positions:[Origin,new(5,1,0)]);
        Rejected(Query(empty,ability:Ability(empty,false)),RosterThrowStoneAiEntryStage.Admission,Rng);
        var valid=Query(empty);Assert.True(valid.Accepted);Assert.Empty(valid.Outcome!.Candidates);
    }

    [Fact]
    public void Late_second_candidate_failure_rolls_back_prefix_first_candidate_and_workspace()
    {
        var s=Make([new("a",0,1,15,Move:0,Actions:["Stone"]),new("b",1,0,95),new("c",2,2,95),new("d",3,1,100)],
            positions:[Origin,new(2,1,0),new(1,0,0),new(0,1,0)]);
        var rng=new RandomState(1,ulong.MaxValue-9);var workspace=AiPriorityWorkspace.StartUnknown(s.State.TurnBattle.Battle.Map);var before=Snapshot(s.State);
        var r=Query(s,rng,workspace);Rejected(r,RosterThrowStoneAiEntryStage.Candidate,rng,workspace);Assert.Equal(1,r.Failure!.CandidateIndex);
        Assert.Equal(RosterThrowStoneAiCandidateRejection.RandomDrawCountExhausted,r.Failure.CandidateReason);Assert.Equal(before,Snapshot(s.State));
        var prefixFailure=Query(s,new(1,ulong.MaxValue),workspace);Rejected(prefixFailure,RosterThrowStoneAiEntryStage.Prefix,new(1,ulong.MaxValue),workspace);
    }

    [Fact]
    public void Corpse_clock_tick_failure_discards_successful_tick_free_prefix()
    {
        var s=Make([new("a",0,1,15,Move:0,Actions:["Stone"]),new("b",1,0,100,Hp:5),new("c",2,1)],
            positions:[Origin,new(2,1,0),new(1,2,0)],ticks:ulong.MaxValue);
        var workspace=AiPriorityWorkspace.StartUnknown(s.State.TurnBattle.Battle.Map);var before=Snapshot(s.State);
        var r=Query(s,workspace:workspace);Rejected(r,RosterThrowStoneAiEntryStage.Candidate,Rng,workspace);
        Assert.Equal(RosterThrowStoneAiCandidateRejection.TickCountExhausted,r.Failure!.CandidateReason);Assert.Equal(before,Snapshot(s.State));
    }

    [Fact]
    public void Complete_candidate_ties_keep_first_map_aim_and_compare_against_owned_d0()
    {
        var s=Make([new("a",0,1,15,Move:0,Actions:["Stone"]),new("b",1,0,95),new("c",2,2,95),new("d",3,1,100)],
            positions:[Origin,new(2,1,0),new(1,0,0),new(0,1,0)]);
        var r=Query(s);Assert.True(r.Accepted);var o=r.Outcome!;Assert.Equal(new[]{C,B},o.AimTargets);Assert.Equal(2,o.Candidates.Length);
        Assert.All(o.Candidates,c=>Assert.Equal(26,c.Evaluation.Score!.Priority));Assert.Equal(0,o.BestCandidateIndex);Assert.True(o.BeatsMovementBaseline);
        Assert.Equal(0,o.RemainingAimCount);Assert.Equal(new RandomState(836760821,19),r.RngState);
    }

    [Fact]
    public void Retained_priority_uses_fresh_proximity_and_success_writes_only_owned_movement_mask()
    {
        var s=Make([new("a",0,1,15,Move:1,Actions:["Stone"]),new("b",1,0,95),new("c",2,1)],positions:[Origin,new(5,1,0),new(1,2,0)]);
        var unknown=AiPriorityWorkspace.StartUnknown(s.State.TurnBattle.Battle.Map);Rejected(Query(s,workspace:unknown),RosterThrowStoneAiEntryStage.Priority,Rng,unknown);
        var old=s.State.TurnBattle;var bodies=old.Battle.Units.Select(u=>new BattleUnitState(u.Id,u.Position,u.MovementProfile,u.Id==A));
        var turns=old.Turns.Units.Select(u=>new TurnUnitState(u.Id,u.Speed,u.Ct,u.TieKey,u.HasTurn,u.Id==A,u.ActionTaken));
        var state=new AttackBattleSnapshot(new(new(old.Battle.Map,bodies,old.Battle.RngState),new(turns,old.Turns.RngState,old.Turns.Ticks,old.Turns.Phase)),s.State.Combatants);
        var seed=Query(s with{State=state});Assert.True(seed.Accepted);var workspace=seed.Workspace;
        var result=Query(s,workspace:workspace);Assert.True(result.Accepted,result.Failure?.ToString());var key=Assert.Single(result.Outcome!.RankedOrigins.Where(k=>k.Tile==Origin));
        Assert.Equal(AiCastPriorityProvenance.Retained,key.Provenance);Assert.Equal(workspace.KnownPriorities[Origin],key.Priority);
        Assert.Equal(result.Outcome.Scenarios.Preparation.Proximity.WrittenValues[Origin],key.Proximity);Assert.Single(workspace.KnownPriorities);
        Assert.Equal(2,result.Workspace.KnownPriorities.Count);
        Assert.Throws<ArgumentException>(()=>Query(Sample(),workspace:workspace));
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Reordered_storage_and_replay_preserve_all_live_units_and_deterministic_visits()
    {
        var a=Sample();var b=Sample(reverse:true);var before=Snapshot(a.State);var x=Query(a);var y=Query(b);var repeat=Query(a);
        Assert.True(x.Accepted);Assert.Equal(x.RngState,y.RngState);Assert.Equal(x.RngState,repeat.RngState);
        Assert.Equal(JsonSerializer.Serialize(x.Outcome!.Visits),JsonSerializer.Serialize(y.Outcome!.Visits));
        Assert.Equal(JsonSerializer.Serialize(x.Outcome.Candidates),JsonSerializer.Serialize(repeat.Outcome!.Candidates));
        Assert.Equal(before,Snapshot(a.State));Assert.Equal(before,Snapshot(b.State));
    }

    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
    private static int? NullableInt(JsonElement e)=>e.ValueKind==JsonValueKind.Null ? null : e.GetInt32();
    private static void RngEquals(JsonElement e,RandomState rng)=>Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),rng);
    private static void Rejected(RosterThrowStoneAiEntryResult r,RosterThrowStoneAiEntryStage stage,RandomState rng,AiPriorityWorkspace? workspace=null)
    {Assert.False(r.Accepted);Assert.Equal(stage,r.Failure!.Stage);Assert.Null(r.Outcome);Assert.Equal(rng,r.RngState);if(workspace is not null)Assert.Same(workspace,r.Workspace);}
}
