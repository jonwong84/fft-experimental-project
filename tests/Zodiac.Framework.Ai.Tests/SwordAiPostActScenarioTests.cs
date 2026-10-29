using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPostActScenarioTests
{
    internal static readonly SwordAiPostActScenarioProfile Profile = new(true, true, true);

    [Fact]
    public void Actual_hit_and_miss_score_four_restored_origins_once_and_copy_D1_into_D0()
    {
        foreach (var (evade, word, count, score) in new[] { (0, 2473755763u, 29ul, 64), (89, 1116175964u, 34ul, 0) })
        {
            var state = AfterAct(evade: evade).Battle; var before = Snapshot(state); var result = Evaluate(state); var o = Success(result);
            Assert.False(o.Preparation.ActorAttackUsable); Assert.Equal((2, 1), (o.Preparation.EnemyAbilityReach, o.MovementScenario.TargetDistance));
            var scenario = o.MovementScenario;
            Assert.Equal(new[] { new TilePosition(2, 0, 0), new(2, 1, 0), new(3, 1, 0), new(2, 2, 0) }, scenario.Scenario.MovementOrigins);
            Assert.Equal(new TilePosition(2, 2, 0), scenario.Scenario.AttackDestination);
            Assert.Equal(scenario.Scenario.AttackDestination, scenario.Scenario.BaselineDestination);
            Assert.False(scenario.BaselineUsedDistanceFallback); Assert.Equal(3, scenario.BaselineIndex); Assert.Null(o.OutOfReach);
            Assert.Equal(new FftRandomState(word, count), result.RngState);
            var rng = state.TurnBattle.Turns.RngState;
            rng = new FftBiosRandom().Next(rng).State; // The one owned refresh precedes cell scoring.
            foreach (var tile in scenario.Movement)
            {
                rng = new FftBiosRandom().Next(rng).State; Assert.Equal(rng, tile.RngState);
                Assert.Equal(score, tile.Outcome.Score.Priority);
                Assert.Equal(tile.Outcome.Destination == new TilePosition(2, 1, 0) ? 35 : 15, tile.Outcome.TurnEndCt);
            }
            Assert.Equal(rng, result.RngState); Assert.Equal(before, Snapshot(state));
            var legacy = SwordAiMovementScenario.Evaluate(state, Candidate, Score, new(A, B, true), new(true, true, true), rng);
            Assert.False(legacy.Accepted); Assert.Equal(SwordAiMovementBaselineRejection.UnavailableAction, legacy.Failure!.MovementReason);
        }
    }

    [Fact]
    public void Out_of_reach_scores_filtered_mask_before_NonBlue_adjustment_and_keeps_ACT_used_D0_copy()
    {
        var state = State(moved: false, acted: true, move: 1, distance: 3);
        foreach (var (seed, halved) in new[] { (1u, false), (20u, true) })
        {
            var rng = new FftRandomState(seed, 7); var result = Evaluate(state, outOfReach: true, rng: rng); var o = Success(result);
            Assert.Equal(new TilePosition(2, 1, 0), Assert.Single(o.MovementScenario.Scenario.MovementOrigins));
            var reach = Assert.IsType<SwordAiOutOfReachScenarioOutcome>(o.OutOfReach);
            var adjustment = Assert.IsType<SwordAiReachAdjustment>(reach.ReachAdjustment);
            Assert.Equal(halved, adjustment.Halved); Assert.Equal(2, adjustment.Before); Assert.Equal(halved ? 1 : 2, reach.EffectiveReach);
            Assert.Equal(halved, reach.AttackUsedDistanceFallback);
            Assert.Equal(o.MovementScenario.Scenario.AttackDestination, o.MovementScenario.Scenario.BaselineDestination);
            var afterRefresh = new FftBiosRandom().Next(rng).State;
            var afterCell = new FftBiosRandom().Next(afterRefresh).State;
            Assert.Equal(afterCell, o.MovementScenario.Movement[0].RngState);
            var final = new FftBiosRandom().Next(afterCell); Assert.Equal(final.State, result.RngState); Assert.Equal(final.Value, adjustment.Raw);
        }
    }

    [Fact]
    public void Actual_knockback_changes_the_source_mask_and_Critical_preserves_absolute_scores()
    {
        var pushed = AfterAct(power: 1, entryWord: 3).Battle; var o = Success(Evaluate(pushed));
        Assert.Equal(new TilePosition(0, 1, 0), o.Preparation.FocusPosition);
        Assert.Contains(new TilePosition(1, 1, 0), o.MovementScenario.Scenario.MovementOrigins);
        Assert.DoesNotContain(new TilePosition(0, 1, 0), o.MovementScenario.Scenario.MovementOrigins);
        Assert.Equal(5, o.MovementScenario.Movement.Length);
        var critical = Success(Evaluate(AfterAct(power: 8).Battle));
        Assert.All(critical.MovementScenario.Movement, cell => Assert.Equal(135, cell.Outcome.Score.Priority));
    }

    [Fact]
    public void Exact_owned_budgets_and_late_clock_or_reach_failure_hide_all_prefix_work()
    {
        var state = AfterAct().Battle; var last = new FftRandomState(state.TurnBattle.Turns.RngState.Value, ulong.MaxValue - 5);
        Assert.Equal(ulong.MaxValue, Evaluate(state, rng: last).RngState.DrawCount);
        var shortBudget = last with { DrawCount = ulong.MaxValue - 4 };
        var failed = Evaluate(state, rng: shortBudget); Atomic(failed, SwordAiPostActScenarioRejection.ScenarioRejected, shortBudget);
        Assert.Equal(SwordAiMovementScenarioStage.Movement, failed.Failure!.ScenarioFailure!.Stage); Assert.Equal(3, failed.Failure.ScenarioFailure.Index);
        var far = State(moved: false, acted: true, move: 1, distance: 3);
        var beforeAdjustment = last with { DrawCount = ulong.MaxValue - 2 };
        failed = Evaluate(far, outOfReach: true, rng: beforeAdjustment); Atomic(failed, SwordAiPostActScenarioRejection.ScenarioRejected, beforeAdjustment);
        Assert.Equal(SwordAiMovementScenarioStage.ReachAdjustment, failed.Failure!.ScenarioFailure!.Stage);
        var lateClock = State(moved: false, acted: true, move: 1, targetCt: 0, ticks: ulong.MaxValue - 13);
        var before = Snapshot(lateClock); failed = Evaluate(lateClock);
        Atomic(failed, SwordAiPostActScenarioRejection.ScenarioRejected, LiveRng);
        Assert.Equal(SwordAiMovementBaselineRejection.TickCountExhausted, failed.Failure!.ScenarioFailure!.MovementReason);
        Assert.Equal(before, Snapshot(lateClock));
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actors_and_orders_replay_and_wrong_branch_or_certificates_restore_input_RNG()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = AfterAct(second: second).Battle; var rng = state.TurnBattle.Turns.RngState; var before = Snapshot(state);
            var result = Evaluate(state, second: second); Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(AfterAct(second: second, reverse: true).Battle, second: second)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, second: second)));
            Atomic(Evaluate(state, outOfReach: true, second: second), SwordAiPostActScenarioRejection.ScenarioRejected, rng);
            Atomic(Evaluate(state, second: second, profile: Profile with { CanonicalCoordinates = false }), SwordAiPostActScenarioRejection.UnsupportedProfile, rng);
            Assert.Equal(before, Snapshot(state));
        }
        var pre = Ready(move: false).Battle;
        Atomic(Evaluate(pre), SwordAiPostActScenarioRejection.PreparationRejected, pre.TurnBattle.Turns.RngState);
        var far = State(moved: false, acted: true, move: 0, distance: 3);
        var fallback = Success(Evaluate(far, outOfReach: true));
        Assert.Equal(new TilePosition(1, 1, 0), Assert.Single(fallback.MovementScenario.Scenario.MovementOrigins));
    }

    internal static SwordAiPostActScenarioResult Evaluate(AttackBattleSnapshot state, bool outOfReach = false,
        bool second = false, SwordAiPostActScenarioProfile? profile = null, FftRandomState? rng = null) => outOfReach
        ? SwordAiPostActScenarios.EvaluateOutOfReach(state, Request(second), Candidate, Score, Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, profile ?? Profile, rng ?? state.TurnBattle.Turns.RngState)
        : SwordAiPostActScenarios.EvaluateInReach(state, Request(second), Candidate, Score, Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, profile ?? Profile, rng ?? state.TurnBattle.Turns.RngState);
    private static SwordAiPostActScenarioOutcome Success(SwordAiPostActScenarioResult result)
    { Assert.True(result.Accepted, Serialize(result)); Assert.Null(result.Failure); return Assert.IsType<SwordAiPostActScenarioOutcome>(result.Outcome); }
    private static void Atomic(SwordAiPostActScenarioResult result, SwordAiPostActScenarioRejection reason, FftRandomState rng)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Failure!.Reason); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState); }
}
