using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum SwordAiMoveFirstRejection { UnsupportedContext, SelectionRejected, DispatchRejected, MoveFirstRequired, BoundaryRejected }
/// <summary>Reason-only failures; no partial selection/dispatch or advanced RNG escapes a failed Move.</summary>
public sealed record SwordAiMoveFirstFailure(SwordAiMoveFirstRejection Reason,
    SwordAiDerivedSelectionRejection? SelectionReason = null, SwordAiDerivedSelectionStage? SelectionStage = null,
    SwordAiDispatchRejection DispatchReason = SwordAiDispatchRejection.None, FacingMoveRejection BoundaryReason = FacingMoveRejection.None);
public sealed record SwordAiMoveFirstOutcome(SwordAiDerivedSelectionOutcome Decision, SwordAiDispatchOutcome Dispatch, FftRandomState DecisionRng)
{
    public SwordAiOutOfReachSelectionOutcome? OutOfReach { get; init; }
}
public sealed record SwordAiMoveFirstResult(SwordAiMoveFirstFailure? Failure, FacingMoveResult<SwordAiMoveFirstOutcome> Move)
{
    public bool Accepted => Move.Accepted;
    public FacingTurnState State => Move.State;
    public ImmutableArray<AttackResultEvent> Events => Move.Events;
    public FlatMovementPath? Path => Move.Path;
    public FacingMoveContinuation? Continuation => Move.Continuation;
    public SwordAiMoveFirstOutcome? Outcome => Move.Diagnostic;
}

/// <summary>Own one fresh explicit post-prefix selection and real Move-first handoff; follow-up stays in Framework.</summary>
/// <remarks>
/// Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-165/167/169/170.
/// Explicit general-entry certificates remain: already-moved refresh/preparation cannot establish this unmoved entry.
/// No cached-plan input, ACT-first execution, automatic branch dispatch or host activation.
/// </remarks>
public static class SwordAiMoveFirstExecution
{
    public static SwordAiMoveFirstResult StartInReach(FacingTurnState state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, SwordAiExecutionProfile executionProfile, SwordAiCommandDispatchProfile dispatchProfile)
        => StartCore(state, weapon, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, derivedProfile,
            selectionProfile, executionProfile, dispatchProfile, null);

    /// <summary>Explicit out-of-reach branch with known source team; freezes deferred Wait after an actual Move.</summary>
    public static SwordAiMoveFirstResult StartOutOfReach(FacingTurnState state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, SwordAiExecutionProfile executionProfile,
        SwordAiCommandDispatchProfile dispatchProfile, SwordAiScenarioTeam team)
        => StartCore(state, weapon, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, derivedProfile,
            selectionProfile, executionProfile, dispatchProfile, team);

    private static SwordAiMoveFirstResult StartCore(FacingTurnState state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, SwordAiExecutionProfile executionProfile,
        SwordAiCommandDispatchProfile dispatchProfile, SwordAiScenarioTeam? team)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        ArgumentNullException.ThrowIfNull(derivedProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        ArgumentNullException.ThrowIfNull(executionProfile);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        SwordAiMoveFirstFailure? failure = null;
        var result = FacingMoveDecisionBattle.ExecuteMove<SwordAiMoveFirstOutcome>(state, comparisonProfile.ActorId, context =>
        {
            if (!executionProfile.GeneralSelectionEntryContext)
            {
                failure = new(SwordAiMoveFirstRejection.UnsupportedContext);
                return context.Reject<SwordAiMoveFirstOutcome>();
            }
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            {
                failure = new(SwordAiMoveFirstRejection.DispatchRejected, DispatchReason: SwordAiDispatchRejection.UnsupportedContext);
                return context.Reject<SwordAiMoveFirstOutcome>();
            }
            SwordAiOutOfReachSelectionOutcome? outOfReach = null;
            SwordAiDerivedSelectionResult decision;
            if (team is { } knownTeam)
            {
                var query = SwordAiDerivedSelection.EvaluateOutOfReach(context.Battle, weapon, candidateProfile, scoreProfile,
                    comparisonProfile, scenarioProfile, derivedProfile, selectionProfile, knownTeam, context.RngState);
                outOfReach = query.Outcome;
                decision = new(query.Accepted, query.Failure, query.Accepted
                    ? new(outOfReach!.MovementScenario.MovementScenario, outOfReach.Selection) : null, query.RngState);
            }
            else decision = SwordAiDerivedSelection.Evaluate(context.Battle, weapon, candidateProfile, scoreProfile, comparisonProfile,
                scenarioProfile, derivedProfile, selectionProfile, context.RngState);
            if (!decision.Accepted)
            {
                failure = new(SwordAiMoveFirstRejection.SelectionRejected, decision.Failure!.Reason, decision.Failure.Stage);
                return context.Reject<SwordAiMoveFirstOutcome>();
            }
            var plan = decision.Outcome!.Selection.Plan;
            var selected = new SwordAiDispatchSelection(plan.Kind, plan.ActorId, plan.Destination, plan.Attack?.Candidate);
            var request = new SwordAiDispatchRequest(context.ActorId, comparisonProfile.TargetId, SwordAiDecisionMarker.Decided,
                new(context.ActorId, false, false), selected);
            // This invocation actually completed selection; do not re-enter the earlier no-progress guard.
            var dispatch = SwordAiCommandDispatch.DispatchFreshSelection(context.Battle, request, candidateProfile.Movement,
                dispatchProfile with { FreshSelectionCompleted = true });
            if (!dispatch.Accepted)
            {
                failure = new(SwordAiMoveFirstRejection.DispatchRejected, DispatchReason: dispatch.Rejection);
                return context.Reject<SwordAiMoveFirstOutcome>();
            }
            if (dispatch.Outcome!.Kind != SwordAiDispatchKind.Move || plan.ActBeforeMove)
            {
                failure = new(SwordAiMoveFirstRejection.MoveFirstRequired);
                return context.Reject<SwordAiMoveFirstOutcome>();
            }
            var diagnostic = new SwordAiMoveFirstOutcome(decision.Outcome, dispatch.Outcome, decision.RngState) { OutOfReach = outOfReach };
            return plan.Kind == SwordAiSelectedPlanKind.Attack
                ? context.PrepareMoveThenAttack(dispatch.Outcome.Move!, candidateProfile.Movement,
                    new(context.ActorId, comparisonProfile.TargetId), weapon, decision.RngState, diagnostic)
                : context.PrepareMoveThenWait(dispatch.Outcome.Move!, candidateProfile.Movement,
                    new FacingChoice.ChooseClosedEnemy(comparisonProfile.TargetId), decision.RngState, diagnostic);
        });
        if (!result.Accepted && failure is null) failure = new(SwordAiMoveFirstRejection.BoundaryRejected, BoundaryReason: result.Rejection);
        return new(failure, result);
    }
}
