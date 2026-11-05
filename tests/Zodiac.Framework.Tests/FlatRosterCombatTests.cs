using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class FlatRosterCombatTests
{
    [Fact]
    public void Eleven_literal_source_descriptions_match_roster_paths_and_actual_sword_results()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "roster-combat-contract-v1.json")));
        int n = 0;
        foreach (var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected"); bool route = row.GetProperty("kind").GetString() == "route";
            TilePosition Point(JsonElement p) => new(p[0].GetInt32(), p[1].GetInt32(), 0);
            var specs = d.GetProperty("units").EnumerateArray().Select(u => new Spec(u.GetProperty("id").GetString()!, Point(u.GetProperty("tile")),
                u.GetProperty("team").GetInt32(), u.GetProperty("hp").GetInt32())).ToArray();
            var s = Make(specs, d.GetProperty("width").GetInt32(), d.GetProperty("depth").GetInt32(),
                move: route ? d.GetProperty("move").GetInt32() : 4, jump: route ? d.GetProperty("jump").GetInt32() : 0,
                moved: route && d.GetProperty("moved").GetBoolean(), seed: route ? 57 : d.GetProperty("seed").GetUInt32(),
                count: route ? 0 : d.GetProperty("count").GetUInt64(), pa: route ? 5 : d.GetProperty("pa").GetInt32(), evade: route ? 0 : d.GetProperty("evade").GetInt32());
            var before = Snapshot(s.State);
            if (route)
            {
                var range = FlatRosterMovement.Evaluate(s.State, A, s.Profile); Assert.True(range.Accepted);
                Assert.Equal(e.GetProperty("physical").EnumerateArray().Select(Point), range.Outcome!.PhysicalDestinations);
                Assert.Equal(e.GetProperty("origins").EnumerateArray().Select(Point), range.Outcome.AiOrigins);
                var path = FlatRosterMovement.Evaluate(s.State, A, s.Profile, Point(d.GetProperty("destination")));
                if (e.GetProperty("path").ValueKind == JsonValueKind.Null) Assert.False(path.Accepted);
                else
                {
                    Assert.True(path.Accepted); Assert.Equal(e.GetProperty("path").EnumerateArray().Select(Point), path.Outcome!.Path!.Tiles);
                    Assert.Equal(e.GetProperty("packed").EnumerateArray().Select(b => b.GetByte()), path.Outcome.Path.PackedSteps);
                    Assert.Equal(Enum.Parse<BattleFacing>(e.GetProperty("heading").GetString()!), path.Outcome.Path.FinalHeading);
                }
                Assert.Equal(s.State.TurnBattle.Turns.RngState, path.RngState);
            }
            else
            {
                var result = AttackBattleSimulator.AttackRoster(s.State, new(A, B), new(Sword, d.GetProperty("power").GetInt32()), s.Profile);
                Assert.True(result.Accepted); Assert.Equal(new AttackResolvedEvent(A, B, e.GetProperty("hit").GetBoolean(), e.GetProperty("critical").GetBoolean(), e.GetProperty("damage").GetInt32()), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
                Assert.Equal(e.GetProperty("hp").GetInt32(), result.State.Combatants.Single(u => u.UnitId == B).Hp);
                Assert.Equal(Point(e.GetProperty("target")), Position(result.State, B));
                Assert.Equal(new RandomState(e.GetProperty("rngWord").GetUInt32(), e.GetProperty("drawCount").GetUInt64()), result.State.TurnBattle.Turns.RngState);
                Assert.Equal(e.GetProperty("moved").GetBoolean() ? 1 : 0, result.Events.OfType<SwordKnockbackEvent>().Count());
                Assert.Same(s.State.Combatants.Single(u => u.UnitId == C), result.State.Combatants.Single(u => u.UnitId == C));
                Assert.Equal(Position(s.State, C), Position(result.State, C));
            }
            Assert.Equal(before, Snapshot(s.State)); n++;
        }
        Assert.Equal(11, n);
    }

    [Fact]
    public void Owned_profile_preserves_storage_and_separates_catalog_capacity_from_source_extents()
    {
        var rows = Enumerable.Range(0, 32).Select(i => new FlatRosterUnit(new($"unit-{i}"), i % 4, i)).ToArray();
        var p = new FlatRosterBattleProfile(Ground, rows); rows[0] = new(new("replacement"), 0, 32);
        Assert.Equal(32, p.Units.Count); Assert.Equal("unit-0", p.Members[0].UnitId.Value);
        foreach (int count in new[] { 17, 18, 21, 22 })
        {
            var specs = Enumerable.Range(0, count).Select(i => new Spec(i == 0 ? "a" : i == 1 ? "b" : $"unit-{i}", new(i % 16, i / 16, 0), i == 0 ? 0 : 1)).ToArray();
            var setup = Make(specs, width: 16, depth: 16);
            Assert.Equal(count <= 17, FlatRosterMovement.Evaluate(setup.State, A, setup.Profile).Accepted);
            if (count <= 21) setup.Profile.ValidateState(setup.State);
            else Assert.Throws<NotSupportedException>(() => setup.Profile.ValidateState(setup.State));
        }
    }

    [Fact]
    public void Invalid_identity_team_order_and_unrepresented_source_facts_reject_before_work()
    {
        foreach (var rows in new FlatRosterUnit[][] { [], [new(A, 4, 0)], [new(A, 0, -1)], [new(A, 0, 0), new(B, 1, 0)], [new(A, 0, 0), new(A, 1, 1)] })
            Assert.Throws<ArgumentException>(() => new FlatRosterBattleProfile(Ground, rows));
        var s = Make(); var wrong = new FlatRosterBattleProfile(Ground, s.Profile.Members.Select(u => u with { SourceOrder = u.SourceOrder + 1 }));
        Assert.Throws<NotSupportedException>(() => wrong.ValidateState(s.State));
        Assert.False(FlatRosterMovement.Evaluate(s.State, A, wrong).Accepted);
        Assert.False(FlatRosterMovement.Evaluate(Make(move: 256).State, A, s.Profile).Accepted);
        Assert.False(FlatRosterMovement.Evaluate(Make(jump: 256).State, A, s.Profile).Accepted);
        Assert.False(FlatRosterMovement.Evaluate(Make([new("a", new(1,1,0)), new("b", new(2,1,0),1), new("c",new(3,1,0),Hp:0)]).State,A,s.Profile).Accepted);
    }

    [Fact]
    public void Ally_transit_retains_residuals_but_not_endpoints_and_focus_rows_ignore_all_occupants()
    {
        var s = Make([new("a", new(0,0,0)), new("c", new(1,0,0)), new("b", new(4,0,0),1)], width:5,depth:1,move:3);
        var result = FlatRosterMovement.Evaluate(s.State,A,s.Profile,focusId:B); Assert.True(result.Accepted);
        Assert.Equal(new FlatMovementResidual(new(1,0,0),3), result.Outcome!.RemainingRanges.Single(r=>r.Tile==new TilePosition(1,0,0)));
        Assert.Equal(new byte[]{4,3,2,1,0},Assert.Single(result.Outcome.TargetDistanceRows).Distances);
        Assert.DoesNotContain(new TilePosition(1,0,0),result.Outcome.AiOrigins);
        Assert.Empty(FlatRosterMovement.Evaluate(s.State,A,s.Profile).Outcome!.TargetDistanceRows);
        Assert.Equal(FlatMovementRejection.UnknownFocus,FlatRosterMovement.Evaluate(s.State,A,s.Profile,focusId:new("absent")).Rejection);
    }

    [Fact]
    public void Existing_pair_queries_use_the_same_kernel_and_retain_their_three_unit_guard()
    {
        foreach (int move in new[]{0,2,4,255})
        foreach (bool moved in new[]{false,true})
        {
            var s=Make([new("a",new(0,1,0)),new("b",new(1,1,0),1)],width:3,depth:3,move:move,jump:7,moved:moved);
            var old=FlatOrdinaryMovement.Evaluate(s.State,A,new(Ground,A,B));
            var current=FlatRosterMovement.Evaluate(s.State,A,s.Profile,focusId:B);
            Assert.Equal(JsonSerializer.Serialize(old),JsonSerializer.Serialize(current));
        }
        Assert.False(FlatOrdinaryMovement.Evaluate(Make().State,A,new(Ground,A,B)).Accepted);
    }

    [Fact]
    public void Actual_walk_crosses_ally_atomically_sets_heading_and_preserves_bystanders_and_RNG()
    {
        var s=Make([new("a",new(0,0,0)),new("c",new(1,0,0)),new("b",new(4,0,0),1)],width:5,depth:1,move:3);
        var before=Snapshot(s.State); var result=FlatRosterPathMovement.Move(s.State,new(A,new(3,0,0)),s.Profile);
        Assert.True(result.Accepted); Assert.Equal(new byte[]{0,0,0},result.Path!.PackedSteps);
        Assert.Equal(new TilePosition(3,0,0),Position(result.State,A)); Assert.Equal(BattleFacing.East,result.State.Combatants.Single(u=>u.UnitId==A).Facing);
        Assert.Equal(s.State.TurnBattle.Turns.RngState,result.State.TurnBattle.Turns.RngState);
        Assert.Equal(s.State.TurnBattle.Turns.Ticks,result.State.TurnBattle.Turns.Ticks);
        Assert.True(result.State.TurnBattle.Turns.Units.Single(u=>u.Id==A).MovementTaken);
        Assert.Equal(15,result.State.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
        Assert.Same(s.State.Combatants.Single(u=>u.UnitId==C),result.State.Combatants.Single(u=>u.UnitId==C));
        Assert.IsType<VoluntaryPathCompletedEvent>(result.Events[1]); Assert.Equal(before,Snapshot(s.State));
    }

    [Fact]
    public void Rejected_walk_never_publishes_staged_choices_paths_or_heading()
    {
        foreach (bool moved in new[]{false,true})
        {
            var s=Make(moved:moved); var result=FlatRosterPathMovement.Move(s.State,new(A,Position(s.State,C)),s.Profile);
            Assert.False(result.Accepted); Assert.Same(s.State,result.State); Assert.Null(result.Path); Assert.Empty(result.Events);
        }
        var state=Make(); var inactive=FlatRosterPathMovement.Move(state.State,new(B,new(2,2,0)),state.Profile);
        Assert.False(inactive.Accepted); Assert.Same(state.State,inactive.State); Assert.Equal(TurnCycleRejection.InactiveUnit,inactive.TurnRejection);
    }

    [Fact]
    public void Friendly_sword_target_is_legal_and_only_selected_unit_changes()
    {
        var s=Make([new("a",new(1,1,0)),new("b",new(2,1,0)),new("c",new(4,3,0),1)]);
        var result=AttackBattleSimulator.AttackRoster(s.State,new(A,B),new(Sword,5),s.Profile);
        Assert.True(result.Accepted); Assert.Equal(5,result.State.Combatants.Single(u=>u.UnitId==B).Hp);
        Assert.Equal(new TilePosition(3,1,0),Position(result.State,B));
        Assert.Equal(s.State.TurnBattle.Turns.Units.Single(u=>u.Id==C),result.State.TurnBattle.Turns.Units.Single(u=>u.Id==C));
    }

    [Fact]
    public void Sword_exact_and_late_draw_budgets_keep_all_effects_and_choices_atomic()
    {
        foreach (var e in new[]{(Seed:57u,Evade:0,Draws:7),(Seed:1u,Evade:0,Draws:6),(Seed:1u,Evade:100,Draws:4)})
        {
            var exact=Make(seed:e.Seed,evade:e.Evade,count:ulong.MaxValue-(ulong)e.Draws);
            var accepted=AttackBattleSimulator.AttackRoster(exact.State,new(A,B),new(Sword,5),exact.Profile);
            Assert.True(accepted.Accepted); Assert.Equal(ulong.MaxValue,accepted.State.TurnBattle.Turns.RngState.DrawCount);
            var shortState=Make(seed:e.Seed,evade:e.Evade,count:ulong.MaxValue-(ulong)e.Draws+1);
            var failed=AttackBattleSimulator.AttackRoster(shortState.State,new(A,B),new(Sword,5),shortState.Profile);
            Assert.False(failed.Accepted); Assert.Same(shortState.State,failed.State); Assert.Empty(failed.Events);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted,failed.AttackRejection);
        }
    }

    [Fact]
    public void Pair_sword_resolution_is_identical_and_old_profile_still_rejects_wider_rosters()
    {
        foreach (uint seed in new[]{1u,57u})
        {
            var s=Make([new("a",new(1,1,0)),new("b",new(2,1,0),1)],seed:seed);
            var old=AttackBattleSimulator.Attack(s.State,new(A,B),new(Sword,5),new FlatSwordBattleProfile(Ground));
            var current=AttackBattleSimulator.AttackRoster(s.State,new(A,B),new(Sword,5),s.Profile);
            Assert.True(current.Accepted); Assert.Equal(Snapshot(old.State),Snapshot(current.State)); Assert.Equal(SerializeEvents(old.Events),SerializeEvents(current.Events));
        }
        var wider=Make(); Assert.False(AttackBattleSimulator.Attack(wider.State,new(A,B),new(Sword,5),new FlatSwordBattleProfile(Ground)).Accepted);
    }
}
