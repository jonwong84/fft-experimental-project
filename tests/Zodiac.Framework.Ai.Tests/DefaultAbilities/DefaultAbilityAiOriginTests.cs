using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiOriginTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard");
    private static DefaultAbilityAiOriginResult Query(DefaultAbilityAiCatalog catalog, DefaultAbilityBattleState state,
        DefaultAbilityAiOriginScenario scenario = DefaultAbilityAiOriginScenario.Main, RandomState? rng = null) =>
        DefaultAbilityAiOriginQuery.Evaluate(state, catalog, new(Mage, 0, scenario), new(true), rng ?? new(57, 0));

    private static DefaultAbilityAiOriginPass Pass(DefaultAbilityAiOriginOutcome outcome, DefaultAbilityAiAimPassKind kind) =>
        outcome.Passes.Single(p => p.Potential.Kind == kind);

    [Fact]
    public void Ranked_origins_use_unit_range_and_tile_range_plus_area_before_per_origin_selectable_intersection()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup();
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var unit = Pass(result.Outcome!, DefaultAbilityAiAimPassKind.UnitsStillAtDeadline);
        Assert.True(unit.RankedOrigins);
        Assert.Equal(new[] { new TilePosition(1, 3, 0), new(0, 2, 0), new(1, 1, 0), new(2, 3, 0),
            new(1, 2, 0), new(2, 1, 0), new(2, 2, 0), new(3, 2, 0) }, unit.Origins.Select(o => o.CastOrigin));
        Assert.All(unit.Origins, o => {
            Assert.Equal(new TilePosition(4, 2, 0), Assert.Single(o.Aims).Position);
            Assert.Equal(Guard, o.Aims[0].UnitId);
            Assert.Equal(AiCastPriorityProvenance.Fresh, o.Priority!.Provenance);
        });
        var tiles = Pass(result.Outcome!, DefaultAbilityAiAimPassKind.Tiles);
        Assert.Equal(8, unit.CastOriginMask.Length);
        Assert.Equal(12, tiles.CastOriginMask.Length);
        Assert.Equal(new[] { new TilePosition(4, 1, 0), new(3, 2, 0), new(5, 2, 0), new(4, 3, 0) },
            tiles.Potential.Aims.Select(a => a.Position));
        Assert.Equal(new TilePosition(1, 4, 0), tiles.Origins[0].CastOrigin);
        Assert.Equal(new[] { new TilePosition(3, 2, 0), new(4, 3, 0) }, tiles.Origins[0].Aims.Select(a => a.Position));
        Assert.DoesNotContain(new TilePosition(4, 2, 0), result.Outcome!.Movement.Geometry.AiOrigins);
        Assert.Equal(15UL, result.RngState.DrawCount);
        Assert.Equal(39090480U, result.RngState.Value);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Actor_origin_keeps_its_own_rank_key_but_substitutes_D1_as_the_movement_destination()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup();
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        var unit = Pass(outcome, DefaultAbilityAiAimPassKind.UnitsStillAtDeadline);
        var origin = Assert.Single(unit.Origins, o => o.ActBeforeMove);
        Assert.Equal(new TilePosition(0, 2, 0), origin.CastOrigin);
        Assert.Equal(new TilePosition(0, 4, 0), origin.MovementDestination);
        Assert.Equal((byte)6, origin.Priority!.Proximity);
        Assert.Equal((byte)4, outcome.Movement.Cells.Single(c => c.Tile == origin.MovementDestination).Proximity);
        Assert.All(unit.Origins.Where(o => !o.ActBeforeMove), o => Assert.Equal(o.CastOrigin, o.MovementDestination));
    }

    [Fact]
    public void Walkable_aims_outside_both_Move_and_current_cast_range_are_available_from_a_moved_origin()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup();
        var fixedAims = DefaultAbilityAiAimQuery.Evaluate(state, catalog, new(Mage, 0)).Outcome!;
        Assert.DoesNotContain(fixedAims.Passes.SelectMany(p => p.Aims), a => a.Position == new TilePosition(5, 2, 0));
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var tiles = Pass(result.Outcome!, DefaultAbilityAiAimPassKind.Tiles);
        Assert.Contains(tiles.Origins, o => o.CastOrigin == new TilePosition(3, 2, 0) &&
            o.Aims.Any(a => a.Position == new TilePosition(5, 2, 0)));
        Assert.DoesNotContain(new TilePosition(5, 2, 0), result.Outcome!.Movement.Geometry.AiOrigins);
    }

    [Fact]
    public void Exhaustive_origins_scan_map_order_without_reading_priorities_and_clear_consumption()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(range: 1, area: 1, targetX: 2);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        Assert.True(result.Outcome!.Potential.Ability.Usage.ExhaustiveOrigins);
        var tiles = Pass(result.Outcome, DefaultAbilityAiAimPassKind.Tiles);
        Assert.False(tiles.RankedOrigins);
        Assert.False(tiles.Potential.ConsumeEvaluatedAims);
        Assert.Equal(new[] { new TilePosition(1, 1, 0), new(2, 1, 0), new(0, 2, 0),
            new(1, 2, 0), new(1, 3, 0), new(2, 3, 0) }, tiles.Origins.Select(o => o.CastOrigin));
        Assert.All(tiles.Origins, o => Assert.Null(o.Priority));
    }

    [Fact]
    public void Potential_origins_do_not_consume_aims_or_add_draws_even_when_the_pass_requests_consumption()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(force: true);
        var movement = DefaultAbilityAiMovementQuery.Evaluate(state, catalog, new(Mage, 0), new(true), new(57, 0));
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(movement.RngState, result.RngState);
        var unit = Pass(result.Outcome!, DefaultAbilityAiAimPassKind.Units);
        Assert.True(unit.Potential.ConsumeEvaluatedAims);
        Assert.Equal(8, unit.Origins.Length);
        Assert.All(unit.Origins, o => Assert.Equal(Guard, Assert.Single(o.Aims).UnitId));
    }

    [Fact]
    public void Out_of_reach_origin_masks_can_avoid_unwritten_cells_while_complement_empty_masks_do_not_read_them()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(targetX: 6);
        var main = Query(catalog, state);
        Assert.True(main.Accepted, main.Rejection);
        var origin = new TilePosition(0, 2, 0);
        Assert.Null(main.Outcome!.Movement.Cells.Single(c => c.Tile == origin).WrittenPriority);
        Assert.All(main.Outcome.Passes, p => Assert.DoesNotContain(origin, p.CastOriginMask));
        var complement = Query(catalog, state, DefaultAbilityAiOriginScenario.Complement);
        Assert.True(complement.Accepted, complement.Rejection);
        Assert.All(complement.Outcome!.Passes, p => Assert.Empty(p.Origins));
        Assert.Equal(main.RngState, complement.RngState);
    }

    [Theory]
    [InlineData(DefaultAbilityAiOriginScenario.Main)]
    [InlineData(DefaultAbilityAiOriginScenario.Complement)]
    public void Unknown_ranked_priority_rejects_after_movement_and_restores_the_whole_query(DefaultAbilityAiOriginScenario scenario)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(targetX: 6, range: 6);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state, scenario);
        Assert.False(result.Accepted);
        Assert.Contains("Unwritten cast-origin priority", result.Rejection);
        Assert.Null(result.Outcome);
        Assert.Equal(new RandomState(57, 0), result.RngState);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Replay_and_input_order_do_not_change_pass_or_origin_order()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(other: (3, 1, SpellTargetTeam.Red));
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var replay = Query(catalog, state.With(units: state.Units.Reverse().ToImmutableArray()));
        Assert.True(replay.Accepted, replay.Rejection);
        Assert.Equal(result.RngState, replay.RngState);
        Assert.Equal(JsonSerializer.Serialize(result.Outcome!.Passes), JsonSerializer.Serialize(replay.Outcome!.Passes));
    }

    [Fact]
    public void No_valuable_enemies_returns_an_empty_pass_list_with_the_same_fresh_movement_rng()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup();
        var d = state.Definition;
        var definition = new DefaultAbilityBattleDefinition(d.Map, d.Units.Select(u => u with {
            Occupant = u.Occupant with { InitialTeam = SpellTargetTeam.Blue, CurrentTeam = SpellTargetTeam.Blue } }),
            d.Spells.Values, d.Jobs, d.JobThresholds, d.InitialRng, d.MovementGround, d.Learning);
        catalog = new(definition, catalog.PrimaryByJob);
        state = DefaultAbilityBattle.Apply(definition.Start(), new DefaultAbilityCommand.Advance()).State;
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        Assert.True(result.Outcome!.Movement.Focus.UsedActorFallback);
        Assert.Empty(result.Outcome.Passes);
        Assert.Empty(result.Outcome.Potential.Passes);
        var baseline = DefaultAbilityAiMovementQuery.Evaluate(state, catalog, new(Mage, 0), new(true), new(57, 0));
        Assert.Equal(baseline.RngState, result.RngState);
    }

    [Fact]
    public void Invalid_scenario_late_rng_failure_and_unsafe_radius_publish_no_partial_outcome()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup();
        Assert.False(Query(catalog, state, (DefaultAbilityAiOriginScenario)0).Accepted);
        var rng = new RandomState(57, ulong.MaxValue - 2);
        var failed = Query(catalog, state, rng: rng);
        Assert.False(failed.Accepted);
        Assert.Null(failed.Outcome);
        Assert.Equal(rng, failed.RngState);
        var unsafeRadius = DefaultAbilityAiAimTests.Setup(range: 7, area: 1);
        failed = Query(unsafeRadius.Catalog, unsafeRadius.State);
        Assert.False(failed.Accepted);
        Assert.Null(failed.Outcome);
        Assert.Equal(new RandomState(57, 0), failed.RngState);
    }

    [Fact]
    public void A_real_256_panel_aim_union_rejects_the_source_counter_alias_after_a_private_movement_scan()
    {
        var (catalog, original) = DefaultAbilityAiAimTests.Setup(charge: 0, range: 1, area: 6, follow: false);
        var d = original.Definition;
        d.Map.TryGetTile(new(0, 0, 0), out var tile);
        var map = new BattleMapDefinition(16, 16, 1, Enumerable.Repeat(tile, 256).ToArray());
        var actor = d.Units[0] with { Move = 124, Occupant = d.Units[0].Occupant with { Position = new(0, 0, 0) } };
        (int X, int Y)[] panels = [(4,0),(10,0),(15,0),(0,5),(5,5),(10,5),(15,5),(0,10),(5,10),(10,10),
            (15,10),(0,15),(5,15),(10,15),(15,15)];
        var peers = panels.Select((p, i) => d.Units[1] with {
            Occupant = d.Units[1].Occupant with { UnitId = new("peer" + i), SourceOrder = i + 1, Position = new(p.X, p.Y, 0) },
            Current = d.Units[1].Current with { UnitId = new("peer" + i) } });
        var definition = new DefaultAbilityBattleDefinition(map, new[] { actor }.Concat(peers), d.Spells.Values,
            d.Jobs, d.JobThresholds, d.InitialRng, d.MovementGround, d.Learning);
        var largeCatalog = new DefaultAbilityAiCatalog(definition, catalog.PrimaryByJob);
        var state = DefaultAbilityBattle.Apply(definition.Start(), new DefaultAbilityCommand.Advance()).State;
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(largeCatalog, state);
        Assert.False(result.Accepted);
        Assert.Contains("unsigned-byte counters", result.Rejection);
        Assert.Null(result.Outcome);
        Assert.Equal(new RandomState(57, 0), result.RngState);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }
}
