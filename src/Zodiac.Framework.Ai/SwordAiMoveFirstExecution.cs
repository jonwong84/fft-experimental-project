using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum SwordAiMoveFirstRejection { UnsupportedContext, SelectionRejected, DispatchRejected, MoveFirstRequired, BoundaryRejected }
/// <summary>Reason-only failures; no partial selection/dispatch or advanced RNG escapes a failed Move.</summary>
public sealed record SwordAiMoveFirstFailure(SwordAiMoveFirstRejection Reason,
    SwordAiDerivedSelectionRejection? SelectionReason = null, SwordAiDerivedSelectionStage? SelectionStage = null,
    SwordAiDispatchRejection DispatchReason = SwordAiDispatchRejection.None, FacingMoveRejection BoundaryReason = FacingMoveRejection.None)
{
    public SwordAiPreparedSelectionFailure? PreparedSelectionFailure { get; init; }
}
public sealed record SwordAiMoveFirstOutcome(SwordAiDerivedSelectionOutcome Decision, SwordAiDispatchOutcome Dispatch, FftRandomState DecisionRng)
{
    public SwordAiOutOfReachSelectionOutcome? OutOfReach { get; init; }
    public SwordAiPreparationOutcome? Preparation { get; init; }
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

/// <summary>Own fresh selection and real Move-first handoff; follow-up stays in Framework.</summary>
/// <remarks>
/// Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-165/167/169/170/174/175.
/// Post-prefix entries retain their general-entry certificate. Prepared entries own fresh unmoved preparation instead.
/// No cached-plan input, ACT-first execution, automatic branch dispatch or host activation.
/// </remarks>
public static class SwordAiMoveFirstExecution
{
    public static SwordAiMoveFirstResult StartInReach(FacingTurnState state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, SwordAiExecutionProfile executionProfile, SwordAiCommandDispatchProfile dispatchProfile)
        => StartCore(state, weapon, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, derivedProfile,
            selectionProfile, executionProfile, dispatchProfile, null);

    /// <summary>Explicit out-of-reach branch with known source team; freezes deferred Wait after an actual Move.</summary>
    public static SwordAiMoveFirstResult StartOutOfReach(FacingTurnState state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, SwordAiExecutionProfile executionProfile,
        SwordAiCommandDispatchProfile dispatchProfile, SwordAiScenarioTeam team)
        => StartCore(state, weapon, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, derivedProfile,
            selectionProfile, executionProfile, dispatchProfile, team);

    /// <summary>Own fresh NonBlue unmoved preparation and in-reach selection inside the Framework Move transaction.</summary>
    public static SwordAiMoveFirstResult StartPreparedInReach(FacingTurnState state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team,
        SwordAiPreparedSelectionProfile selectionProfile, SwordAiCommandDispatchProfile dispatchProfile)
        => StartPreparedCore(state, request, weapon, candidateProfile, scoreProfile, refreshProfile, preparationProfile,
            team, selectionProfile, dispatchProfile, outOfReach: false);

    /// <summary>Own fresh NonBlue unmoved preparation and explicit out-of-reach selection, then move and retain Wait.</summary>
    public static SwordAiMoveFirstResult StartPreparedOutOfReach(FacingTurnState state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team,
        SwordAiPreparedSelectionProfile selectionProfile, SwordAiCommandDispatchProfile dispatchProfile)
        => StartPreparedCore(state, request, weapon, candidateProfile, scoreProfile, refreshProfile, preparationProfile,
            team, selectionProfile, dispatchProfile, outOfReach: true);

