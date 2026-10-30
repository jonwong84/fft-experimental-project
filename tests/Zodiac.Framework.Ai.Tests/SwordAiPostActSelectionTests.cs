using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPostActSelectionTests
{
    [Fact]
    public void Actual_hit_and_miss_consume_eight_empty_rolls_without_an_Attack_scan_or_repeat_prefix()
    {
        foreach (var (evade, word, count, first, last) in new[] {
            (0, 3834121963u, 37ul, 20495u, 25736u), (89, 3776134388u, 42ul, 13145u, 24851u) })
        {
            var state = AfterAct(evade: evade).Battle; var before = Snapshot(state); var result = Evaluate(state); var o = Success(result);
            Assert.Equal(SwordAiSelectionReason.MovementFallback, o.Reason);
            Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Plan.Kind); Assert.Null(o.Plan.Attack); Assert.False(o.Plan.ActBeforeMove);
            Assert.Equal(new TilePosition(2, 2, 0), o.Plan.Destination); Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, o.Plan.WaitFacingHint);
            var scenarios = SwordAiPostActScenarioTests.Evaluate(state);
            Assert.Equal(Serialize(scenarios.Outcome), Serialize(o.Scenarios)); Assert.Equal(scenarios.RngState, o.LotteryEntryRng);
            Assert.Equal(new FftRandomState(word, count), result.RngState);
            Assert.Equal(first, o.Lottery[0].Raw); Assert.Equal(last, o.Lottery[^1].Raw);
            Assert.Equal(Enumerable.Range(0, 8), o.Lottery.Select(roll => roll.Slot));
            var rng = o.LotteryEntryRng;
            foreach (var roll in o.Lottery)
            {
                var draw = new FftBiosRandom().Next(rng); rng = draw.State;
                Assert.Equal(draw.Value, roll.Raw); Assert.Equal((int)(roll.Raw % 100), roll.Modulo);
                Assert.Equal(0, roll.HitPercent); Assert.False(roll.Passed);
            }
            Assert.Equal(rng, result.RngState); Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Out_of_reach_adjustment_precedes_empty_lottery_and_zero_Move_can_select_stay()
    {
        var state = State(moved: false, acted: true, move: 1, distance: 3); var result = Evaluate(state, outOfReach: true, rng: Rng);
        var o = Success(result); Assert.Equal(new TilePosition(2, 1, 0), o.Plan.Destination);
        Assert.Equal(new FftRandomState(180171308, 18), result.RngState);
        Assert.Equal(10113u, o.Scenarios.OutOfReach!.ReachAdjustment!.Raw);
        Assert.Equal(17515u, o.Lottery[0].Raw); Assert.Equal(2749u, o.Lottery[^1].Raw);
        var zero = State(moved: false, acted: true, move: 0, distance: 1);
        o = Success(Evaluate(zero)); Assert.Equal(new TilePosition(1, 1, 0), o.Plan.Destination);
        Assert.Single(o.Scenarios.MovementScenario.Movement); Assert.Equal(8, o.Lottery.Length);
    }

    [Fact]
    public void Exact_full_budget_and_last_roll_failure_are_atomic_and_no_Defend_remains_explicit()
    {
        var state = AfterAct().Battle; var last = new FftRandomState(state.TurnBattle.Turns.RngState.Value, ulong.MaxValue - 13);
        var fits = Evaluate(state, rng: last); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var shortBudget = last with { DrawCount = ulong.MaxValue - 12 }; var before = Snapshot(state);
        var failed = Evaluate(state, rng: shortBudget); Atomic(failed, SwordAiPostActSelectionRejection.LotteryRejected, shortBudget);
        Assert.Equal(7, failed.Failure!.LotteryFailure!.LotteryIndex);
        Assert.Equal(SwordAiSelectionRejection.RandomDrawCountExhausted, failed.Failure.LotteryFailure.Reason);
        Atomic(Evaluate(state, profile: new()), SwordAiPostActSelectionRejection.UnsupportedProfile, state.TurnBattle.Turns.RngState);
        Atomic(Evaluate(state, outOfReach: true), SwordAiPostActSelectionRejection.ScenariosRejected, state.TurnBattle.Turns.RngState);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_order_replay_from_actual_Critical_or_knockback_without_committing_RNG()
    {
        foreach (bool second in new[] { false, true })
        foreach (var (power, entry) in new[] { (8, 1u), (1, 3u) })
        {
            var state = AfterAct(power: power, entryWord: entry, second: second).Battle; var before = Snapshot(state);
            var result = Evaluate(state, second: second); var o = Success(result);
            Assert.Equal(second ? B : A, o.Plan.ActorId); Assert.False(o.Scenarios.Preparation.ActorAttackUsable);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, second: second)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(AfterAct(power: power, entryWord: entry, second: second, reverse: true).Battle, second: second)));
            Assert.Equal(before, Snapshot(state));
        }
        var pre = Ready(move: false).Battle;
        Atomic(Evaluate(pre), SwordAiPostActSelectionRejection.ScenariosRejected, pre.TurnBattle.Turns.RngState);
    }

    internal static SwordAiPostActSelectionResult Evaluate(AttackBattleSnapshot state, bool outOfReach = false,
        bool second = false, SwordAiPostActSelectionProfile? profile = null, FftRandomState? rng = null) => outOfReach
        ? SwordAiPostActSelection.EvaluateOutOfReach(state, Request(second), Candidate, Score, Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, SwordAiPostActScenarioTests.Profile, profile ?? new(true), rng ?? state.TurnBattle.Turns.RngState)
        : SwordAiPostActSelection.EvaluateInReach(state, Request(second), Candidate, Score, Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, SwordAiPostActScenarioTests.Profile, profile ?? new(true), rng ?? state.TurnBattle.Turns.RngState);
    private static SwordAiPostActSelectionOutcome Success(SwordAiPostActSelectionResult result)
    { Assert.True(result.Accepted, Serialize(result)); Assert.Null(result.Failure); return Assert.IsType<SwordAiPostActSelectionOutcome>(result.Outcome); }
    private static void Atomic(SwordAiPostActSelectionResult result, SwordAiPostActSelectionRejection reason, FftRandomState rng)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Failure!.Reason); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState); }
}
