using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPreparedSelectionTests
{
    private static readonly SwordAiPreparedSelectionProfile Profile = new(true, true, true, true, true, true);

    [Theory]
    [InlineData(0, 2633739833u, 15ul, 1)]
    [InlineData(89, 2111915288u, 22ul, 8)]
    public void Fresh_prefix_retains_exact_refresh_movement_formula_and_lottery_schedule(int evade, uint word, ulong count, int rolls)
    {
        var state = State(evade: evade); var result = Evaluate(state); var o = Success(result);
        Assert.Equal(new FftRandomState(word, count), result.RngState);
        Assert.Equal(16838u, Assert.Single(o.Preparation.Refresh.UpkeepPicks).RawRoll);
        Assert.Equal(5758u, Assert.Single(o.Decision.MovementScenario.Movement).Outcome.UpkeepPicks[0].RawRoll);
        Assert.Equal(new FftRandomState(3655513600, 14), o.Decision.Selection.LotteryEntryRng);
        Assert.Equal(rolls, o.Decision.Selection.Lottery.Length);
        Assert.Equal(7419u, o.Decision.Selection.Lottery[0].Raw); // First lottery draw after the retained refresh prefix.
        Assert.Equal(SwordAiSelectedPlanKind.Attack, o.Decision.Selection.Plan.Kind);
        Assert.Equal(new TilePosition(1, 1, 0), o.Decision.Selection.Plan.Destination);
        Assert.Equal(o.Preparation.Origin, o.Decision.MovementScenario.Scenario.BaselineDestination);
        Assert.Equal(o.Preparation.EnemyAbilityReach, o.Decision.MovementScenario.TargetReach);
        // Equivalent explicit stages, each fresh; this comparison never supplies a cached outcome as input.
        var prep = SwordAiPreparation.Evaluate(state, Request(), Candidate, Score, Refresh, Preparation, SwordAiScenarioTeam.NonBlue, Rng);
        var selected = SwordAiDerivedSelection.Evaluate(state, new(Weapon, 5), Candidate, Score, new(A, B, true),
            new(true, true, true), new(true, true, true, true, true, true, true, true), new(true, true, true), prep.RngState);
        Assert.Equal(Serialize(selected.Outcome), Serialize(o.Decision)); Assert.Equal(selected.RngState, result.RngState);
    }

    [Theory] [InlineData(100, 5)] [InlineData(0, 0)]
    public void Empty_ranking_runs_eight_rolls_then_falls_back_to_prepared_origin(int evade, int power)
    {
        var result = Evaluate(State(evade: evade), weapon: new(Weapon, power)); var o = Success(result);
        Assert.Equal(SwordAiSelectionReason.MovementFallback, o.Decision.Selection.Reason);
        Assert.Equal(8, o.Decision.Selection.Lottery.Length);
        Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Decision.Selection.Plan.Kind);
        Assert.Null(o.Decision.Selection.Plan.Attack); Assert.Equal(o.Preparation.Origin, o.Decision.Selection.Plan.Destination);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_collection_order_replay_without_mutating_live_CT_flags_positions_or_RNG()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = State(second: second); var before = Snapshot(state); var result = Evaluate(state, request: Request(second));
            var o = Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, request: Request(second))));
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(second: second, reverse: true), request: Request(second, reverse: true))));
            Assert.Equal(second ? B : A, o.Decision.Selection.Plan.ActorId);
            Assert.Equal(15, state.TurnBattle.Turns.Units.Single(u => u.Id == o.Decision.Selection.Plan.ActorId).Ct);
            Assert.Equal(before, Snapshot(state)); Assert.Equal(LiveRng, state.TurnBattle.Battle.RngState);
        }
    }

    [Fact]
    public void Required_draw_budget_is_exact_and_any_late_failure_rolls_back_the_whole_prefix()
    {
        var state = State();
        var start = new FftRandomState(1, ulong.MaxValue - 8);
        Assert.Equal(ulong.MaxValue, Evaluate(state, rng: start).RngState.DrawCount);
        foreach (int available in new[] { 0, 1, 2, 7 })
        {
            var rng = new FftRandomState(1, ulong.MaxValue - (ulong)available);
            var result = Evaluate(state, rng: rng);
            Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState);
            Assert.Equal(available == 0 ? SwordAiPreparedSelectionStage.Preparation : SwordAiPreparedSelectionStage.Selection, result.Failure!.Stage);
        }
        var lowAccuracy = State(evade: 89);
        Assert.Equal(ulong.MaxValue, Evaluate(lowAccuracy, rng: new(1, ulong.MaxValue - 15)).RngState.DrawCount);
        var lotteryRng = new FftRandomState(1, ulong.MaxValue - 14);
        var lotteryFailure = Evaluate(lowAccuracy, rng: lotteryRng);
        Assert.Equal(SwordAiDerivedSelectionStage.Lottery, lotteryFailure.Failure!.SelectionStage);
        Assert.Null(lotteryFailure.Outcome); Assert.Equal(lotteryRng, lotteryFailure.RngState);
        var lateTick = Evaluate(State(targetCt: 0, speed: 10, targetSpeed: 1, ticks: ulong.MaxValue - 7));
        Assert.Equal(SwordAiDerivedSelectionStage.Composition, lateTick.Failure!.SelectionStage);
        Assert.Null(lateTick.Outcome); Assert.Equal(Rng, lateTick.RngState);
        var tickFailure = Evaluate(State(ticks: ulong.MaxValue));
        Assert.Equal(SwordAiRefreshRejection.TickCountExhausted, tickFailure.Failure!.RefreshRejection);
        Assert.Null(tickFailure.Outcome); Assert.Equal(Rng, tickFailure.RngState);
        // All simulations can select the same already-ready peer on the restored snapshot without adding a tick.
        Assert.True(Evaluate(State(ticks: ulong.MaxValue, targetCt: 100)).Accepted);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Remaining_assertions_unknown_preparation_and_unsupported_selection_publish_reason_only_failures()
    {
        foreach (var profile in new[] { new SwordAiPreparedSelectionProfile(), Profile with { NormalScoring = false },
            Profile with { CanonicalCoordinates = false }, Profile with { OrdinaryScenarioPolicy = false },
            Profile with { OrdinaryAttackTilePolicy = false }, Profile with { TargetSelectionMatchesBaseline = false },
            Profile with { NoDefendSupport = false } })
            Atomic(Evaluate(State(), profile: profile), SwordAiPreparedSelectionStage.Admission);
        Atomic(Evaluate(State(), preparation: new()), SwordAiPreparedSelectionStage.Preparation);
        Atomic(Evaluate(State(), team: SwordAiScenarioTeam.Blue), SwordAiPreparedSelectionStage.Preparation);
        Atomic(Evaluate(State(), weapon: new(new("sample:item/wrong"), 5)), SwordAiPreparedSelectionStage.Selection);
        Atomic(Evaluate(State(distance: 4, move: 2)), SwordAiPreparedSelectionStage.Selection); // Outside the closed in-reach branch.
    }

    private static SwordAiPreparedSelectionResult Evaluate(AttackBattleSnapshot state, BoundedSwordWeapon? weapon = null,
        SwordAiRefreshRequest? request = null, SwordAiPreparedSelectionProfile? profile = null,
        SwordAiPreparationProfile? preparation = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null) =>
        SwordAiPreparedSelection.Evaluate(state, request ?? Request(), weapon ?? new(Weapon, 5), Candidate, Score, Refresh,
            preparation ?? Preparation, team, profile ?? Profile, rng ?? Rng);
    private static SwordAiPreparedSelectionOutcome Success(SwordAiPreparedSelectionResult result)
    {
        Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure);
        return Assert.IsType<SwordAiPreparedSelectionOutcome>(result.Outcome);
    }
    private static void Atomic(SwordAiPreparedSelectionResult result, SwordAiPreparedSelectionStage stage)
    {
        Assert.False(result.Accepted); Assert.Equal(stage, result.Failure!.Stage); Assert.Null(result.Outcome); Assert.Equal(Rng, result.RngState);
    }
}
