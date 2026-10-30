using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-171/174.
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPreparedMoveFirstTests
{
    private static readonly SwordAiPreparedSelectionProfile Selection = new(true, true, true, true, true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Owned_prefix_moves_then_executes_retained_hit_or_miss_without_refreshing_again()
    {
        foreach (var (evade, decisionWord, decisionCount, actualWord, actualCount, hit) in new[] {
            (0, 836760821u, 19ul, 3535774167u, 25ul, true), (89, 3794901700u, 26ul, 1343201072u, 30ul, false) })
        {
            var state = Ready(evade: evade, move: false); var before = Snapshot(state.Battle);
            var moved = Start(state); var o = Success(moved); var prep = Assert.IsType<SwordAiPreparationOutcome>(o.Preparation);
            Assert.Equal(new FftRandomState(decisionWord, decisionCount), o.DecisionRng);
            Assert.Equal(16838u, Assert.Single(prep.Refresh.UpkeepPicks).RawRoll); Assert.Equal(55, prep.Refresh.TurnEndCt);
            Assert.Equal(60, prep.Refresh.Units.Single(u => u.UnitId == A).Ct);
            Assert.Equal(5, prep.ReachableOrigins.Length); Assert.Equal(5, o.Decision.MovementScenario.Movement.Length);
            var pure = SwordAiPreparedSelection.EvaluateUnmoved(state.Battle, Request(), new(Weapon, 5), Candidate, Score,
                Refresh, Preparation, SwordAiScenarioTeam.NonBlue, Selection, state.Battle.TurnBattle.Turns.RngState);
            Assert.True(pure.Accepted); Assert.Equal(pure.RngState, o.DecisionRng);
            Assert.Equal(Serialize(pure.Outcome!.Preparation), Serialize(prep)); Assert.Equal(Serialize(pure.Outcome.Decision), Serialize(o.Decision));
            Assert.Equal(new TilePosition(2, 1, 0), moved.Path!.Tiles[^1]); Assert.Equal(BattleFacing.West, moved.Path.FinalHeading);
            Assert.Equal(state.Revision + 1, moved.State.Revision); Assert.Null(moved.State.PendingFacing);
            Assert.Equal(15, moved.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
            Assert.Equal(o.DecisionRng, moved.State.Battle.TurnBattle.Battle.RngState);
            var next = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(next.Accepted);
            Assert.Equal(new AttackResolvedEvent(A, B, hit, false, hit ? 25 : 0), Assert.Single(next.Events.OfType<AttackResolvedEvent>()));
            Assert.Equal(new FftRandomState(actualWord, actualCount), next.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(hit ? 25 : 50, next.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            Assert.Equal(moved.State.Revision + 1, next.State.Revision);
            Assert.True(FacingTurnBattle.FinishFacing(next.State, A, next.State.PendingFacing, new FacingChoice.ChooseClosedEnemy(B)).Accepted);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, next.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Prepared_empty_ranking_moves_then_Waits_at_MAX_with_live_CT_refund_and_deferred_facing()
    {
        var state = Ready(evade: 0, move: false, count: ulong.MaxValue - 19);
        var moved = Start(state, power: 0); var o = Success(moved);
        Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Decision.Selection.Plan.Kind);
        Assert.Equal(8, o.Decision.Selection.Lottery.Length); Assert.Equal(ulong.MaxValue, o.DecisionRng.DrawCount);
        var next = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(next.Accepted);
        Assert.Equal(o.DecisionRng, next.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(35, next.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(BattleFacing.West, next.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
        Assert.Null(next.State.PendingFacing); Assert.Empty(next.Events.OfType<AttackResolvedEvent>());
    }

    [Fact]
    public void Prefix_failure_rolls_back_but_actual_Attack_exhaustion_retains_Move_and_can_retry_or_Wait()
    {
        foreach (int available in new[] { 0, 1, 11 })
        {
            var state = Ready(evade: 0, move: false, count: ulong.MaxValue - (ulong)available);
            var failure = Atomic(state, Start(state), SwordAiMoveFirstRejection.SelectionRejected);
            Assert.Equal(available == 0 ? SwordAiPreparedSelectionStage.Preparation : SwordAiPreparedSelectionStage.Selection,
                failure.PreparedSelectionFailure!.Stage);
        }
        var fits = Start(Ready(evade: 0, move: false, count: ulong.MaxValue - 18)); Success(fits);
        var done = FacingMoveDecisionBattle.Continue(fits.State, A, fits.Continuation); Assert.True(done.Accepted);
        Assert.Equal(ulong.MaxValue, done.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        var moved = Start(Ready(evade: 0, move: false, count: ulong.MaxValue - 17)); Success(moved);
        var before = Snapshot(moved.State.Battle);
        var failed = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation);
        Assert.False(failed.Accepted); Assert.Same(moved.State, failed.State); Assert.Empty(failed.Events);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Transition.AttackRejection);
        Assert.Equal(before, Snapshot(moved.State.Battle));
        Assert.Equal(failed.Rejection, FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation).Rejection);
        var cancelled = FacingTurnBattle.WaitWithFacing(moved.State, A, new FacingChoice.ChooseClosedEnemy(B)); Assert.True(cancelled.Accepted);
        Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(cancelled.State, A, moved.Continuation).Rejection);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Prepared_admission_dispatch_and_non_Move_plans_publish_no_partial_prefix_or_authority()
    {
        var state = Ready(evade: 0, move: false);
        Atomic(state, Start(state, selection: Selection with { NoDefendSupport = false }), SwordAiMoveFirstRejection.SelectionRejected);
        Atomic(state, Start(state, preparation: Preparation with { OrdinaryAttackOnlyLists = false }), SwordAiMoveFirstRejection.SelectionRejected);
        Atomic(state, Start(state, team: SwordAiScenarioTeam.Blue), SwordAiMoveFirstRejection.SelectionRejected);
        Atomic(state, Start(state, dispatch: new()), SwordAiMoveFirstRejection.DispatchRejected);
        Atomic(state, Start(state, request: Request(second: true)), SwordAiMoveFirstRejection.BoundaryRejected);
        var used = Ready(); Atomic(used, Start(used), SwordAiMoveFirstRejection.BoundaryRejected);
        var adjacent = Ready(evade: 0, move: false, origin: new(2, 1, 0));
        Atomic(adjacent, Start(adjacent), SwordAiMoveFirstRejection.MoveFirstRequired);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_order_replay_while_cross_tokens_reject_without_undoing_the_Move()
    {
        foreach (bool second in new[] { false, true })
        {
            var actor = second ? B : A; var state = Ready(evade: 0, move: false, second: second); var before = Snapshot(state.Battle);
            var moved = Start(state, request: Request(second)); var replay = Start(state, request: Request(second));
            var reversed = Start(Ready(evade: 0, move: false, second: second, reverse: true), request: Request(second, reverse: true));
            Success(moved); Success(replay); Success(reversed);
            Assert.Equal(Serialize(moved.Outcome), Serialize(reversed.Outcome)); Assert.Equal(Snapshot(moved.State.Battle), Snapshot(reversed.State.Battle));
            var rejected = FacingMoveDecisionBattle.Continue(replay.State, actor, moved.Continuation);
            Assert.Equal(FacingMoveRejection.StaleContinuation, rejected.Rejection); Assert.Same(replay.State, rejected.State); Assert.Empty(rejected.Events);
            var next = FacingMoveDecisionBattle.Continue(moved.State, actor, moved.Continuation); Assert.True(next.Accepted);
            Assert.Equal(Snapshot(next.State.Battle), Snapshot(FacingMoveDecisionBattle.Continue(moved.State, actor, moved.Continuation).State.Battle));
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static SwordAiMoveFirstResult Start(FacingTurnState state, int power = 5, SwordAiRefreshRequest? request = null,
        SwordAiPreparationProfile? preparation = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue,
        SwordAiPreparedSelectionProfile? selection = null, SwordAiCommandDispatchProfile? dispatch = null) =>
        SwordAiMoveFirstExecution.StartPreparedInReach(state, request ?? Request(), new(Weapon, power), Candidate, Score,
            Refresh, preparation ?? Preparation, team, selection ?? Selection, dispatch ?? Dispatch);
    private static SwordAiMoveFirstOutcome Success(SwordAiMoveFirstResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); Assert.NotNull(result.Continuation); return Assert.IsType<SwordAiMoveFirstOutcome>(result.Outcome); }
    private static SwordAiMoveFirstFailure Atomic(FacingTurnState state, SwordAiMoveFirstResult result, SwordAiMoveFirstRejection reason)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Equal(reason, result.Failure!.Reason); return result.Failure; }
}