    private static SwordAiMoveFirstResult StartPreparedCore(FacingTurnState state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team,
        SwordAiPreparedSelectionProfile selectionProfile, SwordAiCommandDispatchProfile dispatchProfile, bool outOfReach)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile);
        ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        return ExecuteCore(state, request.ActorId, request.TargetId, weapon, candidateProfile, dispatchProfile, (battle, rng) =>
        {
            var decision = outOfReach
                ? SwordAiPreparedSelection.EvaluateOutOfReachUnmoved(battle, request, weapon, candidateProfile, scoreProfile,
                    refreshProfile, preparationProfile, team, selectionProfile, rng)
                : SwordAiPreparedSelection.EvaluateUnmoved(battle, request, weapon, candidateProfile, scoreProfile,
                    refreshProfile, preparationProfile, team, selectionProfile, rng);
            return decision.Accepted
                ? new(decision.Outcome!.Decision, decision.RngState, decision.Outcome.OutOfReach, decision.Outcome.Preparation)
                : new(null, rng, Failure: new(SwordAiMoveFirstRejection.SelectionRejected) { PreparedSelectionFailure = decision.Failure });
        });
    }

    private static SwordAiMoveFirstResult StartCore(FacingTurnState state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
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
        return ExecuteCore(state, comparisonProfile.ActorId, comparisonProfile.TargetId, weapon, candidateProfile, dispatchProfile, (battle, rng) =>
        {
            SwordAiOutOfReachSelectionOutcome? outOfReach = null;
            SwordAiDerivedSelectionResult decision;
            if (team is { } knownTeam)
            {
                var query = SwordAiDerivedSelection.EvaluateOutOfReach(battle, weapon, candidateProfile, scoreProfile,
                    comparisonProfile, scenarioProfile, derivedProfile, selectionProfile, knownTeam, rng);
                outOfReach = query.Outcome;
                decision = new(query.Accepted, query.Failure, query.Accepted
                    ? new(outOfReach!.MovementScenario.MovementScenario, outOfReach.Selection) : null, query.RngState);
            }
            else decision = SwordAiDerivedSelection.Evaluate(battle, weapon, candidateProfile, scoreProfile, comparisonProfile,
                scenarioProfile, derivedProfile, selectionProfile, rng);
            return decision.Accepted ? new(decision.Outcome, decision.RngState, outOfReach)
                : new(null, rng, Failure: new(SwordAiMoveFirstRejection.SelectionRejected, decision.Failure!.Reason, decision.Failure.Stage));
        }, executionProfile);
    }

    private sealed record PlanningResult(SwordAiDerivedSelectionOutcome? Decision, FftRandomState Rng,
        SwordAiOutOfReachSelectionOutcome? OutOfReach = null, SwordAiPreparationOutcome? Preparation = null,
        SwordAiMoveFirstFailure? Failure = null);

    private static SwordAiMoveFirstResult ExecuteCore(FacingTurnState state, BattleUnitId actorId, BattleUnitId targetId,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiCommandDispatchProfile dispatchProfile,
        Func<AttackBattleSnapshot, FftRandomState, PlanningResult> planner, SwordAiExecutionProfile? postPrefixProfile = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        SwordAiMoveFirstFailure? failure = null;
        var result = FacingMoveDecisionBattle.ExecuteMove<SwordAiMoveFirstOutcome>(state, actorId, context =>
        {
            if (postPrefixProfile is { GeneralSelectionEntryContext: false })
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
            var decision = planner(context.Battle, context.RngState);
            if (decision.Failure is not null)
            {
                failure = decision.Failure;
                return context.Reject<SwordAiMoveFirstOutcome>();
            }
            var plan = decision.Decision!.Selection.Plan;
            var selected = new SwordAiDispatchSelection(plan.Kind, plan.ActorId, plan.Destination, plan.Attack?.Candidate);
            var request = new SwordAiDispatchRequest(context.ActorId, targetId, SwordAiDecisionMarker.Decided,
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
            var diagnostic = new SwordAiMoveFirstOutcome(decision.Decision, dispatch.Outcome, decision.Rng)
                { OutOfReach = decision.OutOfReach, Preparation = decision.Preparation };
            return plan.Kind == SwordAiSelectedPlanKind.Attack
                ? context.PrepareMoveThenAttack(dispatch.Outcome.Move!, candidateProfile.Movement,
                    new(context.ActorId, targetId), weapon, decision.Rng, diagnostic)
                : context.PrepareMoveThenWait(dispatch.Outcome.Move!, candidateProfile.Movement,
                    new FacingChoice.ChooseClosedEnemy(targetId), decision.Rng, diagnostic);
        });
        if (!result.Accepted && failure is null) failure = new(SwordAiMoveFirstRejection.BoundaryRejected, BoundaryReason: result.Rejection);
        return new(failure, result);
    }
}
