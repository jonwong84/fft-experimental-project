using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActPreparationTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiPostActScenarioTests
{
    [Fact]
    public void Actual_hit_miss_and_push_use_post_ACT_refunds_and_literal_fresh_scenario_streams()
    {
        // Words are internal RNG checkpoints, counts are cumulative. Scenario cost is one refresh + one draw per cell,
        // plus one reach-adjustment draw only for out-of-reach. No action formula or lottery is evaluated here.
        var cases = new[] {
            new { Name = "Attack hit, Move3", Move = 3, Power = 2, Evade = 0, Seed = 1u, Outside = false,
                Cells = 14, Destination = new TilePosition(1, 4, 0), Score = 26, Rng = new FftRandomState(2240736910, 64) },
            new { Name = "Stone hit, Move0", Move = 0, Power = 1, Evade = 0, Seed = 1u, Outside = false,
                Cells = 1, Destination = Origin, Score = 26, Rng = new FftRandomState(2473755763, 29) },
            new { Name = "Stone miss, Move0", Move = 0, Power = 1, Evade = 75, Seed = 1u, Outside = false,
                Cells = 1, Destination = Origin, Score = 0, Rng = new FftRandomState(1969681615, 33) },
            new { Name = "Stone push, Move0", Move = 0, Power = 1, Evade = 0, Seed = 20u, Outside = true,
                Cells = 1, Destination = Origin, Score = 26, Rng = new FftRandomState(2438815311, 30) } };
        foreach (var example in cases)
        {
            var state = Act(Ready(move: example.Move, evade: example.Evade, entryWord: example.Seed), example.Power).Battle;
            string before = Snapshot(state);
            var result = Evaluate(state, example.Outside, weaponPower: example.Power); var o = Success(result);
            Assert.Equal(example.Rng, result.RngState); Assert.Equal(example.Cells, o.Movement.Length);
            Assert.Equal(example.Destination, o.Destination); Assert.Equal(o.D0, o.D1);
            Assert.Equal(o.Destination, o.Movement[o.BaselineIndex].Outcome.Destination);
            Assert.All(o.Movement, m => {
                Assert.Equal(example.Score, m.Outcome.Score.Priority);
                Assert.Equal(m.Outcome.Destination == Origin ? 35 : 15, m.Outcome.TurnEndCt);
                Assert.Single(m.Outcome.UpkeepPicks);
                Assert.True(m.Outcome.Units.Single(u => u.UnitId == A).ActionTaken);
            });
            Assert.All(o.Preparation.ActorEntries, e => Assert.False(e.Usable));
            if (!example.Outside) { Assert.Null(o.ReachAdjustment); Assert.Empty(o.Scenario2); Assert.All(o.Cells, c => Assert.True(c.PriorityWritten)); }
            else { Assert.Equal(new SwordAiReachAdjustment(4445, 0, 1, 0), o.ReachAdjustment); Assert.True(o.FilterUsedReachableFallback); }
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Out_of_reach_preserves_unwritten_actor_overlap_and_adjusts_reach_after_the_fresh_scores()
    {
        var state = Pushed(move: 3, originalTargetX: 5); string before = Snapshot(state);
        var result = Evaluate(state, outside: true); var o = Success(result);
        Assert.Equal(5, o.Movement.Length); Assert.Equal(17, o.Cells.Length);
        Assert.Equal(new TilePosition(3, 2, 0), o.Destination); Assert.Equal(o.D0, o.D1);
        Assert.Equal(new FftRandomState(2000392299, 34), result.RngState);
        Assert.Equal(new SwordAiReachAdjustment(30523, 3, 4, 4), o.ReachAdjustment);
        Assert.Equal(new AiMovementCellProvenance(Origin, 5, null), o.Cells.Single(c => c.Tile == Origin));
        Assert.Contains(Origin, o.Scenario1); Assert.Contains(Origin, o.Scenario2); Assert.DoesNotContain(Origin, o.ScoreMask);
        Assert.Equal(before, Snapshot(state));

        // A supplied pure-query checkpoint makes the third draw 12502, divisible by7: refresh, one score, then halving.
        // It does not replace either live RNG copy. The actor is an unknown key inside Stone range, but no cast reads it.
        var nearer = Pushed(move: 1, originalTargetX: 4); var entry = new FftRandomState(20, 7);
        var halved = Success(Evaluate(nearer, outside: true, rng: entry));
        Assert.Equal(new SwordAiReachAdjustment(12502, 0, 2, 1), halved.ReachAdjustment);
        Assert.Equal(new TilePosition(2, 1, 0), halved.Destination); Assert.True(halved.DestinationUsedDistanceFallback);
        Assert.Null(halved.Cells.Single(c => c.Tile == Origin).WrittenPriority); Assert.Equal(4, halved.TargetDistance);
        Assert.Equal(new FftRandomState(2966873603, 10), Evaluate(nearer, outside: true, rng: entry).RngState);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Branch_source_choice_and_terminal_guards_hide_all_partial_preparation()
    {
        var inside = Act(Ready()).Battle; var outside = Pushed(move: 1, originalTargetX: 4);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.InReach, Atomic(inside, Evaluate(inside, true)).Reason);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.OutOfReach, Atomic(outside, Evaluate(outside, false)).Reason);
        var config = Configuration();
        foreach (var selection in new[] { config.Selection with { NormalScoring = false }, config.Selection with { CanonicalCoordinates = false },
            config.Selection with { NoOtherScenarioOverrides = false } })
            Assert.Equal(AttackThrowStoneAiScenarioStage.Admission,
                Atomic(inside, Evaluate(inside, configuration: config with { Selection = selection })).Stage);
        Assert.Equal(SwordAiRefreshRejection.UnavailableAction,
            Atomic(Battle(), Evaluate(Battle())).PreparationFailure!.RefreshRejection);
        var terminal = Act(Ready(move: 0, hp: 10), power: 1).Battle;
        Assert.Equal(SwordAiRefreshRejection.TerminalInput,
            Atomic(terminal, Evaluate(terminal, weaponPower: 1)).PreparationFailure!.RefreshRejection);
    }

    [Fact]
    public void Exact_and_short_draws_and_late_movement_tick_failure_restore_the_whole_query()
    {
        var cases = new[] {
            new { State = Act(Ready()).Battle, Outside = false, Cost = 15ul },
            new { State = Act(Ready(move: 0), power: 1).Battle, Outside = false, Cost = 2ul },
            new { State = Pushed(move: 1, originalTargetX: 4), Outside = true, Cost = 3ul },
            new { State = Pushed(move: 3, originalTargetX: 5), Outside = true, Cost = 7ul } };
        foreach (var example in cases)
        {
            string before = Snapshot(example.State);
            var exact = example.State.TurnBattle.Turns.RngState with { DrawCount = ulong.MaxValue - example.Cost };
            var fits = Evaluate(example.State, example.Outside, rng: exact); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
            var shortRng = exact with { DrawCount = exact.DrawCount + 1 };
            var failure = Atomic(example.State, Evaluate(example.State, example.Outside, rng: shortRng), shortRng);
            Assert.Equal(example.Outside ? AttackThrowStoneAiScenarioStage.ReachAdjustment : AttackThrowStoneAiScenarioStage.Movement, failure.Stage);
            Assert.Equal(before, Snapshot(example.State));
        }
        // Refresh can finish after13 ticks, while a changed-tile score needs17. Its late failure must discard the refresh too.
        var late = Battle(acted: true, targetCt: 0, ticks: ulong.MaxValue - 13);
        var failed = Atomic(late, Evaluate(late));
        Assert.Equal(AttackThrowStoneAiScenarioStage.Movement, failed.Stage); Assert.Equal(0, failed.MovementIndex);
        Assert.Equal(SwordAiMovementBaselineRejection.TickCountExhausted, failed.MovementReason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actors_and_reordered_state_and_policy_collections_replay_fresh_scenarios()
    {
        foreach (bool second in new[] { false, true })
        {
            var turn = FreshRequest(second); var state = Act(Ready(second: second), request: turn).Battle;
            var request = new AttackThrowStoneAiSelectionRequest(turn, Stone, new(Sword, 2)); string before = Snapshot(state);
            var first = Evaluate(state, request: request); Success(first);
            var replay = Evaluate(state, request: request); Success(replay);
            var reversedTurn = turn with { Policies = turn.Policies.Reverse().ToImmutableArray() };
            var reversed = Act(Ready(second: second, reverse: true), request: reversedTurn).Battle;
            var reordered = Evaluate(reversed, request: request with { Turn = reversedTurn }); Success(reordered);
            Assert.Equal(Serialize(first), Serialize(replay)); Assert.Equal(Serialize(first), Serialize(reordered));
            Assert.Equal(before, Snapshot(state));
        }
    }

    internal static AttackThrowStoneAiSelectionConfiguration Configuration() => SelectionConfiguration(new(true, true, true));
    internal static AttackBattleSnapshot Pushed(int move, int originalTargetX, bool second = false, bool reverse = false)
    {
        var result = FacingTurnBattle.UseTargetedAbility(Ready(move: move, target: new(originalTargetX, 1, 0), entryWord: 2711922929, count: 20,
            second: second, reverse: reverse), new(second ? B : A, Stone, second ? A : B), Profile().ThrowStone.Ability);
        Assert.True(result.Accepted); Assert.Single(result.Events.OfType<ThrowStoneKnockbackEvent>()); return result.State.Battle;
    }
    internal static AttackThrowStoneAiPostActScenarioResult Evaluate(AttackBattleSnapshot state, bool outside = false, int weaponPower = 2,
        AttackThrowStoneAiSelectionRequest? request = null, AttackThrowStoneAiSelectionConfiguration? configuration = null, FftRandomState? rng = null) =>
        outside ? AttackThrowStoneAiPostActScenarios.EvaluateOutOfReach(state, request ?? new(FreshRequest(), Stone, new(Sword, weaponPower)),
            configuration ?? Configuration(), rng ?? state.TurnBattle.Turns.RngState) :
            AttackThrowStoneAiPostActScenarios.EvaluateInReach(state, request ?? new(FreshRequest(), Stone, new(Sword, weaponPower)),
                configuration ?? Configuration(), rng ?? state.TurnBattle.Turns.RngState);
    private static AttackThrowStoneAiPostActScenarioOutcome Success(AttackThrowStoneAiPostActScenarioResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiPostActScenarioOutcome>(result.Outcome); }
    private static AttackThrowStoneAiScenarioFailure Atomic(AttackBattleSnapshot state, AttackThrowStoneAiPostActScenarioResult result, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? state.TurnBattle.Turns.RngState, result.RngState); return Assert.IsType<AttackThrowStoneAiScenarioFailure>(result.Failure); }
}
