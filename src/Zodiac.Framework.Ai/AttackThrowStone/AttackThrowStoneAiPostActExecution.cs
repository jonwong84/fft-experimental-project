using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum AttackThrowStoneAiPostActExecutionRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, DispatchRejected, UnsupportedPlan, BoundaryRejected }
public sealed record AttackThrowStoneAiPostActExecutionFailure(AttackThrowStoneAiPostActExecutionRejection Reason,
    AttackThrowStoneAiPostActSelectionFailure? SelectionFailure = null,
    SwordAiDispatchRejection DispatchRejection = SwordAiDispatchRejection.None,
    FacingDecisionRejection BoundaryRejection = FacingDecisionRejection.None);
public sealed record AttackThrowStoneAiPostActExecutionOutcome(AttackThrowStoneAiPostActSelectionOutcome Selection,
    SwordAiDispatchOutcome Dispatch, RandomState DecisionRng);
public sealed record AttackThrowStoneAiPostActExecutionResult(AttackThrowStoneAiPostActExecutionFailure? Failure,
    FacingDecisionResult<AttackThrowStoneAiPostActExecutionOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public FlatMovementPath? Path => Execution.Path;
    public AttackThrowStoneAiPostActExecutionOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own fresh post-ACT pair selection and settle one actual path Move or same-tile END.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-209/215.
/// No cached plan, predicted pre-ACT destination, additional ACT, continuation token or clock advance.</remarks>
internal static class AttackThrowStoneAiPostActExecution
{
    public static AttackThrowStoneAiPostActExecutionResult ExecuteInReach(FacingTurnState state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration,
        SwordAiCommandDispatchProfile dispatchProfile) => ExecuteCore(state, request, configuration, dispatchProfile, false);

    public static AttackThrowStoneAiPostActExecutionResult ExecuteOutOfReach(FacingTurnState state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration,
        SwordAiCommandDispatchProfile dispatchProfile) => ExecuteCore(state, request, configuration, dispatchProfile, true);

    private static AttackThrowStoneAiPostActExecutionResult ExecuteCore(FacingTurnState state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration,
        SwordAiCommandDispatchProfile dispatchProfile, bool outside)
    {
        ArgumentNullException.ThrowIfNull(state);
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state.Battle, request, configuration);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        AttackThrowStoneAiPostActExecutionFailure? failure = null;
        var result = FacingDecisionBattle.Execute<AttackThrowStoneAiPostActExecutionOutcome>(state, request.Turn.ActorId, context =>
        {
            var turn = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (turn.MovementTaken || !turn.ActionTaken)
            { failure = new(AttackThrowStoneAiPostActExecutionRejection.UnavailableChoices); return context.Reject<AttackThrowStoneAiPostActExecutionOutcome>(); }
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            { failure = new(AttackThrowStoneAiPostActExecutionRejection.UnsupportedDispatchContext); return context.Reject<AttackThrowStoneAiPostActExecutionOutcome>(); }
            var selection = outside
                ? AttackThrowStoneAiPostActSelection.EvaluateOutOfReach(context.Battle, request, configuration, context.RngState)
                : AttackThrowStoneAiPostActSelection.EvaluateInReach(context.Battle, request, configuration, context.RngState);
            if (!selection.Accepted)
            { failure = new(AttackThrowStoneAiPostActExecutionRejection.SelectionRejected, selection.Failure); return context.Reject<AttackThrowStoneAiPostActExecutionOutcome>(); }
            var plan = selection.Outcome!.Plan;
            // ACT is spent, so the pair's movement-only plan has the same command shape as the shared dispatcher.
            var dispatch = SwordAiCommandDispatch.DispatchFreshSelection(context.Battle,
                new(context.ActorId, request.Turn.TargetId, SwordAiDecisionMarker.Decided,
                    new(context.ActorId, turn.MovementTaken, turn.ActionTaken),
                    new(SwordAiSelectedPlanKind.MoveOnly, plan.ActorId, plan.Destination, null)),
                configuration.Attack.Movement, dispatchProfile with { FreshSelectionCompleted = true });
            if (!dispatch.Accepted)
            { failure = new(AttackThrowStoneAiPostActExecutionRejection.DispatchRejected, DispatchRejection: dispatch.Rejection); return context.Reject<AttackThrowStoneAiPostActExecutionOutcome>(); }
            var diagnostic = new AttackThrowStoneAiPostActExecutionOutcome(selection.Outcome, dispatch.Outcome!, selection.RngState);
            var facing = new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId);
            if (plan.Kind == AttackThrowStoneAiPlanKind.MoveOnly && plan.Action is null)
            {
                if (dispatch.Outcome!.Kind == SwordAiDispatchKind.Move)
                    return context.PrepareTurnEndingMove(dispatch.Outcome.Move!, configuration.Attack.Movement, facing, selection.RngState, diagnostic);
                context.Battle.TurnBattle.Battle.TryGetUnit(context.ActorId, out var actor);
                if (dispatch.Outcome.Kind == SwordAiDispatchKind.EndTurn && plan.Destination == actor!.Position)
                    return context.PrepareWait(facing, selection.RngState, diagnostic);
            }
            failure = new(AttackThrowStoneAiPostActExecutionRejection.UnsupportedPlan);
            return context.Reject<AttackThrowStoneAiPostActExecutionOutcome>();
        });
        if (!result.Accepted && failure is null)
            failure = new(AttackThrowStoneAiPostActExecutionRejection.BoundaryRejected, BoundaryRejection: result.Rejection);
        return new(failure, result);
    }
}
