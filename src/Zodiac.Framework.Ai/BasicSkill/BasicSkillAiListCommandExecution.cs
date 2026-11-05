using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record BasicSkillAiListCommandFailure(BasicSkillAiCommandRejection Reason, BasicSkillAiListFailure? Selection = null,
    FacingDecisionRejection Boundary = FacingDecisionRejection.None);
public sealed record BasicSkillAiListCommandOutcome(BasicSkillAiListSelectionOutcome Selection, RandomState DecisionRng, BasicSkillAiCommandKind Kind);
public sealed record BasicSkillAiListCommandResult(BasicSkillAiListCommandFailure? Failure,
    FacingDecisionResult<BasicSkillAiListCommandOutcome> Execution, AiPriorityWorkspace Workspace)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public FlatMovementPath? Path => Execution.Path;
    public FacingMoveContinuation? Continuation => Execution.Continuation;
    public BasicSkillAiListCommandOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Privately own fresh variable selection and use the existing Framework typed proposals. No cached plan input.</summary>
public static class BasicSkillAiListCommandExecution
{
    public static BasicSkillAiListCommandResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, BasicSkillAiListSelectionProfile selection, SwordAiCommandDispatchProfile dispatch,
        AiPriorityWorkspace? workspace = null) => ExecuteCore(state, request, profile, selection, dispatch, workspace, false);

    /// <summary>One fresh ACT-used/Move-unused decision from actual results, with no remaining action passes.</summary>
    public static BasicSkillAiListCommandResult ExecuteAfterAct(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, BasicSkillAiListSelectionProfile selection, SwordAiCommandDispatchProfile dispatch,
        AiPriorityWorkspace? workspace = null) => ExecuteCore(state, request, profile, selection, dispatch, workspace, true);

    private static BasicSkillAiListCommandResult ExecuteCore(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, BasicSkillAiListSelectionProfile selection, SwordAiCommandDispatchProfile dispatch,
        AiPriorityWorkspace? workspace, bool afterAct)
    {
        ArgumentNullException.ThrowIfNull(state); BasicSkillAiListScenarios.ValidateArguments(state.Battle, request, profile);
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(dispatch);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map); workspace.ValidateMap(state.Battle);
        var successor = workspace; BasicSkillAiListCommandFailure? failure = null;
        var result = FacingDecisionBattle.Execute<BasicSkillAiListCommandOutcome>(state, request.Turn.ActorId, context => {
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (actor.MovementTaken || actor.ActionTaken != afterAct)
            { failure = new(BasicSkillAiCommandRejection.UnavailableChoices); return context.Reject<BasicSkillAiListCommandOutcome>(); }
            if (!dispatch.InitializedActorContext || !dispatch.PresentCompactRecords || !dispatch.CanonicalCoordinates || !dispatch.OrdinaryContinuation)
            { failure = new(BasicSkillAiCommandRejection.UnsupportedDispatchContext); return context.Reject<BasicSkillAiListCommandOutcome>(); }
            var query = BasicSkillAiListSelection.EvaluateCore(context.Battle, request, profile, selection, context.RngState, workspace, afterAct);
            if (!query.Accepted)
            { failure = new(BasicSkillAiCommandRejection.SelectionRejected, query.Failure); return context.Reject<BasicSkillAiListCommandOutcome>(); }
            successor = query.Workspace; var plan = query.Outcome!.Plan;
            var origin = context.Battle.TurnBattle.Battle.Units.Single(u => u.Id == context.ActorId).Position;
            var kind = BasicSkillAiCommandPipeline.Kind(plan, origin);
            var diagnostic = new BasicSkillAiListCommandOutcome(query.Outcome, query.RngState, kind);
            if (afterAct)
            {
                if (plan.Action is not null) throw new InvalidOperationException("A prepared ACT-used list produced an action.");
                var facing = new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId);
                return kind == BasicSkillAiCommandKind.Move
                    ? context.PrepareTurnEndingMove(new(context.ActorId, plan.Destination), profile.Preparation.Attack.Movement, facing, query.RngState, diagnostic)
                    : context.PrepareWait(facing, query.RngState, diagnostic);
            }
            return BasicSkillAiCommandPipeline.Prepare(context, request, profile.Preparation, plan, query.RngState, diagnostic, kind);
        });
        if (!result.Accepted && failure is null) failure = new(BasicSkillAiCommandRejection.BoundaryRejected, Boundary: result.Rejection);
        return new(failure, result, result.Accepted ? successor : workspace);
    }
}
