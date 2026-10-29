using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiOutOfReachSelectionTests
{
    private static readonly AttackThrowStoneAiPreparedSelectionProfile Selection = new(true, true, true, true, true);
    private static readonly TilePosition Right = new(2, 1, 0), FarTarget = new(6, 1, 0);

    [Fact]
    public void Out_of_range_unwritten_actor_is_not_read_and_literal_move_first_Stone_uses_one_owned_prefix()
    {
        foreach (var (move, destination, prefixWord, prefixDraws, lotteryWord, lotteryDraws, finalWord, finalDraws, raw) in new[] {
            (1, Right, 2524885223u, 9ul, 2633739833u, 15ul, 3210001534u, 16ul, 16212u),
            (3, new TilePosition(3, 2, 0), 2516284547u, 13ul, 836760821u, 19ul, 595337866u, 20ul, 9084u) })
        {
            var result = Select(Battle(move: move, target: FarTarget)); var value = Success(result); var selected = value.Selection;
            Assert.Contains(Origin, value.MovementScenario.Scenario1); Assert.Contains(Origin, value.MovementScenario.Scenario2);
            var actorCell = value.MovementScenario.Cells.Single(c => c.Tile == Origin);
            Assert.Equal(new AiMovementCellProvenance(Origin, 5, null), actorCell); Assert.DoesNotContain(Origin, value.ScanOrigins);
            Assert.Equal(value.MovementScenario.ScoreMask.ToArray(), value.ScanOrigins.ToArray());
            Assert.Empty(selected.Composition.Attack.Candidates);
            var stone = Assert.IsType<ThrowStoneAiScannedCandidate>(selected.Composition.ThrowStone.Candidate);
            Assert.Equal(destination, stone.Origin); Assert.Equal(destination, selected.Plan.Destination);
            Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, selected.Plan.Kind); Assert.False(selected.Plan.ActBeforeMove);
            Assert.True(selected.Plan.ChooseFacingAtWait); Assert.Equal(0, selected.SelectedRankedIndex);
            Assert.Equal((26, (byte)100), (selected.Plan.Action!.Score.Priority, selected.Plan.Action.Score.HitPercent));
            Assert.Equal(new FftRandomState(prefixWord, prefixDraws), selected.Composition.Movement[^1].RngState);
            Assert.Equal(new FftRandomState(lotteryWord, lotteryDraws), selected.LotteryEntryRng);
            Assert.Equal(new AiLotteryRoll(0, raw, (int)(raw % 100), 100, true), Assert.Single(selected.Lottery));
            Assert.Equal(new FftRandomState(finalWord, finalDraws), result.RngState);
            Assert.Equal(Serialize(value.MovementScenario.Movement), Serialize(selected.Composition.Movement));
            Assert.Equal(value.MovementScenario.BaselineIndex, selected.Composition.BaselineIndex);
        }
        var half = Select(Battle(move: 1, target: FarTarget), rng: new(20, 7)); var halved = Success(half);
        Assert.True(halved.MovementScenario.ReachAdjustment.Halved); Assert.True(halved.MovementScenario.DestinationUsedDistanceFallback);
        Assert.Equal(Right, halved.Selection.Plan.Destination); Assert.Equal(new FftRandomState(4007910005, 16), half.RngState);
    }

    [Fact]
    public void Fresh_actor_from_zero_Move_filter_fallback_can_cast_ACT_first_including_KO_and_halved_zero_reach()
    {
        foreach (int distance in new[] { 3, 4 })
        {
            var result = Select(Battle(move: 0, target: new(distance + 1, 1, 0))); var value = Success(result);
            Assert.True(value.MovementScenario.FilterUsedReachableFallback); Assert.True(Assert.Single(value.MovementScenario.Cells).PriorityWritten);
            Assert.Equal(new[] { Origin }, value.ScanOrigins); Assert.Empty(value.MovementScenario.Scenario2);
            Assert.True(value.Selection.Plan.ActBeforeMove); Assert.Equal(Origin, value.Selection.Plan.Destination);
            Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, value.Selection.Plan.Kind);
            Assert.Equal(new FftRandomState(3210001534, 16), result.RngState);
        }
        var ko = Select(Battle(move: 0, target: new(4, 1, 0), hp: 10));
        Assert.Equal(320, Success(ko).Selection.Plan.Action!.Score.Priority);
        Assert.Equal(new FftRandomState(267834847, 17), ko.RngState);
        var zero = Success(Select(Battle(move: 0, target: new(4, 1, 0)), rng: new(20, 7)));
        Assert.Equal(0, zero.MovementScenario.EffectiveReach); Assert.True(zero.Selection.Plan.ActBeforeMove);
    }

    [Fact]
    public void No_casts_zero_accuracy_and_failed_lottery_preserve_actual_costs_and_movement_fallback()
    {
        var none = Select(Battle(move: 1, target: new(7, 1, 0))); var empty = Success(none).Selection;
        Assert.Null(empty.Composition.ThrowStone.Candidate); Assert.Equal(AiLotteryReason.MovementFallback, empty.Reason);
        Assert.Equal(AttackThrowStoneAiPlanKind.MoveOnly, empty.Plan.Kind); Assert.Equal(Right, empty.Plan.Destination);
        Assert.Equal(new FftRandomState(662824084, 10), empty.LotteryEntryRng); Assert.Equal(8, empty.Lottery.Length);
        Assert.Equal(new FftRandomState(180171308, 18), none.RngState);
        var inaccurate = Select(Battle(move: 1, target: FarTarget, evade: 100)); var zero = Success(inaccurate).Selection;
        Assert.Equal(AiScoringDisposition.NotValuable, zero.Composition.ThrowStone.Candidate!.Evaluation.Disposition);
        Assert.All(zero.Composition.RankedActions, Assert.Null); Assert.Null(zero.Plan.Action);
        Assert.Equal(AttackThrowStoneAiPlanKind.MoveOnly, zero.Plan.Kind); Assert.Equal(Right, zero.Plan.Destination);
        Assert.Equal(new FftRandomState(2111915288, 22), inaccurate.RngState);
        var failed = Select(Battle(move: 0, target: new(4, 1, 0), evade: 75), rng: new(20, 7)); var low = Success(failed).Selection;
        Assert.Equal(AiLotteryReason.AllRollsFailedUseFirst, low.Reason); Assert.Equal(8, low.Lottery.Length);
        Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, low.Plan.Kind); Assert.True(low.Plan.ActBeforeMove);
        Assert.Equal(new FftRandomState(3660420676, 23), failed.RngState);
    }

    [Fact]
    public void Exact_and_short_budgets_and_late_formula_or_tick_failures_publish_no_partial_results()
    {
        foreach (var (move, target, hp, evade, seed, draws, slot) in new[] {
            (1, FarTarget, 50, 0, 1u, 9ul, 0), (0, new TilePosition(4,1,0), 10, 0, 1u, 10ul, 0),
            (0, new TilePosition(4,1,0), 50, 75, 20u, 16ul, 7), (1, FarTarget, 50, 100, 1u, 15ul, 7),
            (1, new TilePosition(7,1,0), 50, 0, 1u, 11ul, 7) })
        {
            var state = Battle(move: move, target: target, hp: hp, evade: evade); var before = Snapshot(state);
            var rng = new FftRandomState(seed, ulong.MaxValue - draws); var fits = Select(state, rng: rng); Success(fits);
            Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
            var shortBudget = rng with { DrawCount = rng.DrawCount + 1 };
            var failure = Atomic(Select(state, rng: shortBudget), AttackThrowStoneAiPreparedSelectionStage.Selection, shortBudget);
            Assert.Equal(slot, failure.SelectionFailure!.LotteryIndex); Assert.Equal(before, Snapshot(state));
        }
        var formulaBudget = new FftRandomState(1, ulong.MaxValue - 6);
        var formula = Atomic(Select(Battle(move: 1, target: FarTarget), rng: formulaBudget),
            AttackThrowStoneAiPreparedSelectionStage.Composition, formulaBudget);
        Assert.Equal(AttackThrowStoneAiStage.ThrowStone, formula.CompositionFailure!.Stage);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, formula.CompositionFailure.ThrowStoneFailure!.Detail!.AbilityRejection);
        var tick = Atomic(Select(Battle(move: 1, target: FarTarget, hp: 10, ticks: ulong.MaxValue - 1)),
            AttackThrowStoneAiPreparedSelectionStage.Composition);
        Assert.Equal(ThrowStoneAiCandidateRejection.TickCountExhausted, tick.CompositionFailure!.ThrowStoneFailure!.Detail!.CandidateRejection);
        var reachBudget = new FftRandomState(1, ulong.MaxValue - 2);
        Assert.Equal(AttackThrowStoneAiScenarioStage.ReachAdjustment,
            Atomic(Select(Battle(move: 1, target: FarTarget), rng: reachBudget), AttackThrowStoneAiPreparedSelectionStage.Scenario,
                reachBudget).ScenarioFailure!.Stage);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Unknown_actor_within_range_four_and_remaining_source_facts_reject_without_weakening_legacy_scans()
    {
        foreach (int distance in new[] { 3, 4 })
            Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnwrittenCastPriority,
                Atomic(Select(Battle(move: 1, target: new(distance + 1, 1, 0))), AttackThrowStoneAiPreparedSelectionStage.Provenance).Reason);
        var state = Battle(move: 1, target: FarTarget); var value = Success(Select(state));
        var full = value.MovementScenario;
        var legacy = Evaluate(state: state, scenario: new(full.ScoreMask, full.Scenario1, full.BaselineDestination, full.ActFirstDestination));
        Assert.False(legacy.Accepted); Assert.Equal(ThrowStoneAiScanRejection.InvalidScenario, legacy.Failure!.ThrowStoneFailure!.Reason);
        Assert.Equal(Decision, legacy.RngState);
        foreach (var profile in new[] { Selection with { NormalScoring = false }, Selection with { CanonicalCoordinates = false },
            Selection with { NoOtherScenarioOverrides = false }, Selection with { ClearDirectTrajectories = false }, Selection with { NoDefendSupport = false } })
            Atomic(Select(state, profile: profile), AttackThrowStoneAiPreparedSelectionStage.Admission);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.InReach,
            Atomic(Select(Battle()), AttackThrowStoneAiPreparedSelectionStage.Scenario).ScenarioFailure!.Reason);
        foreach (var team in new[] { SwordAiScenarioTeam.Blue, SwordAiScenarioTeam.Unknown })
            Assert.Equal(AttackThrowStoneAiPreparationRejection.UnsupportedTeam,
                Atomic(Select(state, team: team), AttackThrowStoneAiPreparedSelectionStage.Scenario).ScenarioFailure!.PreparationFailure!.Reason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_policy_orders_replay_plans_without_live_changes()
    {
        foreach (bool second in new[] { false, true })
        foreach (int move in new[] { 0, 3 })
        {
            var state = Battle(move: move, target: FarTarget, second: second); var before = Snapshot(state); var request = FreshRequest(second);
            var result = Select(state, request: request); var value = Success(result);
            Assert.Equal(request.ActorId, value.Selection.Plan.ActorId);
            Assert.Equal(Serialize(result), Serialize(Select(state, request: request)));
            Assert.Equal(Serialize(result), Serialize(Select(Battle(move: move, target: FarTarget, second: second, reverse: true),
                request: request with { Policies = request.Policies.Reverse().ToImmutableArray() })));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static AttackThrowStoneAiOutOfReachSelectionResult Select(AttackBattleSnapshot state,
        SwordAiRefreshRequest? request = null, AttackThrowStoneAiPreparedSelectionProfile? profile = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null)
    {
        var p = Profile(); return AttackThrowStoneAiOutOfReachSelection.Evaluate(state, request ?? FreshRequest(), Stone, new(Sword, 2),
            p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), profile ?? Selection, team, rng ?? Decision);
    }
    private static AttackThrowStoneAiOutOfReachSelectionOutcome Success(AttackThrowStoneAiOutOfReachSelectionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiOutOfReachSelectionOutcome>(result.Outcome); }
    private static AttackThrowStoneAiPreparedSelectionFailure Atomic(AttackThrowStoneAiOutOfReachSelectionResult result,
        AttackThrowStoneAiPreparedSelectionStage stage, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<AttackThrowStoneAiPreparedSelectionFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure; }
}
