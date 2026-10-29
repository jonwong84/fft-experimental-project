using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum SwordAiPostActExecutionRejection { UnavailableChoices, SelectionRejected, DispatchRejected, UnsupportedPlan, BoundaryRejected }
/// <summary>Reason-only failure; no private planning output or intermediate command escapes rejection.</summary>
public sealed record SwordAiPostActExecutionFailure(SwordAiPostActExecutionRejection Reason,
    SwordAiPostActSelectionFailure? SelectionFailure = null,
    SwordAiDispatchRejection DispatchRejection = SwordAiDispatchRejection.None,
    FacingDecisionRejection BoundaryRejection = FacingDecisionRejection.None);
public sealed record SwordAiPostActExecutionOutcome(SwordAiPostActSelectionOutcome Selection,
    SwordAiDispatchOutcome Dispatch, FftRandomState DecisionRng);
public sealed record SwordAiPostActExecutionResult(SwordAiPostActExecutionFailure? Failure,
    FacingDecisionResult<SwordAiPostActExecutionOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public FlatMovementPath? Path => Execution.Path;
    public SwordAiPostActExecutionOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own one fresh post-ACT decision and actual path Move or END with settled facing.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-176/183/184. No cached plan, external RNG or Move-first token.</remarks>
public static class SwordAiPostActExecution
{
    public static SwordAiPostActExecutionResult ExecuteInReach(FacingTurnState state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team, SwordAiPostActScenarioProfile scenarioProfile,
        SwordAiPostActSelectionProfile selectionProfile, SwordAiCommandDispatchProfile dispatchProfile)
        => ExecuteCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team,
            scenarioProfile, selectionProfile, dispatchProfile, outOfReach: false);

    public static SwordAiPostActExecutionResult ExecuteOutOfReach(FacingTurnState state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team, SwordAiPostActScenarioProfile scenarioProfile,
        SwordAiPostActSelectionProfile selectionProfile, SwordAiCommandDispatchProfile dispatchProfile)
        => ExecuteCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team,
            scenarioProfile, selectionProfile, dispatchProfile, outOfReach: true);

    private static SwordAiPostActExecutionResult ExecuteCore(FacingTurnState state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team, SwordAiPostActScenarioProfile scenarioProfile,
        SwordAiPostActSelectionProfile selectionProfile, SwordAiCommandDispatchProfile dispatchProfile, bool outOfReach)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile); ArgumentNullException.ThrowIfNull(selectionProfile);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        SwordAiPostActExecutionFailure? failure = null;
        var result = FacingDecisionBattle.Execute<SwordAiPostActExecutionOutcome>(state, request.ActorId, context =>
        {
            var turn = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (turn.MovementTaken || !turn.ActionTaken)
            { failure = new(SwordAiPostActExecutionRejection.UnavailableChoices); return context.Reject<SwordAiPostActExecutionOutcome>(); }
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            {
                failure = new(SwordAiPostActExecutionRejection.DispatchRejected, DispatchRejection: SwordAiDispatchRejection.UnsupportedContext);
                return context.Reject<SwordAiPostActExecutionOutcome>();
            }
            var selection = outOfReach
                ? SwordAiPostActSelection.EvaluateOutOfReach(context.Battle, request, candidateProfile, scoreProfile,
                    refreshProfile, preparationProfile, team, scenarioProfile, selectionProfile, context.RngState)
                : SwordAiPostActSelection.EvaluateInReach(context.Battle, request, candidateProfile, scoreProfile,
                    refreshProfile, preparationProfile, team, scenarioProfile, selectionProfile, context.RngState);
            if (!selection.Accepted)
            { failure = new(SwordAiPostActExecutionRejection.SelectionRejected, selection.Failure); return context.Reject<SwordAiPostActExecutionOutcome>(); }
            var plan = selection.Outcome!.Plan;
            var dispatch = SwordAiCommandDispatch.DispatchFreshSelection(context.Battle,
                new(request.ActorId, request.TargetId, SwordAiDecisionMarker.Decided,
                    new(request.ActorId, turn.MovementTaken, turn.ActionTaken),
                    new(plan.Kind, plan.ActorId, plan.Destination, plan.Attack?.Candidate)),
                candidateProfile.Movement, dispatchProfile with { FreshSelectionCompleted = true });
            if (!dispatch.Accepted)
            {
                failure = new(SwordAiPostActExecutionRejection.DispatchRejected, DispatchRejection: dispatch.Rejection);
                return context.Reject<SwordAiPostActExecutionOutcome>();
            }
            var diagnostic = new SwordAiPostActExecutionOutcome(selection.Outcome, dispatch.Outcome!, selection.RngState);
            var facing = new FacingChoice.ChooseClosedEnemy(request.TargetId); // Resolve only after actual placement.
            if (plan.Kind == SwordAiSelectedPlanKind.MoveOnly && plan.Attack is null)
            {
                if (dispatch.Outcome!.Kind == SwordAiDispatchKind.Move)
                    return context.PrepareTurnEndingMove(dispatch.Outcome.Move!, candidateProfile.Movement, facing, selection.RngState, diagnostic);
                context.Battle.TurnBattle.Battle.TryGetUnit(request.ActorId, out var body);
                if (dispatch.Outcome.Kind == SwordAiDispatchKind.EndTurn && plan.Destination == body!.Position)
                    return context.PrepareWait(facing, selection.RngState, diagnostic);
            }
            failure = new(SwordAiPostActExecutionRejection.UnsupportedPlan);
            return context.Reject<SwordAiPostActExecutionOutcome>();
        });
        if (!result.Accepted && failure is null)
            failure = new(SwordAiPostActExecutionRejection.BoundaryRejected, BoundaryRejection: result.Rejection);
        return new(failure, result);
    }
}
