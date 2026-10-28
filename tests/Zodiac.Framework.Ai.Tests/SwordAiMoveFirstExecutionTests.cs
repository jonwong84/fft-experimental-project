using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiMoveFirstExecutionTests
{
    private static readonly SwordAiMovementScenarioProfile Scenario = new(true, true, true);
    private static readonly SwordAiDerivedSelectionProfile Derived = new(true, true, true, true, true, true, true, true);
    private static readonly SwordAiSelectionProfile Selection = new(true, true, true);
    private static readonly SwordAiExecutionProfile Execution = new(true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Fresh_selection_and_dispatch_commit_real_Move_then_retained_Attack_without_reselection()
    {
        var state = Ready(evade: 0, move: false); var before = Snapshot(state.Battle); var moved = Start(state); var o = Success(moved);
        Assert.Equal(new FftRandomState(180171308, 18), o.DecisionRng);
        Assert.Equal(5, o.Decision.MovementScenario.Movement.Length);
        Assert.False(o.Decision.Selection.Plan.ActBeforeMove); Assert.Equal(SwordAiDispatchKind.Move, o.Dispatch.Kind);
        Assert.Equal(new TilePosition(2, 1, 0), o.Decision.Selection.Plan.Destination);
        Assert.Equal(BattleFacing.West, moved.Path!.FinalHeading); Assert.Null(moved.State.PendingFacing);
        Assert.Equal(state.Revision + 1, moved.State.Revision);
        Assert.Equal(o.DecisionRng, moved.State.Battle.TurnBattle.Turns.RngState);
        var decision = SwordAiDerivedSelection.Evaluate(state.Battle, new(Weapon, 5), Candidate, Score, new(A, B, true),
            Scenario, Derived, Selection, state.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(Serialize(decision.Outcome), Serialize(o.Decision)); Assert.Equal(decision.RngState, o.DecisionRng);
        var next = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(next.Accepted);
        Assert.Equal(new AttackResolvedEvent(A, B, true, false, 25), Assert.Single(next.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(new FftRandomState(3791773014, 24), next.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(25, next.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(moved.State.Revision + 1, next.State.Revision); Assert.NotNull(next.State.PendingFacing);
        Assert.Equal(before, Snapshot(state.Battle)); Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, next.State.Battle.TurnBattle.Turns.Ticks);
    }

    [Fact]
    public void Low_accuracy_lottery_and_actual_miss_use_the_retained_stream_instead_of_new_preparation()
    {
        var state = Ready(move: false); var moved = Start(state); var o = Success(moved);
        Assert.Equal(new FftRandomState(3535774167, 25), o.DecisionRng); Assert.Equal(8, o.Decision.Selection.Lottery.Length);
        var next = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(next.Accepted);
        Assert.Equal(new AttackResolvedEvent(A, B, false, false, 0), Assert.Single(next.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(new FftRandomState(2473755763, 29), next.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(50, next.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
    }

    [Fact]
    public void Move_only_fallback_freezes_deferred_Wait_and_finishes_at_exact_MAX_without_another_draw()
    {
        var state = Ready(evade: 0, move: false, count: ulong.MaxValue - 18);
        var moved = Start(state, power: 0); var o = Success(moved);
        Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Decision.Selection.Plan.Kind);
        Assert.Equal(8, o.Decision.Selection.Lottery.Length); Assert.Equal(ulong.MaxValue, o.DecisionRng.DrawCount);
        Assert.Equal(new TilePosition(2, 1, 0), moved.Path!.Tiles[^1]);
        var next = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation); Assert.True(next.Accepted);
        Assert.Equal(o.DecisionRng, next.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(35, next.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Assert.Equal(BattleFacing.West, next.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
        Assert.Null(next.State.PendingFacing); Assert.Empty(next.Events.OfType<AttackResolvedEvent>());
    }

    [Fact]
    public void Selection_failure_rolls_back_initial_Move_but_late_actual_Attack_failure_preserves_that_Move()
    {
        var early = Ready(evade: 0, move: false, count: ulong.MaxValue - 10);
        var rejected = Start(early); Atomic(early, rejected, SwordAiMoveFirstRejection.SelectionRejected);
        Assert.Equal(SwordAiDerivedSelectionStage.Lottery, rejected.Failure!.SelectionStage);
        var fits = Start(Ready(evade: 0, move: false, count: ulong.MaxValue - 17)); Success(fits);
        var done = FacingMoveDecisionBattle.Continue(fits.State, A, fits.Continuation); Assert.True(done.Accepted);
        Assert.Equal(ulong.MaxValue, done.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        var state = Ready(evade: 0, move: false, count: ulong.MaxValue - 16); var moved = Start(state); Success(moved);
        var before = Snapshot(moved.State.Battle);
        var failed = FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation);
        Assert.False(failed.Accepted); Assert.Same(moved.State, failed.State); Assert.Empty(failed.Events);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Transition.AttackRejection);
        Assert.Equal(before, Snapshot(moved.State.Battle));
        Assert.Equal(new TilePosition(2, 1, 0), moved.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == A).Position);
        Assert.Equal(failed.Rejection, FacingMoveDecisionBattle.Continue(moved.State, A, moved.Continuation).Rejection);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Missing_entry_context_invalid_dispatch_and_non_Move_plans_publish_no_partial_decision()
    {
        var state = Ready(evade: 0, move: false);
        Atomic(state, Start(state, execution: new()), SwordAiMoveFirstRejection.UnsupportedContext);
        Atomic(state, Start(state, dispatch: new()), SwordAiMoveFirstRejection.DispatchRejected);
        Atomic(state, Start(state, derived: Derived with { FirstAttackEntry = false }), SwordAiMoveFirstRejection.SelectionRejected);
        var alreadyMoved = Ready(); Atomic(alreadyMoved, Start(alreadyMoved), SwordAiMoveFirstRejection.BoundaryRejected);
        // A real initial setup where staying adjacent favors an ACT-first Attack is outside this entry.
        var nearby = Ready(evade: 0, move: false, origin: new(2, 1, 0)); Atomic(nearby, Start(nearby), SwordAiMoveFirstRejection.MoveFirstRequired);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_order_replay_while_continuations_remain_bound_to_their_exact_Move()
    {
        foreach (bool second in new[] { false, true })
        {
            var actor = second ? B : A; var target = second ? A : B;
            var state = Ready(evade: 0, second: second, move: false); var before = Snapshot(state.Battle);
            var first = Start(state, second: second); var replay = Start(state, second: second);
            var other = Start(Ready(evade: 0, second: second, reverse: true, move: false), second: second);
            Success(first); Success(replay); Success(other);
            Assert.Equal(Serialize(first.Outcome), Serialize(other.Outcome)); Assert.Equal(Snapshot(first.State.Battle), Snapshot(other.State.Battle));
            Assert.Equal(FacingMoveRejection.StaleContinuation, FacingMoveDecisionBattle.Continue(replay.State, actor, first.Continuation).Rejection);
            var next = FacingMoveDecisionBattle.Continue(first.State, actor, first.Continuation); Assert.True(next.Accepted);
            Assert.True(FacingTurnBattle.FinishFacing(next.State, actor, next.State.PendingFacing, new FacingChoice.ChooseClosedEnemy(target)).Accepted);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    private static SwordAiMoveFirstResult Start(FacingTurnState state, int power = 5, bool second = false,
        SwordAiExecutionProfile? execution = null, SwordAiCommandDispatchProfile? dispatch = null, SwordAiDerivedSelectionProfile? derived = null) =>
        SwordAiMoveFirstExecution.StartInReach(state, new(Weapon, power), Candidate, Score, new(second ? B : A, second ? A : B, true),
            Scenario, derived ?? Derived, Selection, execution ?? Execution, dispatch ?? Dispatch);
    private static SwordAiMoveFirstOutcome Success(SwordAiMoveFirstResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); Assert.NotNull(result.Continuation); return Assert.IsType<SwordAiMoveFirstOutcome>(result.Outcome); }
    private static void Atomic(FacingTurnState state, SwordAiMoveFirstResult result, SwordAiMoveFirstRejection reason)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Null(result.Path); Assert.Null(result.Continuation); Assert.Equal(reason, result.Failure!.Reason); }
}
