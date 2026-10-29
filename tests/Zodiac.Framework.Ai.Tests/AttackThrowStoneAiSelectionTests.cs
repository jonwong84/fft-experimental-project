using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiSelectionTests
{
    private static readonly AttackThrowStoneAiSelectionProfile Selection = new(true, true, true);

    [Fact]
    public void Fresh_full_accuracy_and_failed_first_roll_select_typed_actions_with_original_order_and_deferred_facing()
    {
        foreach (var (power, kind, destination, actFirst) in new[] {
            (1, AttackThrowStoneAiPlanKind.ThrowStone, Far, false), (4, AttackThrowStoneAiPlanKind.Attack, Origin, true) })
        {
            var result = Select(power: power); var outcome = Success(result);
            Assert.Equal(new FftRandomState(836760821, 19), outcome.LotteryEntryRng);
            Assert.Equal(new AiLotteryRoll(0, 9084, 84, 100, true), Assert.Single(outcome.Lottery));
            Assert.Equal(AiLotteryReason.FirstRoll, outcome.Reason); Assert.Equal(0, outcome.SelectedRankedIndex);
            Assert.Equal((kind, destination, actFirst), (outcome.Plan.Kind, outcome.Plan.Destination, outcome.Plan.ActBeforeMove));
            Assert.True(outcome.Plan.ChooseFacingAtWait); Assert.Equal(new FftRandomState(595337866, 20), result.RngState);
        }
        var second = Select(Battle(evade: 75), power: 4, rng: new(10, 7)); var selected = Success(second);
        Assert.Equal(1, selected.SelectedRankedIndex); Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone, selected.Plan.Kind);
        Assert.Collection(selected.Lottery, r => Assert.Equal(new AiLotteryRoll(0, 7136, 36, 25, false), r),
            r => Assert.Equal(new AiLotteryRoll(1, 13, 13, 25, true), r));
        Assert.Equal(new FftRandomState(2148343772, 21), second.RngState);
    }

    [Fact]
    public void Failed_real_slots_consume_empty_slots_then_keep_first_while_empty_ranking_moves_to_D0()
    {
        var result = Select(Battle(evade: 75), power: 4); var outcome = Success(result);
        Assert.Equal(AiLotteryReason.AllRollsFailedUseFirst, outcome.Reason); Assert.Equal(0, outcome.SelectedRankedIndex);
        Assert.Equal(AttackThrowStoneAiPlanKind.Attack, outcome.Plan.Kind); Assert.Equal(8, outcome.Lottery.Length);
        Assert.Equal(new uint[] { 9084, 12060, 32225, 17543, 25089, 21183, 25137, 25566 }, outcome.Lottery.Select(r => r.Raw));
        Assert.Equal(new[] { 25, 25, 0, 0, 0, 0, 0, 0 }, outcome.Lottery.Select(r => r.HitPercent));
        Assert.All(outcome.Lottery, r => Assert.False(r.Passed)); Assert.Equal(new FftRandomState(3823029677, 27), result.RngState);
        foreach (var (state, profile, expected) in new[] {
            (Battle(), Profile(active: false), new FftRandomState(267834847, 17)),
            (Battle(evade: 100), Profile(), new FftRandomState(3535774167, 25)) })
        {
            result = Select(state, profile: profile); outcome = Success(result);
            Assert.Equal(AiLotteryReason.MovementFallback, outcome.Reason); Assert.Null(outcome.SelectedRankedIndex);
            Assert.Equal(AttackThrowStoneAiPlanKind.MoveOnly, outcome.Plan.Kind); Assert.Null(outcome.Plan.Action);
            Assert.Equal(Origin, outcome.Plan.Destination); Assert.False(outcome.Plan.ActBeforeMove); Assert.True(outcome.Plan.ChooseFacingAtWait);
            Assert.Equal(8, outcome.Lottery.Length); Assert.All(outcome.Lottery, r => Assert.Equal(0, r.HitPercent));
            Assert.Equal(expected, result.RngState);
        }
    }

    [Fact]
    public void Exact_first_second_and_last_slot_budgets_fit_and_late_failure_rolls_back_the_entire_decision()
    {
        foreach (var (seed, evade, active, draws, failedIndex) in new[] {
            (1u, 0, true, 13ul, 0), (10u, 75, true, 14ul, 1), (1u, 75, true, 20ul, 7), (1u, 0, false, 10ul, 7) })
        {
            var state = Battle(evade: evade); var before = Snapshot(state); var profile = Profile(active);
            var rng = new FftRandomState(seed, ulong.MaxValue - draws);
            var fits = Select(state, power: 4, profile: profile, rng: rng); Success(fits);
            Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
            var shortBudget = rng with { DrawCount = rng.DrawCount + 1 };
            var failure = Atomic(Select(state, power: 4, profile: profile, rng: shortBudget), AttackThrowStoneAiSelectionStage.Lottery, shortBudget);
            Assert.Equal(failedIndex, failure.LotteryIndex); Assert.Equal(AttackThrowStoneAiSelectionRejection.RandomDrawCountExhausted, failure.Reason);
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Terminal_no_Defend_and_fresh_defaults_are_explicit_and_composition_failure_exposes_no_plan()
    {
        foreach (var certificate in new[] { Selection with { TerminalThrowStoneEntry = false },
            Selection with { NoDefendSupport = false }, Selection with { FreshOrdinarySelectionDefaults = false } })
            Assert.Equal(AttackThrowStoneAiSelectionRejection.UnsupportedProfile,
                Atomic(Select(selection: certificate), AttackThrowStoneAiSelectionStage.Admission).Reason);
        var p = Profile(); p = p with { ThrowStoneScan = p.ThrowStoneScan with { NoInterveningRandomWork = false } };
        var failure = Atomic(Select(profile: p), AttackThrowStoneAiSelectionStage.Composition);
        Assert.Equal(AttackThrowStoneAiSelectionRejection.CompositionRejected, failure.Reason);
        Assert.Equal(AttackThrowStoneAiStage.Admission, failure.CompositionFailure!.Stage);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actors_replay_selected_payloads_without_live_RNG_choices_or_state_changes()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second, evade: 75); var before = Snapshot(state);
            var result = Select(state, power: 4, rng: new(10, 7), second: second); var outcome = Success(result);
            Assert.Equal(second ? B : A, outcome.Plan.ActorId); Assert.IsType<AttackThrowStoneAiStone>(outcome.Plan.Action);
            Assert.Equal(Serialize(result), Serialize(Select(state, power: 4, rng: new(10, 7), second: second)));
            Assert.Equal(Serialize(result), Serialize(Select(Battle(second: second, evade: 75, reverse: true), power: 4,
                scenario: new([Far, Origin], [Far, Origin], Origin, Origin), rng: new(10, 7), second: second)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static AttackThrowStoneAiSelectionResult Select(AttackBattleSnapshot? state = null, int power = 2,
        ThrowStoneAiScanScenario? scenario = null, AttackThrowStoneAiProfile? profile = null,
        AttackThrowStoneAiSelectionProfile? selection = null, FftRandomState? rng = null, bool second = false) =>
        AttackThrowStoneAiSelection.Evaluate(state ?? Battle(second: second), Request(second), scenario ?? Scenario(),
            new(Sword, power), profile ?? Profile(), selection ?? Selection, rng ?? Decision);
    private static AttackThrowStoneAiSelectionOutcome Success(AttackThrowStoneAiSelectionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiSelectionOutcome>(result.Outcome); }
    private static AttackThrowStoneAiSelectionFailure Atomic(AttackThrowStoneAiSelectionResult result,
        AttackThrowStoneAiSelectionStage stage, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<AttackThrowStoneAiSelectionFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure; }
}
