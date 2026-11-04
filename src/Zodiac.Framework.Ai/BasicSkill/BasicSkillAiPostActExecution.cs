using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum BasicSkillAiPostActExecutionRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, DispatchRejected, UnsupportedPlan, BoundaryRejected }
public sealed record BasicSkillAiPostActExecutionFailure(BasicSkillAiPostActExecutionRejection Reason,
    BasicSkillAiPostActFailure? Selection = null, SwordAiDispatchRejection Dispatch = SwordAiDispatchRejection.None,
    FacingDecisionRejection Boundary = FacingDecisionRejection.None);
public sealed record BasicSkillAiPostActExecutionOutcome(BasicSkillAiPostActOutcome Selection, SwordAiDispatchOutcome Dispatch, RandomState DecisionRng);
public sealed record BasicSkillAiPostActExecutionResult(BasicSkillAiPostActExecutionFailure? Failure,
    FacingDecisionResult<BasicSkillAiPostActExecutionOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public FlatMovementPath? Path => Execution.Path;
    public BasicSkillAiPostActExecutionOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Fresh explicit in/out-of-reach post-ACT selection, actual remaining Move/END and facing settlement.</summary>
/// <remarks>ZOD-243/246. Uses actual boosted state, Framework authority and per-command rollback; no additional ACT,
/// frozen Move-first token, clock advance or public cached decision input.</remarks>
public static class BasicSkillAiPostActExecution
{
    public static BasicSkillAiPostActExecutionResult ExecuteInReach(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiPostActConfiguration configuration, SwordAiCommandDispatchProfile dispatch) => ExecuteCore(state, request, configuration, dispatch, false);
    public static BasicSkillAiPostActExecutionResult ExecuteOutOfReach(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiPostActConfiguration configuration, SwordAiCommandDispatchProfile dispatch) => ExecuteCore(state, request, configuration, dispatch, true);

    private static BasicSkillAiPostActExecutionResult ExecuteCore(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiPostActConfiguration configuration, SwordAiCommandDispatchProfile dispatch, bool outside)
    {
        ArgumentNullException.ThrowIfNull(state); BasicSkillAiPostActSelection.ValidateArguments(state.Battle, request, configuration);
        ArgumentNullException.ThrowIfNull(dispatch);
        BasicSkillAiPostActExecutionFailure? failure = null;
        var result = FacingDecisionBattle.Execute<BasicSkillAiPostActExecutionOutcome>(state, request.Turn.ActorId, context => {
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (actor.MovementTaken || !actor.ActionTaken)
            { failure = new(BasicSkillAiPostActExecutionRejection.UnavailableChoices); return context.Reject<BasicSkillAiPostActExecutionOutcome>(); }
            if (!dispatch.InitializedActorContext || !dispatch.PresentCompactRecords || !dispatch.CanonicalCoordinates || !dispatch.OrdinaryContinuation)
            { failure = new(BasicSkillAiPostActExecutionRejection.UnsupportedDispatchContext); return context.Reject<BasicSkillAiPostActExecutionOutcome>(); }
            var selection = outside
                ? BasicSkillAiPostActSelection.EvaluateOutOfReach(context.Battle, request, configuration, context.RngState)
                : BasicSkillAiPostActSelection.EvaluateInReach(context.Battle, request, configuration, context.RngState);
            if (!selection.Accepted)
            { failure = new(BasicSkillAiPostActExecutionRejection.SelectionRejected, selection.Failure); return context.Reject<BasicSkillAiPostActExecutionOutcome>(); }
            var plan = selection.Outcome!.Plan;
            var dispatched = SwordAiCommandDispatch.DispatchFreshSelection(context.Battle,
                new(context.ActorId, request.Turn.TargetId, SwordAiDecisionMarker.Decided,
                    new(context.ActorId, actor.MovementTaken, actor.ActionTaken),
                    new(SwordAiSelectedPlanKind.MoveOnly, plan.ActorId, plan.Destination, null)),
                configuration.Preparation.Attack.Movement, dispatch with { FreshSelectionCompleted = true });
            if (!dispatched.Accepted)
            { failure = new(BasicSkillAiPostActExecutionRejection.DispatchRejected, Dispatch: dispatched.Rejection); return context.Reject<BasicSkillAiPostActExecutionOutcome>(); }
            var diagnostic = new BasicSkillAiPostActExecutionOutcome(selection.Outcome, dispatched.Outcome!, selection.RngState);
            var facing = new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId);
            if (plan.Kind == BasicSkillAiPlanKind.MoveOnly && plan.Action is null)
            {
                if (dispatched.Outcome!.Kind == SwordAiDispatchKind.Move)
                    return context.PrepareTurnEndingMove(dispatched.Outcome.Move!, configuration.Preparation.Attack.Movement, facing, selection.RngState, diagnostic);
                var body = context.Battle.TurnBattle.Battle.Units.Single(u => u.Id == context.ActorId);
                if (dispatched.Outcome.Kind == SwordAiDispatchKind.EndTurn && plan.Destination == body.Position)
                    return context.PrepareWait(facing, selection.RngState, diagnostic);
            }
            failure = new(BasicSkillAiPostActExecutionRejection.UnsupportedPlan); return context.Reject<BasicSkillAiPostActExecutionOutcome>();
        });
        if (!result.Accepted && failure is null) failure = new(BasicSkillAiPostActExecutionRejection.BoundaryRejected, Boundary: result.Rejection);
        return new(failure, result);
    }
}
