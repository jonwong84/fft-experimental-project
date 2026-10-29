using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-176/177.
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiActFirstExecutionTests
{
    private static readonly SwordAiPreparedSelectionProfile Selection = new(true, true, true, true, true, true);
    private static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Full_hit_and_low_hit_miss_match_ACT_first_tapes_and_leave_Move_available_without_a_receipt()
    {
        foreach (var (evade, decisionWord, decisionCount, actualWord, actualCount, hit) in new[] {
            (0, 180171308u, 18ul, 3791773014u, 24ul, true), (89, 3535774167u, 25ul, 2473755763u, 29ul, false) })
        {
            var state = Ready(evade: evade, move: false, origin: new(2, 1, 0)); var before = Snapshot(state.Battle);
            var result = Start(state); var o = Success(result); var plan = o.Selection.Decision.Selection.Plan;
            Assert.Equal(new FftRandomState(decisionWord, decisionCount), o.DecisionRng);
            Assert.Equal(4, o.Selection.Preparation.ReachableOrigins.Length);
            Assert.Equal(4, o.Selection.Decision.MovementScenario.Movement.Length);
            Assert.Equal(16838u, Assert.Single(o.Selection.Preparation.Refresh.UpkeepPicks).RawRoll);
            Assert.True(plan.ActBeforeMove); Assert.Equal(SwordAiDispatchKind.Attack, o.Dispatch.Kind); Assert.Null(o.Dispatch.Move);
            Assert.Equal(new SwordAiSavedChoices(A, false, false), o.Dispatch.RecordedChoices);
            Assert.Equal(new AttackResolvedEvent(A, B, hit, false, hit ? 25 : 0), Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
            Assert.Equal(new FftRandomState(actualWord, actualCount), result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(result.State.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(hit ? 25 : 50, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
            var actor = result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A);
            Assert.True(actor.HasTurn); Assert.True(actor.ActionTaken); Assert.False(actor.MovementTaken); Assert.Equal(15, actor.Ct);
            Assert.Equal(state.Revision + 1, result.State.Revision); Assert.Null(result.State.PendingFacing);
            Assert.Equal(FacingTurnRejection.MissingCompletion, FacingTurnBattle.FinishFacing(result.State, A, null, new FacingChoice.KeepCurrent()).FacingRejection);
            Assert.Equal(BattleFacing.East, result.State.Battle.Combatants.Single(u => u.UnitId == A).Facing);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks, result.State.Battle.TurnBattle.Turns.Ticks);
            foreach (var body in state.Battle.TurnBattle.Battle.Units)
                Assert.Equal(body.Position, result.State.Battle.TurnBattle.Battle.Units.Single(u => u.Id == body.Id).Position);
            // Public dispatch data describes source control flow; it is not an executable future Move.
            var request = new SwordAiDispatchRequest(A, B, SwordAiDecisionMarker.Decided, o.Dispatch.RecordedChoices!.Value,
                new(plan.Kind, A, plan.Destination, plan.Attack!.Candidate));
            var next = SwordAiCommandDispatch.InspectEntry(result.State.Battle, request, Candidate.Movement, Dispatch);
            Assert.True(next.Accepted); Assert.Equal(SwordAiDispatchKind.SelectionRequired, next.Outcome!.Kind);
            var unchanged = SwordAiCommandDispatch.InspectEntry(result.State.Battle,
                request with { SavedChoices = new(A, false, true) }, Candidate.Movement, Dispatch);
            Assert.True(unchanged.Outcome!.EndedForNoProgress);
            Atomic(result.State, Start(result.State), SwordAiActFirstRejection.UnavailableChoices);
            Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Exact_actual_budgets_finish_at_MAX_and_late_Attack_failure_rolls_back_the_entire_prefix()
    {
        foreach (var (evade, cost) in new[] { (0, 17ul), (89, 22ul) })
        {
            var fits = Start(Ready(evade: evade, move: false, origin: new(2, 1, 0), count: ulong.MaxValue - cost)); Success(fits);
            Assert.Equal(ulong.MaxValue, fits.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            var state = Ready(evade: evade, move: false, origin: new(2, 1, 0), count: ulong.MaxValue - cost + 1);
            var failed = Start(state); var failure = Atomic(state, failed, SwordAiActFirstRejection.BoundaryRejected);
            Assert.Equal(FacingDecisionRejection.CommandRejected, failure.BoundaryReason);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, failed.Execution.Transition.AttackRejection);
        }
        foreach (int available in new[] { 0, 1, 10 })
        {
            var state = Ready(evade: 0, move: false, origin: new(2, 1, 0), count: ulong.MaxValue - (ulong)available);
            var failure = Atomic(state, Start(state), SwordAiActFirstRejection.SelectionRejected);
            Assert.Equal(available == 0 ? SwordAiPreparedSelectionStage.Preparation : SwordAiPreparedSelectionStage.Selection, failure.SelectionFailure!.Stage);
        }
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Required_context_and_ACT_first_plan_are_checked_without_automatically_routing_other_commands()
    {
        var state = Ready(evade: 0, move: false, origin: new(2, 1, 0));
        Atomic(state, Start(state, dispatch: new()), SwordAiActFirstRejection.DispatchRejected);
        Atomic(state, Start(state, team: SwordAiScenarioTeam.Blue), SwordAiActFirstRejection.SelectionRejected);
        Atomic(state, Start(state, selection: Selection with { NoDefendSupport = false }), SwordAiActFirstRejection.SelectionRejected);
        Atomic(state, Start(state, preparation: Preparation with { CleanActorDelayCache = false }), SwordAiActFirstRejection.SelectionRejected);
        Atomic(state, Start(state, request: Request(second: true)), SwordAiActFirstRejection.BoundaryRejected);
        Atomic(state, Start(state, power: 0), SwordAiActFirstRejection.ActFirstRequired);
        var moving = Ready(evade: 0, move: false); Atomic(moving, Start(moving), SwordAiActFirstRejection.ActFirstRequired);
        var used = Ready(); Atomic(used, Start(used), SwordAiActFirstRejection.UnavailableChoices);
    }

    [Fact]
    public void Actual_KO_is_published_without_fabricating_a_post_ACT_decision_or_automatic_Wait()
    {
        var state = Ready(evade: 0, move: false, origin: new(2, 1, 0)); var result = Start(state, power: 10); Success(result);
        Assert.Equal(0, result.State.Battle.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Single(result.Events.OfType<AttackKnockedOutEvent>()); Assert.Null(result.State.PendingFacing);
        Assert.Equal(A, result.State.Battle.TurnBattle.Turns.ActiveUnitId);
        Assert.False(result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).MovementTaken);
        Assert.Equal(15, result.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        Atomic(result.State, Start(result.State), SwordAiActFirstRejection.UnavailableChoices);
        // Existing owner-directed authority can finish; this AI entry does not select another policy for terminal data.
        var wait = FacingTurnBattle.WaitWithFacing(result.State, A, new FacingChoice.KeepCurrent()); Assert.True(wait.Accepted);
        Assert.Equal(35, wait.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Actor_identity_order_and_same_input_replay_preserve_the_original_trusted_session()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(evade: 0, move: false, origin: new(2, 1, 0), second: second); var before = Snapshot(state.Battle);
            var first = Start(state, request: Request(second)); var replay = Start(state, request: Request(second));
            var reversed = Start(Ready(evade: 0, move: false, origin: new(2, 1, 0), second: second, reverse: true), request: Request(second, reverse: true));
            Success(first); Success(replay); Success(reversed);
            Assert.Equal(Serialize(first.Outcome), Serialize(replay.Outcome)); Assert.Equal(Serialize(first.Outcome), Serialize(reversed.Outcome));
            Assert.Equal(Snapshot(first.State.Battle), Snapshot(replay.State.Battle)); Assert.Equal(Snapshot(first.State.Battle), Snapshot(reversed.State.Battle));
            Assert.Equal(before, Snapshot(state.Battle)); Assert.Equal(new FftRandomState(1, 7), state.Battle.TurnBattle.Turns.RngState);
        }
    }

    private static SwordAiActFirstResult Start(FacingTurnState state, int power = 5, SwordAiRefreshRequest? request = null,
        SwordAiPreparationProfile? preparation = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue,
        SwordAiPreparedSelectionProfile? selection = null, SwordAiCommandDispatchProfile? dispatch = null) =>
        SwordAiActFirstExecution.StartPreparedInReach(state, request ?? Request(), new(Weapon, power), Candidate, Score,
            Refresh, preparation ?? Preparation, team, selection ?? Selection, dispatch ?? Dispatch);
    private static SwordAiActFirstOutcome Success(SwordAiActFirstResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<SwordAiActFirstOutcome>(result.Outcome); }
    private static SwordAiActFirstFailure Atomic(FacingTurnState state, SwordAiActFirstResult result, SwordAiActFirstRejection reason)
    { Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Outcome);
        Assert.Equal(reason, result.Failure!.Reason); return result.Failure; }
}
