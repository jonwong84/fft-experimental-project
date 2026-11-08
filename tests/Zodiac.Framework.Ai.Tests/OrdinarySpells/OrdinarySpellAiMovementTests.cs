using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class OrdinarySpellAiMovementTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard");

    private static OrdinarySpellAiMovementResult Query(OrdinarySpellAiCatalog catalog, OrdinaryMagicBattleState state,
        RandomState? rng = null, OrdinarySpellAiMovementProfile? profile = null) =>
        OrdinarySpellAiMovementQuery.Evaluate(state, catalog, new(Mage, 0), profile ?? new(true), rng ?? new(57, 0));

    [Fact]
    public void Fresh_scan_keeps_distinct_D0_D1_and_source_coordinate_ties_with_one_rng()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup();
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(Guard, outcome.Focus.TargetId);
        Assert.Equal(4, outcome.TargetDistance);
        Assert.Equal(4, outcome.TargetReach); // Move3 plus structural bare Attack1, not Fire range4+area1.
        Assert.False(outcome.OutOfReach);
        Assert.Null(outcome.ReachAdjustment);
        Assert.Equal(new TilePosition(2, 3, 0), outcome.BaselineDestination);
        Assert.Equal(new TilePosition(0, 4, 0), outcome.ActFirstDestination);
        Assert.Equal(new(false, 16, 100), outcome.BaselineRank);
        Assert.Equal(14, outcome.Movement.Length);
        Assert.All(outcome.Movement, m => {
            Assert.Equal((short)16, m.Score.Priority);
            Assert.Equal((short)-16, m.TilePriority);
            Assert.Equal(m.Destination, m.Units.Single(u => u.Id == Mage).Occupant.Position);
            Assert.Equal((ushort)75, m.Units.Single(u => u.Id == Mage).Current.Mp);
        });
        Assert.Equal(15UL, result.RngState.DrawCount); // One refresh, then fourteen independent one-event hypotheses.
        Assert.Equal(39090480U, result.RngState.Value); // Independent affine LCG checkpoint from seed57.
        Assert.Equal(1UL, outcome.Movement[0].EntryRng.DrawCount);
        for (int i = 1; i < outcome.Movement.Length; i++)
            Assert.Equal(outcome.Movement[i - 1].RngState, outcome.Movement[i].EntryRng);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
        var replay = Query(catalog, state);
        Assert.Equal(result.RngState, replay.RngState);
        Assert.Equal(outcome.BaselineDestination, replay.Outcome!.BaselineDestination);
        Assert.Equal(outcome.ActFirstDestination, replay.Outcome.ActFirstDestination);
        Assert.Equal(JsonSerializer.Serialize(outcome.Movement), JsonSerializer.Serialize(replay.Outcome.Movement));
        Assert.Equal(outcome.Cells.ToArray(), replay.Outcome.Cells.ToArray());
        Assert.All(outcome.Cells, c => Assert.True(c.PriorityWritten));
    }

    [Fact]
    public void Out_of_reach_actor_reinsertion_preserves_unknown_priority_and_overlapping_masks()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(targetX: 6);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        var origin = state.Units.Single(u => u.Id == Mage).Position;
        Assert.True(outcome.OutOfReach);
        Assert.Equal(6, outcome.TargetDistance);
        Assert.Equal(outcome.BaselineDestination, outcome.ActFirstDestination);
        Assert.DoesNotContain(origin, outcome.ScoreMask);
        Assert.Contains(origin, outcome.Scenario1);
        Assert.Contains(origin, outcome.Scenario2);
        Assert.Null(outcome.Cells.Single(c => c.Tile == origin).WrittenPriority);
        Assert.All(outcome.Cells.Where(c => outcome.ScoreMask.Contains(c.Tile)), c => Assert.True(c.PriorityWritten));
        Assert.Null(outcome.ReachAdjustment); // Blue does not roll the nonBlue one-in-seven reduction.
    }

    [Theory]
    [InlineData(57U, 3448561432U, 19852, 2, 3)]
    [InlineData(0U, 1051550459U, 16045, 4, 2)]
    public void NonBlue_reach_draw_follows_the_complete_movement_scan(uint seed, uint finalSeed, int raw, int reach, int x)
    {
        var (catalog, original) = OrdinarySpellAiAimTests.Setup(targetX: 6);
        var d = original.Definition;
        var definitions = d.Units.Select(u => u with { Occupant = u.Occupant with {
            InitialTeam = u.Current.UnitId == Mage ? SpellTargetTeam.Red : SpellTargetTeam.Blue,
            CurrentTeam = u.Current.UnitId == Mage ? SpellTargetTeam.Red : SpellTargetTeam.Blue } }).ToArray();
        var definition = new OrdinaryMagicBattleDefinition(d.Map, definitions, d.Spells.Values, d.Jobs,
            d.JobThresholds, d.InitialRng, d.MovementGround, d.Learning);
        var swapped = new OrdinarySpellAiCatalog(definition, catalog.PrimaryByJob);
        var state = OrdinaryMagicBattle.Apply(definition.Start(), new OrdinaryMagicCommand.Advance()).State;
        var result = Query(swapped, state, new(seed, 0));
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.NotNull(outcome.ReachAdjustment);
        Assert.Equal(5, outcome.Movement.Length);
        Assert.Equal((ulong)outcome.Movement.Length + 2, result.RngState.DrawCount);
        Assert.Equal(finalSeed, result.RngState.Value);
        Assert.Equal((uint)raw, outcome.ReachAdjustment.Raw);
        Assert.Equal(reach, outcome.EffectiveReach);
        Assert.Equal(new TilePosition(x, 2, 0), outcome.BaselineDestination);
    }

    [Theory]
    [InlineData(SpellTargetTeam.Blue, true)]
    [InlineData(SpellTargetTeam.Red, false)]
    public void Ally_tiles_are_traversable_but_occupied_destinations_and_enemy_crossings_are_excluded(SpellTargetTeam team, bool beyond)
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(other: (1, 2, team));
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        Assert.DoesNotContain(new TilePosition(1, 2, 0), result.Outcome!.Geometry.AiOrigins);
        Assert.Equal(beyond, result.Outcome.Geometry.AiOrigins.Contains(new TilePosition(2, 2, 0)));
        Assert.DoesNotContain(result.Outcome.Movement, m => m.Destination == new TilePosition(1, 2, 0));
    }

    [Fact]
    public void Movement_already_taken_scores_only_the_origin_and_preserves_the_unused_ACT_refund()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup();
        var turns = state.Turns;
        state = state.With(turns: new(turns.Units.Select(u => u.Id == Mage
            ? new(u.Id, u.Speed, u.Ct, u.TieKey, true, true, false) : u), turns.RngState, turns.Ticks, turns.Phase));
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var move = Assert.Single(result.Outcome!.Movement);
        Assert.Equal(state.Units.Single(u => u.Id == Mage).Position, move.Destination);
        Assert.Equal(50, move.Clock.Units.Single(u => u.UnitId == Mage).Ct); // CT0 + ACT20 + three Speed10 ticks.
        Assert.True(move.Clock.Units.Single(u => u.UnitId == Mage).MovementTaken);
        Assert.False(move.Clock.Units.Single(u => u.UnitId == Mage).ActionTaken);
        Assert.Equal(2UL, result.RngState.DrawCount);
    }

    [Fact]
    public void Unused_choices_cap_CT_at60_then_the_ready_actor_has_choice_flags_cleared()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup();
        var turns = state.Turns;
        state = state.With(turns: new(turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed,
            u.Id == Mage ? 59 : 0, u.TieKey, u.Id == Mage)), turns.RngState, turns.Ticks, turns.Phase));
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var refresh = result.Outcome!.RefreshClock;
        Assert.Equal(4UL, refresh.ElapsedTicks); // CT59 +40 ->60 cap, then four Speed10 additions.
        Assert.Equal(Mage, Assert.Single(refresh.Events).UnitId);
        Assert.Equal(0, refresh.Units.Single(u => u.UnitId == Mage).Ct);
        Assert.False(refresh.Units.Single(u => u.UnitId == Mage).ActionTaken);
        Assert.False(refresh.Units.Single(u => u.UnitId == Mage).MovementTaken);
        // Even moved hypotheses get ACT20: CT59+20 also reaches the same60 cap.
        Assert.All(result.Outcome.Movement, m => Assert.Equal(4UL, m.Clock.ElapsedTicks));
    }

    [Fact]
    public void Tick_exhaustion_rejects_without_advancing_or_publishing_the_refresh()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup();
        var turns = state.Turns;
        state = state.With(turns: new(turns.Units, turns.RngState, ulong.MaxValue, turns.Phase));
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.False(result.Accepted);
        Assert.Contains("TickCountExhausted", result.Rejection);
        Assert.Null(result.Outcome);
        Assert.Equal(new RandomState(57, 0), result.RngState);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Theory]
    [InlineData(short.MinValue, short.MinValue)]
    [InlineData(short.MaxValue, -32767)]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(1, -1)]
    public void Source_baseline_unsigned_load_negation_and_signed_store_are_preserved(short stored, short expected)
    {
        Assert.Equal(new(false, expected, 100), OrdinarySpellAiMovementQuery.RankBaselineSeed(stored));
    }

    [Fact]
    public void Failure_after_refresh_and_a_scored_tile_discards_all_diagnostics_and_restores_entry_rng()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup();
        var rng = new RandomState(57, ulong.MaxValue - 2);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state, rng);
        Assert.False(result.Accepted);
        Assert.Contains("RandomDrawCountExhausted", result.Rejection);
        Assert.Null(result.Outcome);
        Assert.Equal(rng, result.RngState);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Uncertified_or_healer_context_and_foreign_definition_fail_before_publishing_work()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup();
        Assert.False(Query(catalog, state, profile: new()).Accepted);
        var turns = state.Turns;
        var changedOrder = state.With(turns: new(turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed,
            u.Ct, 20 - u.TieKey, u.HasTurn, u.MovementTaken, u.ActionTaken)), turns.RngState, turns.Ticks, turns.Phase));
        Assert.False(Query(catalog, changedOrder).Accepted); // Runtime tie keys must certify the same source slots.
        var healer = OrdinarySpellAiAimTests.Setup(healing: true);
        Assert.False(Query(healer.Catalog, healer.State).Accepted);
        var foreign = OrdinarySpellAiAimTests.Setup();
        var result = Query(foreign.Catalog, state);
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.Equal(new RandomState(57, 0), result.RngState);
    }
}
