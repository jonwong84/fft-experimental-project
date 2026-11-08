using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiOriginEvaluationTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard");
    private static DefaultAbilityAiOriginEvaluationResult Query(DefaultAbilityAiCatalog catalog, OrdinaryMagicBattleState state,
        TilePosition origin, DefaultAbilityAiAimPassKind pass = DefaultAbilityAiAimPassKind.UnitsStillAtDeadline,
        RandomState? rng = null) => DefaultAbilityAiOriginEvaluationQuery.Evaluate(state, catalog,
            new(Mage, 0, pass, origin), new(true), rng ?? new(57, 0));

    [Theory]
    [InlineData(0, 20UL, 30)]
    [InlineData(2, 19UL, 10)]
    public void ACT_first_prepares_at_the_actor_origin_then_moves_with_no_unused_Move_refund(byte charge, ulong draws, int ct)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: charge);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state, new(0, 2, 0));
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.True(outcome.Origin.ActBeforeMove);
        Assert.Equal(new TilePosition(0, 4, 0), outcome.Origin.MovementDestination);
        var candidate = Assert.Single(outcome.Evaluations).Candidate.Outcome!;
        var actor = candidate.Units.Single(u => u.Id == Mage);
        Assert.Equal(new TilePosition(0, 4, 0), actor.Occupant.Position);
        Assert.Equal((ushort)69, actor.Current.Mp);
        Assert.Equal((ushort)146, candidate.Units.Single(u => u.Id == Guard).Vitals.Hp);
        Assert.Equal((short)49, candidate.Score!.Priority);
        Assert.Equal(ct, candidate.Clock!.Units.Single(u => u.UnitId == Mage).Ct);
        Assert.True(candidate.Clock.Units.Single(u => u.UnitId == Mage).MovementTaken);
        Assert.Equal((byte)4, outcome.DestinationProximity);
        Assert.True(outcome.PassesBaseline);
        Assert.Equal(draws, result.RngState.DrawCount);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void MOVE_first_can_prepare_beyond_the_original_cast_range_and_consumes_movement_despite_preplacement()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(targetX: 6);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state, new(2, 2, 0));
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.False(outcome.Origin.ActBeforeMove);
        var candidate = Assert.Single(outcome.Evaluations).Candidate.Outcome!;
        Assert.Equal(new TilePosition(2, 2, 0), candidate.Units.Single(u => u.Id == Mage).Occupant.Position);
        Assert.Equal((ushort)146, candidate.Units.Single(u => u.Id == Guard).Vitals.Hp);
        Assert.Equal(10, candidate.Clock!.Units.Single(u => u.UnitId == Mage).Ct);
        Assert.True(candidate.Clock.Units.Single(u => u.UnitId == Mage).MovementTaken);
        Assert.Equal(10UL, result.RngState.DrawCount); // Six prefix draws, four formula draws.
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Theory]
    [InlineData(0, 46, 2, -22, false, 22UL)]
    [InlineData(2, 100, 1, 49, true, 17UL)]
    public void Immediate_self_collateral_happens_before_ACT_first_movement_but_delayed_strikes_use_the_new_caster_position(
        byte charge, ushort hp, int frames, short priority, bool passes, ulong draws)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: charge, area: 2, targetX: 2);
        var result = Query(catalog, state, new(0, 2, 0));
        Assert.True(result.Accepted, result.Rejection);
        var candidate = Assert.Single(result.Outcome!.Evaluations).Candidate.Outcome!;
        Assert.Equal(hp, candidate.Units.Single(u => u.Id == Mage).Vitals.Hp);
        Assert.Equal(new TilePosition(0, 4, 0), candidate.Units.Single(u => u.Id == Mage).Occupant.Position);
        Assert.Equal(frames, candidate.Frames.Length);
        Assert.Equal(priority, candidate.Score!.Priority);
        Assert.Equal(passes, result.Outcome.PassesBaseline);
        Assert.Equal(draws, result.RngState.DrawCount);
    }

    [Fact]
    public void One_origin_keeps_first_equal_best_and_each_aim_restores_units_while_rng_continues()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state, new(0, 2, 0), DefaultAbilityAiAimPassKind.Tiles);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(new[] { new TilePosition(3, 1, 0), new(2, 2, 0), new(3, 2, 0), new(3, 3, 0) },
            outcome.Evaluations.Select(e => e.Aim.Position));
        Assert.Equal(new TilePosition(3, 1, 0), outcome.Best!.Aim.Position);
        Assert.Single(outcome.Evaluations, e => e.ImprovedBest);
        Assert.Equal((short)-22, outcome.Evaluations[1].Candidate.Outcome!.Score!.Priority);
        Assert.All(outcome.Evaluations.Where((_, i) => i != 1), e => {
            Assert.Equal((short)49, e.Candidate.Outcome!.Score!.Priority);
            Assert.Equal((ushort)100, e.Candidate.Outcome.Units.Single(u => u.Id == Mage).Vitals.Hp);
        });
        for (int i = 1; i < outcome.Evaluations.Length; i++)
            Assert.Equal(outcome.Evaluations[i - 1].Candidate.RngState, outcome.Evaluations[i].EntryRng);
        Assert.Equal(39UL, result.RngState.DrawCount);
        Assert.Equal(400935288U, result.RngState.Value);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
        var replay = Query(catalog, state, new(0, 2, 0), DefaultAbilityAiAimPassKind.Tiles);
        Assert.Equal(result.RngState, replay.RngState);
        Assert.Equal(JsonSerializer.Serialize(outcome.Evaluations), JsonSerializer.Serialize(replay.Outcome!.Evaluations));
    }

    [Fact]
    public void Nonvaluable_formula_draws_continue_without_ACT_first_movement_clock_or_resource_adoption()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, range: 3, area: 3, targetX: 2, alliesOnly: true);
        var result = Query(catalog, state, new(0, 2, 0), DefaultAbilityAiAimPassKind.Tiles);
        Assert.True(result.Accepted, result.Rejection);
        Assert.Null(result.Outcome!.Best);
        Assert.False(result.Outcome.PassesBaseline);
        Assert.Equal(11, result.Outcome.Evaluations.Length);
        Assert.All(result.Outcome.Evaluations, e => {
            Assert.Equal(DefaultAbilityAiCandidateDisposition.NotValuable, e.Candidate.Disposition);
            Assert.Equal(4UL, e.Candidate.RngState.DrawCount - e.EntryRng.DrawCount);
            Assert.Null(e.Candidate.Outcome!.Clock);
            Assert.Equal((ushort)75, e.Candidate.Outcome.Units.Single(u => u.Id == Mage).Current.Mp);
            Assert.Equal(new TilePosition(0, 2, 0), e.Candidate.Outcome.Units.Single(u => u.Id == Mage).Occupant.Position);
        });
        for (int i = 1; i < result.Outcome.Evaluations.Length; i++)
            Assert.Equal(result.Outcome.Evaluations[i - 1].Candidate.RngState, result.Outcome.Evaluations[i].EntryRng);
        Assert.Equal(57UL, result.RngState.DrawCount); // Thirteen prefix draws plus eleven four-draw nonvaluable attempts.
    }

    private static (DefaultAbilityAiCatalog Catalog, OrdinaryMagicBattleState State) EditSpell(
        (DefaultAbilityAiCatalog Catalog, OrdinaryMagicBattleState State) setup, Func<MagicCastDefinition, MagicCastDefinition> edit)
    {
        var d = setup.State.Definition;
        var spell = edit(d.Spells.Values.Single());
        var definition = new OrdinaryMagicBattleDefinition(d.Map, d.Units, [spell], d.Jobs, d.JobThresholds,
            d.InitialRng, d.MovementGround, d.Learning);
        var catalog = new DefaultAbilityAiCatalog(definition, setup.Catalog.PrimaryByJob.Select(p => KeyValuePair.Create(p.Key,
            p.Value with { Slots = p.Value.Slots.Select(s => s with { Spell = spell }).ToImmutableArray() })));
        return (catalog, OrdinaryMagicBattle.Apply(definition.Start(), new OrdinaryMagicCommand.Advance()).State);
    }

    [Fact]
    public void Equal_baseline_rank_rejects_admission_even_when_final_destination_has_better_foe_proximity()
    {
        var (catalog, state) = EditSpell(DefaultAbilityAiAimTests.Setup(charge: 0),
            spell => spell with { MpCost = 0, Spell = spell.Spell with { Power = 0 } });
        var result = Query(catalog, state, new(0, 2, 0));
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.NotNull(outcome.Best);
        Assert.Equal((short)0, outcome.Best.Candidate.Outcome!.Score!.Priority);
        Assert.Equal((short)0, outcome.Origins.Movement.BaselineRank.Priority);
        Assert.True(outcome.DestinationProximity < outcome.Origins.Movement.Cells
            .Single(c => c.Tile == outcome.Origins.Movement.BaselineDestination).Proximity);
        Assert.False(outcome.PassesBaseline);
    }

    [Fact]
    public void An_offered_origin_with_no_selectable_aims_returns_no_best_without_candidate_draws()
    {
        var (catalog, state) = EditSpell(DefaultAbilityAiAimTests.Setup(charge: 0, width: 2, targetX: 1, range: 1),
            spell => spell with { Targeting = spell.Targeting! with { CannotTargetSelf = true } });
        var result = Query(catalog, state, new(0, 2, 0), DefaultAbilityAiAimPassKind.Tiles);
        Assert.True(result.Accepted, result.Rejection);
        Assert.Empty(result.Outcome!.Evaluations);
        Assert.Null(result.Outcome.Best);
        Assert.False(result.Outcome.PassesBaseline);
        Assert.Equal(result.Outcome.Origins.Movement.Movement[^1].RngState, result.RngState);
    }

    [Fact]
    public void Unresolved_hit255_is_compared_before_the_origin_stops_and_can_pass_equal_priority_baseline()
    {
        var (catalog, state) = DefaultAbilityAiSearchTests.Congested(healing: false);
        var result = Query(catalog, state, state.Units.Single(u => u.Id == Mage).Position, DefaultAbilityAiAimPassKind.Units);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.True(outcome.Origin.Aims.Length > 1);
        Assert.True(outcome.OriginStoppedUnresolved);
        var candidate = Assert.Single(outcome.Evaluations).Candidate;
        Assert.Equal(DefaultAbilityAiCandidateDisposition.ScoredUnresolved, candidate.Disposition);
        Assert.Same(outcome.Evaluations[0], outcome.Best);
        Assert.Equal((byte)255, candidate.Outcome!.Score!.HitPercent);
        Assert.Equal((short)-6774, candidate.Outcome.Score.Priority);
        Assert.True(outcome.PassesBaseline);
        Assert.Equal(outcome.Evaluations[0].EntryRng.DrawCount + 255, result.RngState.DrawCount);
    }

    [Fact]
    public void Late_draw_exhaustion_after_a_valid_candidate_discards_prefix_and_best()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2);
        var rng = new RandomState(57, ulong.MaxValue - 20); // Prefix15 + first candidate5 fit; next formula fails.
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state, new(0, 2, 0), DefaultAbilityAiAimPassKind.Tiles, rng);
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.Equal(rng, result.RngState);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Immediate_caster_KO_keeps_the_existing_terminal_boundary_and_restores_the_entire_query()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2, targetX: 2);
        state = state.With(units: state.Units.SetItem(0, state.Units[0] with { Vitals = state.Units[0].Vitals with { Hp = 40 } }));
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state, new(0, 2, 0));
        Assert.False(result.Accepted);
        Assert.Contains("Immediate caster KO", result.Rejection);
        Assert.Null(result.Outcome);
        Assert.Equal(new RandomState(57, 0), result.RngState);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Enemy_KO_retains_existing_plain_corpse_upkeep_before_the_actor_ready_event()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, targetX: 2);
        state = state.With(units: state.Units.SetItem(1, state.Units[1] with {
            Vitals = state.Units[1].Vitals with { Hp = 40, Critical = true } }));
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state, new(0, 2, 0));
        Assert.True(result.Accepted, result.Rejection);
        var candidate = Assert.Single(result.Outcome!.Evaluations).Candidate.Outcome!;
        Assert.True(candidate.Units.Single(u => u.Id == Guard).Current.Statuses.Dead);
        Assert.Equal((ushort)0, candidate.Units.Single(u => u.Id == Guard).Vitals.Hp);
        Assert.Equal((short)334, candidate.Score!.Priority);
        Assert.Equal(10UL, candidate.Clock!.ElapsedTicks);
        Assert.Equal(new[] { Guard, Mage }, candidate.Clock.UpkeepPicks.Select(p => p.UnitId));
        Assert.True(candidate.Clock.UpkeepPicks[0].Dead);
        Assert.Equal(Mage, Assert.Single(candidate.Clock.Events).UnitId);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Invalid_or_unavailable_selections_and_unknown_priorities_reject_atomically(int change)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(targetX: change == 3 ? 6 : 4, range: change == 3 ? (byte)6 : (byte)4);
        var request = new DefaultAbilityAiOriginEvaluationRequest(Mage, change == 2 ? 1 : 0,
            change == 1 ? (DefaultAbilityAiAimPassKind)999 : DefaultAbilityAiAimPassKind.UnitsStillAtDeadline,
            change == 0 ? new(6, 4, 0) : new(0, 2, 0));
        var result = DefaultAbilityAiOriginEvaluationQuery.Evaluate(state, catalog, request, new(true), new(57, 7));
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.Equal(new RandomState(57, 7), result.RngState);
    }
}
