using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum AttackThrowStoneAiFallbackRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, SameTileFallbackRequired, BoundaryRejected }
public sealed record AttackThrowStoneAiFallbackFailure(AttackThrowStoneAiFallbackRejection Reason,
    AttackThrowStoneAiPreparedSelectionFailure? SelectionFailure = null,
    FacingDecisionRejection BoundaryReason = FacingDecisionRejection.None);
public sealed record AttackThrowStoneAiFallbackOutcome(AttackThrowStoneAiSelectionOutcome Decision, RandomState DecisionRng);
public sealed record AttackThrowStoneAiFallbackResult(AttackThrowStoneAiFallbackFailure? Failure,
    FacingDecisionResult<AttackThrowStoneAiFallbackOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public AttackThrowStoneAiFallbackOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own fresh both-unused selection and execute only its same-tile, empty-action END.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-216/218.
/// This is fresh fallback dispatch, not the earlier saved-choice/no-progress guard.</remarks>
public static class AttackThrowStoneAiFallbackExecution
{
    public static AttackThrowStoneAiFallbackResult ExecuteInReach(FacingTurnState state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration,
        SwordAiCommandDispatchProfile dispatchProfile) => ExecuteCore(state, request, configuration, dispatchProfile, false);

    public static AttackThrowStoneAiFallbackResult ExecuteOutOfReach(FacingTurnState state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration,
        SwordAiCommandDispatchProfile dispatchProfile) => ExecuteCore(state, request, configuration, dispatchProfile, true);

    private static AttackThrowStoneAiFallbackResult ExecuteCore(FacingTurnState state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration,
        SwordAiCommandDispatchProfile dispatchProfile, bool outside)
    {
        ArgumentNullException.ThrowIfNull(state);
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state.Battle, request, configuration);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        AttackThrowStoneAiFallbackFailure? failure = null;
        var result = FacingDecisionBattle.Execute<AttackThrowStoneAiFallbackOutcome>(state, request.Turn.ActorId, context =>
        {
            var turn = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (turn.MovementTaken || turn.ActionTaken)
            { failure = new(AttackThrowStoneAiFallbackRejection.UnavailableChoices); return context.Reject<AttackThrowStoneAiFallbackOutcome>(); }
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            { failure = new(AttackThrowStoneAiFallbackRejection.UnsupportedDispatchContext); return context.Reject<AttackThrowStoneAiFallbackOutcome>(); }
            AttackThrowStoneAiSelectionOutcome? decision;
            AttackThrowStoneAiPreparedSelectionFailure? selectionFailure;
            RandomState rng;
            if (outside)
            {
                var selected = AttackThrowStoneAiOutOfReachSelection.Evaluate(context.Battle, request, configuration, context.RngState);
                decision = selected.Outcome?.Selection; selectionFailure = selected.Failure; rng = selected.RngState;
            }
            else
            {
                var selected = AttackThrowStoneAiPreparedSelection.Evaluate(context.Battle, request, configuration, context.RngState);
                decision = selected.Outcome?.Selection; selectionFailure = selected.Failure; rng = selected.RngState;
            }
            if (selectionFailure is not null)
            { failure = new(AttackThrowStoneAiFallbackRejection.SelectionRejected, selectionFailure); return context.Reject<AttackThrowStoneAiFallbackOutcome>(); }
            var plan = decision!.Plan;
            context.Battle.TurnBattle.Battle.TryGetUnit(context.ActorId, out var actor);
            // An empty action is not permission to discard a different-tile MOVE, and a same-tile action must still ACT.
            if (plan.Kind != AttackThrowStoneAiPlanKind.MoveOnly || plan.Action is not null || plan.Destination != actor!.Position)
            { failure = new(AttackThrowStoneAiFallbackRejection.SameTileFallbackRequired); return context.Reject<AttackThrowStoneAiFallbackOutcome>(); }
            return context.PrepareWait(new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId), rng,
                new AttackThrowStoneAiFallbackOutcome(decision, rng));
        });
        if (!result.Accepted && failure is null)
            failure = new(AttackThrowStoneAiFallbackRejection.BoundaryRejected, BoundaryReason: result.Rejection);
        return new(failure, result);
    }
}
