using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiOutOfReachMoveFirstTests
{
    private static readonly AttackThrowStoneAiPreparedSelectionProfile Selection = new(true, true, true, true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);
    private static readonly TilePosition Target = new(6, 1, 0), Right = new(2, 1, 0);

    [Fact]
    public void Known_cast_scores_preserve_full_masks_and_literal_RNG_through_real_Move_Stone_and_completion()
    {
        foreach (var (move, destination, decisionWord, decisionCount, actualWord, actualCount, damage, push) in new[] {
            (1, Right, 3210001534u, 16ul, 3297241969u, 23ul, 5, true),
            (3, new TilePosition(3, 2, 0), 595337866u, 20ul, 3823029677u, 27ul, 10, false) })
        {
            var state = Ready(move: move, target: Target); string before = Snapshot(state.Battle); var moved = Start(state); var o = Success(moved);
            var scenario = o.Decision.MovementScenario;
            Assert.Contains(Origin, scenario.Scenario1); Assert.Contains(Origin, scenario.Scenario2);
            Assert.Equal(new AiMovementCellProvenance(Origin, 5, null), scenario.Cells.Single(c => c.Tile == Origin));
            Assert.DoesNotContain(Origin, o.Decision.ScanOrigins); Assert.DoesNotContain(scenario.Movement, m => m.Outcome.Destination == Origin);
            Assert.Equal(move == 1 ? 1 : 5, scenario.Movement.Length);
            Assert.Equal(destination, o.Move.Destination); Assert.Equal(new FftRandomState(decisionWord, decisionCount), o.DecisionRng);
            Assert.Equal(o.DecisionRng, moved.State.Battle.TurnBattle.Turns.RngState); Assert.Equal(o.DecisionRng, moved.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(destination, moved.Path!.Tiles[^1]); Assert.Equal(Origin, moved.Path.Tiles[0]);
            Assert.Equal(moved.Path.FinalHeading, moved.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
            Assert.Empty(moved.Events.OfType<ThrowStoneResolvedEvent>()); Assert.Equal(state.Revision + 1, moved.State.Revision);
            var p = Profile(); var pure = AttackThrowStoneAiOutOfReachSelection.Evaluate(state.Battle, FreshRequest(), Stone, new(Sword, 1),
                p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), Selection, SwordAiScenarioTeam.NonBlue,
                state.Battle.TurnBattle.Turns.RngState);
            Assert.True(pure.Accepted); Assert.Equal(Serialize(pure.Outcome), Serialize(o.Decision)); Assert.Equal(pure.RngState, o.DecisionRng);
            var act = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(act.Accepted);
            Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, damage, push), Assert.Single(act.Events.OfType<ThrowStoneResolvedEvent>()));
            Assert.Equal(50 - damage, act.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(push ? new TilePosition(7, 1, 0) : Target, act.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == B).Position);
            Assert.Equal(new FftRandomState(actualWord, actualCount), act.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(state.Revision + 2, act.State.Revision); Assert.Same(act.State.Battle, act.State.PendingFacing!.ExpectedBattle);
            var faced = FacingTurnBattle.FinishFacing(act.State, A, act.State.PendingFacing, new FacingChoice.ChooseClosedEnemy(B));
            Assert.True(faced.Accepted); Assert.Equal(act.State.Battle.TurnBattle, faced.State.Battle.TurnBattle);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Post_score_halving_and_no_cast_or_zero_accuracy_fallbacks_retain_exact_decision_draws()
    {
        var half = Start(Ready(move: 1, target: Target, entryWord: 20)); var h = Success(half);
        Assert.Equal(new SwordAiReachAdjustment(12502, 0, 2, 1), h.Decision.MovementScenario.ReachAdjustment);
        Assert.True(h.Decision.MovementScenario.DestinationUsedDistanceFallback); Assert.Equal(Right, h.Move.Destination);
        Assert.Equal(new FftRandomState(143664818, 9), Assert.Single(h.Decision.MovementScenario.Movement).RngState);
        Assert.Equal(new FftRandomState(4007910005, 16), h.DecisionRng);
        var act = FacingMoveDecisionBattle.Continue(half.State, A, half.Continuation); Assert.True(act.Accepted);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, 10, false), Assert.Single(act.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(new FftRandomState(3660420676, 23), act.State.Battle.TurnBattle.Turns.RngState);
        foreach (var (target, evade, word, count) in new[] { (new TilePosition(7,1,0), 0, 180171308u, 18ul), (Target, 100, 2111915288u, 22ul) })
        {
            var moved = Start(Ready(move: 1, target: target, evade: evade)); var o = Success(moved);
            Assert.Equal(AttackThrowStoneAiPlanKind.MoveOnly, o.Decision.Selection.Plan.Kind); Assert.Equal(Right, o.Move.Destination);
            Assert.Equal(8, o.Decision.Selection.Lottery.Length); Assert.Equal(new FftRandomState(word, count), o.DecisionRng);
            var ended = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(ended.Accepted);
            Assert.Equal(o.DecisionRng, ended.State.Battle.TurnBattle.Turns.RngState); Assert.Null(ended.State.PendingFacing);
            Assert.Equal(35, ended.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Empty(ended.Events.OfType<ThrowStoneResolvedEvent>());
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Range_four_unknown_keys_ACT_first_same_tile_and_wrong_source_branch_admit_no_Move_or_adoption()
    {
        foreach (int distance in new[] { 3, 4 })
        {
            var state = Ready(move: 1, target: new(distance + 1, 1, 0)); string before = Snapshot(state.Battle);
            var failure = Atomic(state, Start(state), AttackThrowStoneAiMoveFirstRejection.SelectionRejected);
            Assert.Equal(AttackThrowStoneAiPreparedSelectionStage.Provenance, failure.SelectionFailure!.Stage);
            Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnwrittenCastPriority, failure.SelectionFailure.Reason);
            Assert.Equal(before, Snapshot(state.Battle));
        }
        foreach (var target in new[] { new TilePosition(4, 1, 0), Target })
        { var zero = Ready(move: 0, target: target); Atomic(zero, Start(zero), AttackThrowStoneAiMoveFirstRejection.MoveFirstRequired); }
        var inReach = Ready(); Assert.Equal(AttackThrowStoneAiScenarioRejection.InReach,
            Atomic(inReach, Start(inReach), AttackThrowStoneAiMoveFirstRejection.SelectionRejected).SelectionFailure!.ScenarioFailure!.Reason);
        var far = Ready(move: 1, target: Target);
        Atomic(far, Start(far, dispatch: Dispatch with { OrdinaryContinuation = false }), AttackThrowStoneAiMoveFirstRejection.UnsupportedDispatchContext);
        Atomic(far, Start(far, selection: Selection with { ClearDirectTrajectories = false }), AttackThrowStoneAiMoveFirstRejection.SelectionRejected);
        Atomic(far, Start(far, team: SwordAiScenarioTeam.Blue), AttackThrowStoneAiMoveFirstRejection.SelectionRejected);
    }

    [Fact]
    public void Exact_full_budgets_and_early_or_late_failure_distinguish_unpublished_selection_from_committed_Move()
    {
        foreach (var (move, target, evade, seed, total) in new[] {
            (1, Target, 0, 1u, 16ul), (3, Target, 0, 1u, 20ul), (1, Target, 0, 20u, 16ul),
            (1, new TilePosition(7,1,0), 0, 1u, 11ul), (1, Target, 100, 1u, 15ul) })
        {
            var moved = Start(Ready(move: move, target: target, evade: evade, entryWord: seed, count: ulong.MaxValue - total)); Success(moved);
            var done = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(done.Accepted);
            Assert.Equal(ulong.MaxValue, done.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        }
        foreach (ulong remaining in new[] { 0ul, 2ul, 8ul })
        {
            var state = Ready(move: 1, target: Target, count: ulong.MaxValue - remaining);
            var failure = Atomic(state, Start(state), AttackThrowStoneAiMoveFirstRejection.SelectionRejected);
            if (remaining == 2) Assert.Equal(AttackThrowStoneAiScenarioStage.ReachAdjustment, failure.SelectionFailure!.ScenarioFailure!.Stage);
            if (remaining == 8) Assert.Equal(AttackThrowStoneAiPreparedSelectionStage.Selection, failure.SelectionFailure!.Stage);
        }
        var late = Start(Ready(move: 1, target: Target, count: ulong.MaxValue - 15)); var o = Success(late); string before = Snapshot(late.State.Battle);
        Assert.Equal(ulong.MaxValue - 6, o.DecisionRng.DrawCount);
        foreach (int attempt in new[] { 1, 2 })
        {
            var failed = FacingMoveDecisionBattle.Continue(late.State, A, late.Continuation);
            Assert.False(failed.Accepted); Assert.Same(late.State, failed.State); Assert.Empty(failed.Events);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Transition.AttackRejection);
        }
        Assert.Equal(before, Snapshot(late.State.Battle)); Assert.Equal(Right, late.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
        var cancelled = FacingTurnBattle.WaitWithFacing(late.State, A, new FacingChoice.ChooseClosedEnemy(B)); Assert.True(cancelled.Accepted);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(cancelled.State, A, late.Continuation).Rejection);
    }

    [Fact]
    public void Real_KO_settles_damage_and_no_push_before_receipt_and_fixed_facing()
    {
        var moved = Start(Ready(move: 1, target: Target, hp: 5)); var o = Success(moved);
        Assert.Equal(new FftRandomState(267834847, 17), o.DecisionRng);
        var act = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(act.Accepted);
        Assert.Equal(new ThrowStoneResolvedEvent(A, Stone, B, true, 10, false), Assert.Single(act.Events.OfType<ThrowStoneResolvedEvent>()));
        Assert.Equal(AttackUnitStatus.Dead, act.State.Battle.Combatants.Single(u => u.UnitId == B).Status);
        Assert.Equal(Target, act.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == B).Position);
        Assert.Equal(new FftRandomState(3791773014, 24), act.State.Battle.TurnBattle.Turns.RngState);
        Assert.Same(act.State.Battle, act.State.PendingFacing!.ExpectedBattle);
        Assert.True(FacingTurnBattle.FinishFacing(act.State, A, act.State.PendingFacing, new FacingChoice.Fixed(BattleFacing.East)).Accepted);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_policy_order_replay_with_exact_session_tokens()
    {
        foreach (bool second in new[] { false, true })
        {
            var actor = second ? B : A; var state = Ready(move: 1, target: Target, second: second); string before = Snapshot(state.Battle);
            var request = FreshRequest(second); var first = Start(state, request: request); Success(first);
            var replay = Start(state, request: request); Success(replay);
            var reversed = Start(Ready(move: 1, target: Target, second: second, reverse: true),
                request: request with { Policies = request.Policies.Reverse().ToImmutableArray() }); Success(reversed);
            Assert.Equal(Serialize(first.Outcome), Serialize(reversed.Outcome)); Assert.Equal(Snapshot(first.State.Battle), Snapshot(reversed.State.Battle));
            Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(replay.State, actor, first.Continuation).Rejection);
            var actual = FacingMoveDecisionBattle.Continue(first.State, actor, first.Continuation); Assert.True(actual.Accepted);
            Assert.Equal(Snapshot(actual.State.Battle), Snapshot(FacingMoveDecisionBattle.Continue(reversed.State, actor, reversed.Continuation).State.Battle));
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static AttackThrowStoneAiOutOfReachMoveFirstResult Start(FacingTurnState state, SwordAiRefreshRequest? request = null,
        SwordAiCommandDispatchProfile? dispatch = null, AttackThrowStoneAiPreparedSelectionProfile? selection = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue)
    {
        var p = Profile(); return AttackThrowStoneAiMoveFirstExecution.StartOutOfReach(state, request ?? FreshRequest(), Stone, new(Sword, 1),
            p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), selection ?? Selection, team, dispatch ?? Dispatch);
    }
    private static AttackThrowStoneAiOutOfReachMoveFirstOutcome Success(AttackThrowStoneAiOutOfReachMoveFirstResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); Assert.NotNull(result.Continuation);
        return Assert.IsType<AttackThrowStoneAiOutOfReachMoveFirstOutcome>(result.Outcome); }
    private static AttackThrowStoneAiMoveFirstFailure Atomic(FacingTurnState state, AttackThrowStoneAiOutOfReachMoveFirstResult result, AttackThrowStoneAiMoveFirstRejection reason)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Path);
        Assert.Null(result.Continuation); Assert.Null(result.Outcome); Assert.Equal(reason, result.Failure!.Reason); return result.Failure; }
}
