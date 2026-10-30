using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum SwordAiActFirstRejection { UnavailableChoices, SelectionRejected, DispatchRejected, ActFirstRequired, BoundaryRejected }
/// <summary>Reason-only failure; no partial prefix or command diagnostics escape rejection.</summary>
public sealed record SwordAiActFirstFailure(SwordAiActFirstRejection Reason,
    SwordAiPreparedSelectionFailure? SelectionFailure = null, SwordAiDispatchRejection DispatchReason = SwordAiDispatchRejection.None,
    FacingDecisionRejection BoundaryReason = FacingDecisionRejection.None);
/// <summary>The selected future destination is diagnostic only; it grants no post-ACT Move authority.</summary>
public sealed record SwordAiActFirstOutcome(SwordAiPreparedSelectionOutcome Selection, SwordAiDispatchOutcome Dispatch,
    FftRandomState DecisionRng);
public sealed record SwordAiActFirstResult(SwordAiActFirstFailure? Failure, FacingDecisionResult<SwordAiActFirstOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public SwordAiActFirstOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own fresh ordinary NonBlue ACT-first selection and one actual Attack; stop before post-ACT reselection.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-176/177. No cached plan, Move token or host activation.</remarks>
public static class SwordAiActFirstExecution
{
    public static SwordAiActFirstResult StartPreparedInReach(FacingTurnState state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team,
        SwordAiPreparedSelectionProfile selectionProfile, SwordAiCommandDispatchProfile dispatchProfile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile);
        ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        SwordAiActFirstFailure? failure = null;
        var result = FacingDecisionBattle.Execute<SwordAiActFirstOutcome>(state, request.ActorId, context =>
        {
            var turn = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (turn.MovementTaken || turn.ActionTaken)
            {
                failure = new(SwordAiActFirstRejection.UnavailableChoices);
                return context.Reject<SwordAiActFirstOutcome>();
            }
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            {
                failure = new(SwordAiActFirstRejection.DispatchRejected, DispatchReason: SwordAiDispatchRejection.UnsupportedContext);
                return context.Reject<SwordAiActFirstOutcome>();
            }
            var selection = SwordAiPreparedSelection.EvaluateUnmoved(context.Battle, request, weapon, candidateProfile, scoreProfile,
                refreshProfile, preparationProfile, team, selectionProfile, context.RngState);
            if (!selection.Accepted)
            {
                failure = new(SwordAiActFirstRejection.SelectionRejected, selection.Failure);
                return context.Reject<SwordAiActFirstOutcome>();
            }
            var plan = selection.Outcome!.Decision.Selection.Plan;
            var dispatch = SwordAiCommandDispatch.DispatchFreshSelection(context.Battle,
                new(request.ActorId, request.TargetId, SwordAiDecisionMarker.Decided, new(request.ActorId, false, false),
                    new(plan.Kind, plan.ActorId, plan.Destination, plan.Attack?.Candidate)), candidateProfile.Movement,
                dispatchProfile with { FreshSelectionCompleted = true });
            if (!dispatch.Accepted)
            {
                failure = new(SwordAiActFirstRejection.DispatchRejected, DispatchReason: dispatch.Rejection);
                return context.Reject<SwordAiActFirstOutcome>();
            }
            if (plan.Kind != SwordAiSelectedPlanKind.Attack || !plan.ActBeforeMove || dispatch.Outcome!.Kind != SwordAiDispatchKind.Attack)
            {
                failure = new(SwordAiActFirstRejection.ActFirstRequired);
                return context.Reject<SwordAiActFirstOutcome>();
            }
            return context.PrepareAttack(dispatch.Outcome.Attack!, weapon, selection.RngState,
                new SwordAiActFirstOutcome(selection.Outcome, dispatch.Outcome, selection.RngState));
        });
        if (!result.Accepted && failure is null) failure = new(SwordAiActFirstRejection.BoundaryRejected, BoundaryReason: result.Rejection);
        return new(failure, result);
    }
}
