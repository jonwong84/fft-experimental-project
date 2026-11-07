using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterAiGeneralTests
{
    private static readonly RosterAiGeneralProfile General = new(true, true);
    private static readonly TilePosition[] Positions = [new(1,1,0), new(3,1,0), new(1,2,0)];
    private static Setup Sample(bool outside = false, bool acted = false, bool moved = false, ulong ticks = 20) =>
        Make([new("a",0,1,15,Move:1,Acted:acted,Moved:moved),new("b",1,0,95),new("c",2,1)], ticks:ticks,
            positions:[Positions[0],outside ? new(7,1,0) : Positions[1],Positions[2]]);
    private static RosterAiGeneralScenarioResult Query(Setup s, RandomState? rng = null) =>
        RosterAiGeneralScenarios.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,Movement,rng ?? Rng);

    [Fact]
    public void Eighteen_literal_descriptions_cover_admission_focus_masks_ties_and_rng_order()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-general-contract-v1.json")));
        int count = 0;
        foreach (var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected");
            var units = d.GetProperty("units").EnumerateArray().Select(u => new Unit(u.GetProperty("id").GetString()!,
                u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),u.GetProperty("ct").GetInt32(),
                u.GetProperty("hp").GetInt32(),u.GetProperty("maxHp").GetInt32(),u.GetProperty("speed").GetInt32(),
                u.GetProperty("move").GetInt32(),u.GetProperty("moved").GetBoolean(),u.GetProperty("acted").GetBoolean(),
                u.GetProperty("support").GetBoolean(),u.GetProperty("conserve").GetBoolean(),
                u.GetProperty("actions").EnumerateArray().Select(a=>a.GetString()!).ToArray())).ToArray();
            var positions = d.GetProperty("units").EnumerateArray().Select(u=>Tile(u.GetProperty("tile"))).ToArray();
            var rng = new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());
            var s = Make(units,d.GetProperty("actor").GetString()!,ticks:d.GetProperty("ticks").GetUInt64(),positions:positions);
            var before = Snapshot(s.State);
            var prefix = RosterAiGeneralPreparation.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,rng);
            var rejection = Enum.Parse<RosterAiGeneralRejection>(e.GetProperty("rejection").GetString()!);
            Assert.Equal(rejection == RosterAiGeneralRejection.None,prefix.Accepted);
            if (!prefix.Accepted)
            {
                Assert.Equal(rejection,prefix.Failure!.Reason); Assert.Null(prefix.Outcome); Assert.Equal(rng,prefix.RngState);
                var rejected = Query(s,rng); Assert.False(rejected.Accepted); Assert.Null(rejected.Outcome); Assert.Equal(rng,rejected.RngState);
            }
            else
            {
                var p = prefix.Outcome!;
                Assert.Equal(e.GetProperty("focus").GetString(),p.Focus.TargetId.Value);
                Assert.Equal(e.GetProperty("fallback").GetBoolean(),p.Focus.UsedActorFallback);
                Assert.Equal(e.GetProperty("reach").GetInt32(),p.FocusReach);
                Assert.Equal(Strings(e,"active"),p.ActiveTargets.SelectedUnits.Select(u=>u.Value));
                Assert.Equal(Strings(e,"imminent"),p.Proximity.TargetableEnemies.Select(u=>u.Value));
                Assert.Equal(Tiles(e,"m0"),p.Geometry.AiOrigins);
                Assert.Equal(rng.DrawCount+1,prefix.RngState.DrawCount);
                if (d.GetProperty("kind").GetString()=="scenarios")
                {
                    var r = Query(s,rng); Assert.True(r.Accepted,row.GetProperty("id").GetString()+": "+r.Failure); var o = r.Outcome!;
                    Assert.Equal(Tiles(e,"mask"),o.ScoreMask); Assert.Equal(Tiles(e,"scenario1"),o.Scenario1); Assert.Equal(Tiles(e,"scenario2"),o.Scenario2);
                    Assert.Equal(Tile(e.GetProperty("d0")),o.BaselineDestination); Assert.Equal(Tile(e.GetProperty("d1")),o.ActFirstDestination);
                    Assert.Equal(e.GetProperty("baseline").GetInt32(),o.BaselineIndex); Assert.Equal(e.GetProperty("distance").GetInt32(),o.TargetDistance);
                    Assert.Equal(e.GetProperty("outside").GetBoolean(),o.OutOfReach); Assert.Equal(e.GetProperty("effective").GetInt32(),o.EffectiveReach);
                    Assert.Equal(NullableInt(e,"modulo"),o.ReachAdjustment?.Modulo); Assert.Equal(NullableInt(e,"bound"),o.BaselineDistanceLimit);
                    Assert.Equal(e.GetProperty("filterFallback").GetBoolean(),o.FilterUsedReachableFallback);
                    Assert.Equal(e.GetProperty("destinationFallback").GetBoolean(),o.DestinationUsedDistanceFallback);
                    Assert.All(o.Movement,m=>Assert.Equal(e.GetProperty("priority").GetInt16(),m.Outcome.Score.Priority));
                    Assert.Equal(e.GetProperty("proximity").EnumerateArray().Select(x=>x.GetByte()),o.Cells.Select(c=>c.Proximity));
                    Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),r.RngState);
                    Assert.All(o.Cells,c=>Assert.Equal(o.ScoreMask.Contains(c.Tile),c.PriorityWritten));
                }
            }
            Assert.Equal(before,Snapshot(s.State)); count++;
        }
        Assert.Equal(18,count);
    }

    [Fact]
    public void Existing_pair_list_scenarios_match_fresh_roster_scenarios_without_weakening_pair_admission()
    {
        foreach (var team in new[]{SwordAiScenarioTeam.Blue,SwordAiScenarioTeam.NonBlue})
        foreach (bool outside in new[]{false,true})
        foreach (bool ranged in new[]{false,true})
        {
            var state = BasicSkillAiTestData.Ready(move:1,target:outside ? new(7,7,0) : new(3,1,0)).Battle;
            var config = BasicSkillAiListQueryTests.ListProfile(ranged ? [AiHumanAbilityKind.ThrowStone] : [],team:team);
            var request = AccumulateAiTestData.Request(); var turn = request.Turn;
            var battle = new FlatRosterBattleProfile(config.Preparation.Attack.Movement.Ground,
                state.TurnBattle.Turns.Units.Select(u=>new FlatRosterUnit(u.Id,
                    u.Id==turn.ActorId ? team==SwordAiScenarioTeam.Blue ? 0 : 1 : team==SwordAiScenarioTeam.Blue ? 1 : 0,u.TieKey)));
            var simulation = new RosterAiSimulationProfile(battle,config.Preparation.Accumulate.Units,true);
            var old = BasicSkillAiListScenarios.Evaluate(state,request,config,Rng);
            var current = RosterAiGeneralScenarios.Evaluate(state,new(turn.ActorId,turn.Policies),simulation,Refresh,
                config.Preparation.AbilityLists!,General,Movement,Rng);
            Assert.True(old.Accepted,old.Failure?.ToString()); Assert.True(current.Accepted,current.Failure?.ToString());
            var a=old.Outcome!.Movement; var b=current.Outcome!;
            Assert.Equal(old.RngState,current.RngState); Assert.Equal(a.ScoreMask.ToArray(),b.ScoreMask.ToArray());
            Assert.Equal(a.Scenario1.ToArray(),b.Scenario1.ToArray()); Assert.Equal(a.Scenario2.ToArray(),b.Scenario2.ToArray());
            Assert.Equal(a.BaselineDestination,b.BaselineDestination); Assert.Equal(a.ActFirstDestination,b.ActFirstDestination);
            Assert.Equal(JsonSerializer.Serialize(a.Cells),JsonSerializer.Serialize(b.Cells));
            Assert.Equal(a.Movement.Select(m=>m.Outcome.Score.Priority),b.Movement.Select(m=>m.Outcome.Score.Priority));
        }
        Assert.Throws<ArgumentException>(()=>new AiScoreProfile([new(A,AiScoreFacts.ClosedProfile),new(B,AiScoreFacts.ClosedProfile),new(C,AiScoreFacts.ClosedProfile)]));
    }

    [Fact]
    public void Unknown_control_special_behavior_scoring_or_retained_rows_reject_atomically()
    {
        var s=Sample();
        foreach (var profile in new[]{new RosterAiGeneralProfile(),General with{NoDirectPlayerControl=false},General with{NoSpecialDecisionBehavior=false}})
        {
            var r=RosterAiGeneralScenarios.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,profile,Movement,Rng);
            Rejected(r,RosterAiGeneralRejection.UnsupportedContext,Rng);
        }
        var scoring=RosterAiGeneralScenarios.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,new(),Rng);
        Rejected(scoring,RosterAiGeneralRejection.UnsupportedContext,Rng);
        var rows=RosterAiGeneralScenarios.Evaluate(s.State,s.Request with{Policies=[]},s.Simulation,Refresh,s.Lists,General,Movement,Rng);
        Rejected(rows,RosterAiGeneralRejection.PreparationRejected,Rng); Assert.Equal(RosterAiRejection.InvalidPolicies,rows.Failure!.PreparationReason);
        var high=Make([new("a",0,1,15,Move:125),new("b",1,0,95)]);
        var invalid=Query(high); Rejected(invalid,RosterAiGeneralRejection.PreparationRejected,Rng); Assert.Equal(RosterAiRejection.UnsupportedProfile,invalid.Failure!.PreparationReason);
    }

    [Fact]
    public void Exact_and_late_draw_budgets_include_refresh_all_tiles_then_reach_adjustment()
    {
        var s=Sample(); var exact=new RandomState(1,ulong.MaxValue-5); var ok=Query(s,exact);
        Assert.True(ok.Accepted); Assert.Equal(ulong.MaxValue,ok.RngState.DrawCount);
        var late=new RandomState(1,ulong.MaxValue-4); var fail=Query(s,late);
        Rejected(fail,RosterAiGeneralRejection.RandomDrawCountExhausted,late); Assert.Equal(3,fail.Failure!.MovementIndex);
        var outside=Sample(outside:true); var full=new RandomState(1,ulong.MaxValue-3);
        Assert.Equal(ulong.MaxValue,Query(outside,full).RngState.DrawCount);
        var adjustment=new RandomState(1,ulong.MaxValue-2); var atEnd=Query(outside,adjustment);
        Rejected(atEnd,RosterAiGeneralRejection.RandomDrawCountExhausted,adjustment); Assert.Null(atEnd.Failure!.MovementIndex);
        var noTile=new RandomState(1,ulong.MaxValue-1); var first=Query(outside,noTile);
        Rejected(first,RosterAiGeneralRejection.RandomDrawCountExhausted,noTile); Assert.Equal(0,first.Failure!.MovementIndex);
        var empty=new RandomState(1,ulong.MaxValue); var refresh=Query(s,empty);
        Rejected(refresh,RosterAiGeneralRejection.PreparationRejected,empty); Assert.Equal(RosterAiRejection.RandomDrawCountExhausted,refresh.Failure!.PreparationReason);
    }

    [Fact]
    public void Late_tile_tick_exhaustion_rolls_back_successful_refresh()
    {
        var s=Make([new("a",0,1,15,Move:1),new("b",1,0),new("c",2,1)],ticks:ulong.MaxValue-10,positions:Positions);
        var prepared=RosterAiGeneralPreparation.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,Rng);
        Assert.True(prepared.Accepted); Assert.Equal((ulong)9,prepared.Outcome!.Preparation.Refresh.ElapsedTicks);
        var failed=Query(s); Rejected(failed,RosterAiGeneralRejection.TickCountExhausted,Rng); Assert.Equal(0,failed.Failure!.MovementIndex);
    }

    [Fact]
    public void Unscored_actor_priority_stays_unknown_and_private_scores_include_all_enemy_teams()
    {
        var outside=Query(Sample(outside:true)).Outcome!;
        var origin=new TilePosition(1,1,0); Assert.Contains(origin,outside.Scenario1); Assert.Contains(origin,outside.Scenario2);
        Assert.Null(outside.Cells.Single(c=>c.Tile==origin).WrittenPriority);
        Assert.Equal(1,outside.Preparation.Preparation.Targets.ById[B].TurnDelay);
        var s=Make([new("a",0,1,15,Move:1),new("b",1,0,95),new("c",2,1),new("d",3,2,95)],
            positions:[Positions[0],Positions[1],Positions[2],new(4,1,0)]);
        var r=Query(s); Assert.True(r.Accepted); Assert.Equal(2,r.Outcome!.Preparation.Proximity.TargetableEnemies.Length);
        Assert.All(r.Outcome.Movement,m=>{Assert.Equal(0,m.Outcome.Score.Priority);Assert.Equal(4,m.Outcome.Score.Contributions.Length);});
        var rng=RosterAiGeneralPreparation.Evaluate(s.State,s.Request,s.Simulation,Refresh,s.Lists,General,Rng).RngState;
        foreach(var tile in r.Outcome.Movement)
        {
            var isolated=RosterAiMovementBaseline.Evaluate(s.State,new(A,tile.Outcome.Destination),s.Simulation,Movement,rng);
            Assert.True(isolated.Accepted); Assert.Equal(JsonSerializer.Serialize(isolated.Outcome),JsonSerializer.Serialize(tile.Outcome));
            Assert.Equal(isolated.RngState,tile.RngState); rng=isolated.RngState;
        }
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_reordered_collections_replay_without_adopting_hypothetical_state()
    {
        Unit[] units=[new("a",4,1,15,Move:1),new("b",2,0,95),new("c",1,1,Move:1)];
        foreach(string actor in new[]{"a","c"})
        {
            var s=Make(units,actor,positions:Positions); var reversed=Make(units,actor,reverse:true,positions:Positions);
            string before=Snapshot(s.State); var a=Query(s); var b=Query(reversed); Assert.True(a.Accepted); Assert.True(b.Accepted);
            Assert.Equal(a.RngState,b.RngState);
            Assert.Equal(JsonSerializer.Serialize(a.Outcome!.Movement),JsonSerializer.Serialize(b.Outcome!.Movement));
            Assert.Equal(a.Outcome.BaselineDestination,b.Outcome.BaselineDestination); Assert.Equal(before,Snapshot(s.State));
            Assert.Equal(before,Snapshot(reversed.State));
        }
    }

    [Fact]
    public void Already_moved_and_post_act_queries_preserve_choices_facing_and_unused_refunds()
    {
        var moved=Sample(moved:true); var m=Query(moved); Assert.True(m.Accepted); Assert.Single(m.Outcome!.Movement);
        Assert.Equal(35,m.Outcome.Movement[0].Outcome.TurnEndCt);
        var acted=Sample(acted:true); var a=Query(acted); Assert.True(a.Accepted);
        Assert.All(a.Outcome!.Preparation.Preparation.ActorEntries,e=>Assert.False(e.Usable));
        Assert.All(a.Outcome.Movement,t=>{
            Assert.Equal(t.Outcome.Destination==Positions[0] ? 35 : 15,t.Outcome.TurnEndCt);
            Assert.Equal(BattleFacing.South,t.Outcome.Units.Single(u=>u.UnitId==A).Facing);
        });
        Assert.Null(a.Outcome.BaselineDistanceLimit); Assert.Equal(a.Outcome.BaselineDestination,a.Outcome.ActFirstDestination);
    }

    private static void Rejected(RosterAiGeneralScenarioResult r,RosterAiGeneralRejection reason,RandomState rng)
    { Assert.False(r.Accepted); Assert.Equal(reason,r.Failure!.Reason); Assert.Null(r.Outcome); Assert.Equal(rng,r.RngState); }
    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
    private static IEnumerable<TilePosition> Tiles(JsonElement e,string name)=>e.GetProperty(name).EnumerateArray().Select(Tile);
    private static IEnumerable<string?> Strings(JsonElement e,string name)=>e.GetProperty(name).EnumerateArray().Select(v=>v.GetString());
    private static int? NullableInt(JsonElement e,string name)=>e.GetProperty(name).ValueKind==JsonValueKind.Null ? null : e.GetProperty(name).GetInt32();
}
