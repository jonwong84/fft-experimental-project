using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-171/175.
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPreparedMoveFirstOutOfReachTests
{
    private static readonly SwordAiPreparedSelectionProfile Selection = new(true, true, true, true, true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Owned_out_of_reach_stream_moves_then_Waits_preserving_adjustment_and_distance_fallback()
    {
        foreach (var (seed, raw, reach, word) in new[] { (1u, 10113u, 2, 180171308u), (20u, 12502u, 1, 3368358523u) })
        {
            var state = Ready(move: false, origin: new(4, 1, 0), entryWord: seed); var before = Snapshot(state.Battle);
            var moved = Start(state); var o = Success(moved);
            var prep = Assert.IsType<SwordAiPreparationOutcome>(o.Preparation);
            var detail = Assert.IsType<SwordAiOutOfReachSelectionOutcome>(o.OutOfReach);
            Assert.Equal(new FftRandomState(word, 18), o.DecisionRng);
            Assert.Equal(55, prep.Refresh.TurnEndCt); Assert.Single(prep.Refresh.UpkeepPicks);
            Assert.Equal(4, prep.ReachableOrigins.Length); Assert.Single(o.Decision.MovementScenario.Movement);
            Assert.Equal(raw, detail.MovementScenario.ReachAdjustment!.Raw);
            Assert.Equal(reach, detail.MovementScenario.EffectiveReach);
            Assert.Equal(reach == 1, detail.MovementScenario.AttackUsedDistanceFallback);
            Assert.Empty(detail.Selection.Composition.Scan.Outcome!.Candidates); Assert.Equal(8, detail.Selection.Lottery.Length);
            Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, detail.Selection.Plan.Kind);
            Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, detail.Selection.Plan.WaitFacingHint);
            var pure = SwordAiPreparedSelection.EvaluateOutOfReachUnmoved(state.Battle, Request(), new(Weapon, 5), Candidate, Score,
                Refresh, Preparation, SwordAiScenarioTeam.NonBlue, Selection, state.Battle.TurnBattle.Turns.RngState);
            Assert.True(pure.Accepted); Assert.Equal(pure.RngState, o.DecisionRng);
            Assert.Equal(Serialize(pure.Outcome!.Preparation), Serialize(prep)); Assert.Equal(Serialize(pure.Outcome.OutOfReach), Serialize(detail));
            Assert.Equal(new TilePosition(3, 1, 0), moved.Path!.Tiles[^1]); Assert.Equal(BattleFacing.West, moved.Path.FinalHeading);
            Assert.Equal(state.Revision + 1, moved.State.Revision); Assert.Null(moved.State.PendingFacing);
            Assert.Equal(15, moved.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            var next = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(next.Accepted);
            Assert.Equal(moved.State.Revision + 1, next.State.Revision); Assert.Null(next.State.PendingFacing);
            Assert.Equal(o.DecisionRng, next.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(o.DecisionRng, next.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(35, next.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(BattleFacing.West, next.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
            Assert.Empty(moved.Events.Concat(next.Events).OfType<AttackResolvedEvent>());
            Assert.All(next.State.Battle.Combatants, u => Assert.Equal(50, u.Hp));
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, next.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Eleven_draw_budget_finishes_at_MAX_while_any_incomplete_prefix_publishes_no_Move()
    {
        var fits = Start(Ready(move: false, origin: new(4, 1, 0), count: ulong.MaxValue - 11)); var o = Success(fits);
        Assert.Equal(ulong.MaxValue, o.DecisionRng.DrawCount);
        var next = FacingMoveDecisionBattle.Continue(fits.State, A, fits.Continuation); Assert.True(next.Accepted);
        Assert.Equal(o.DecisionRng, next.State.Battle.TurnBattle.Turns.RngState);
        foreach (int available in new[] { 0, 1, 2, 3, 10 })
        {
            var state = Ready(move: false, origin: new(4, 1, 0), count: ulong.MaxValue - (ulong)available);
            var failure = Atomic(state, Start(state), SwordAiMoveFirstRejection.SelectionRejected).PreparedSelectionFailure!;
            Assert.Equal(available == 0 ? SwordAiPreparedSelectionStage.Preparation : SwordAiPreparedSelectionStage.Selection, failure.Stage);
            if (available > 0) Assert.Equal(available <= 2 ? SwordAiDerivedSelectionStage.MovementScenario : SwordAiDerivedSelectionStage.Lottery,
                failure.SelectionStage);
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Wrong_explicit_branch_team_or_remaining_context_has_no_partial_preparation_or_continuation()
    {
        var far = Ready(move: false, origin: new(4, 1, 0));
        foreach (var team in new[] { SwordAiScenarioTeam.Blue, SwordAiScenarioTeam.Unknown })
            Atomic(far, Start(far, team: team), SwordAiMoveFirstRejection.SelectionRejected);
        Atomic(far, Start(far, dispatch: new()), SwordAiMoveFirstRejection.DispatchRejected);
        Atomic(far, Start(far, selection: Selection with { NoDefendSupport = false }), SwordAiMoveFirstRejection.SelectionRejected);
        Atomic(far, Start(far, refresh: Refresh with { FreshRefreshEntry = false }), SwordAiMoveFirstRejection.SelectionRejected);
        var near = Ready(move: false); Atomic(near, Start(near), SwordAiMoveFirstRejection.SelectionRejected);
        Atomic(far, SwordAiMoveFirstExecution.StartPreparedInReach(far, Request(), new(Weapon, 5), Candidate, Score,
            Refresh, Preparation, SwordAiScenarioTeam.NonBlue, Selection, Dispatch), SwordAiMoveFirstRejection.SelectionRejected);
        var used = Ready(outOfReach: true); Atomic(used, Start(used), SwordAiMoveFirstRejection.BoundaryRejected);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Identity_order_replay_and_continuation_rejection_preserve_Move_and_do_not_double_refund_Wait()
    {
        foreach (bool second in new[] { false, true })
        {
            var actor = second ? B : A; var target = second ? A : B;
            var state = Ready(move: false, origin: new(4, 1, 0), second: second); var before = Snapshot(state.Battle);
            var moved = Start(state, request: Request(second)); var replay = Start(state, request: Request(second));
            var reversed = Start(Ready(move: false, origin: new(4, 1, 0), second: second, reverse: true), request: Request(second, reverse: true));
            Success(moved); Success(replay); Success(reversed);
            Assert.Equal(Serialize(moved.Outcome), Serialize(reversed.Outcome)); Assert.Equal(Snapshot(moved.State.Battle), Snapshot(reversed.State.Battle));
            var wrong = FacingMoveDecisionBattle.Continue(moved.State, target, moved.Continuation);
            Assert.Equal(FacingMoveRejection.WrongActor, wrong.Rejection); Assert.Same(moved.State, wrong.State); Assert.Empty(wrong.Events);
            var cross = FacingMoveDecisionBattle.Continue(replay.State, actor, moved.Continuation);
            Assert.Equal(FacingMoveRejection.StaleContinuation, cross.Rejection); Assert.Same(replay.State, cross.State); Assert.Empty(cross.Events);
            var next = FacingMoveDecisionBattle.Continue(moved.State, actor, moved.Continuation); Assert.True(next.Accepted);
            Assert.Equal(Snapshot(next.State.Battle), Snapshot(FacingMoveDecisionBattle.Continue(moved.State, actor, moved.Continuation).State.Battle));
            Assert.Equal(35, next.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actor).Ct);
            var stale = FacingMoveDecisionBattle.Continue(next.State, actor, moved.Continuation);
            Assert.Equal(FacingMoveRejection.StaleContinuation, stale.Rejection); Assert.Same(next.State, stale.State); Assert.Empty(stale.Events);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static SwordAiMoveFirstResult Start(FacingTurnState state, SwordAiRefreshRequest? request = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, SwordAiPreparedSelectionProfile? selection = null,
        SwordAiCommandDispatchProfile? dispatch = null, SwordAiRefreshProfile? refresh = null) =>
        SwordAiMoveFirstExecution.StartPreparedOutOfReach(state, request ?? Request(), new(Weapon, 5), Candidate, Score,
            refresh ?? Refresh, Preparation, team, selection ?? Selection, dispatch ?? Dispatch);
    private static SwordAiMoveFirstOutcome Success(SwordAiMoveFirstResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); Assert.NotNull(result.Continuation); return Assert.IsType<SwordAiMoveFirstOutcome>(result.Outcome); }
    private static SwordAiMoveFirstFailure Atomic(FacingTurnState state, SwordAiMoveFirstResult result, SwordAiMoveFirstRejection reason)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Equal(reason, result.Failure!.Reason); return result.Failure; }
}
