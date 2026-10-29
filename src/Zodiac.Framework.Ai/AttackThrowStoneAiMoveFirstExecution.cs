using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum AttackThrowStoneAiMoveFirstRejection { UnsupportedDispatchContext, SelectionRejected, MoveFirstRequired, BoundaryRejected }
/// <summary>Reasons only; failed Move publishes no private preparation, selection, RNG or token.</summary>
public sealed record AttackThrowStoneAiMoveFirstFailure(AttackThrowStoneAiMoveFirstRejection Reason,
    AttackThrowStoneAiPreparedSelectionFailure? SelectionFailure = null, FacingMoveRejection BoundaryReason = FacingMoveRejection.None);
public sealed record AttackThrowStoneAiMoveFirstOutcome(AttackThrowStoneAiPreparedSelectionOutcome Decision,
    MoveUnitCommand Move, FftRandomState DecisionRng);
public sealed record AttackThrowStoneAiMoveFirstResult(AttackThrowStoneAiMoveFirstFailure? Failure,
    FacingMoveResult<AttackThrowStoneAiMoveFirstOutcome> Move)
{
    public bool Accepted => Move.Accepted;
    public FacingTurnState State => Move.State;
    public ImmutableArray<AttackResultEvent> Events => Move.Events;
    public FlatMovementPath? Path => Move.Path;
    public FacingMoveContinuation? Continuation => Move.Continuation;
    public AttackThrowStoneAiMoveFirstOutcome? Outcome => Move.Diagnostic;
}
public sealed record AttackThrowStoneAiOutOfReachMoveFirstOutcome(AttackThrowStoneAiOutOfReachSelectionOutcome Decision,
    MoveUnitCommand Move, FftRandomState DecisionRng);
public sealed record AttackThrowStoneAiOutOfReachMoveFirstResult(AttackThrowStoneAiMoveFirstFailure? Failure,
    FacingMoveResult<AttackThrowStoneAiOutOfReachMoveFirstOutcome> Move)
{
    public bool Accepted => Move.Accepted;
    public FacingTurnState State => Move.State;
    public ImmutableArray<AttackResultEvent> Events => Move.Events;
    public FlatMovementPath? Path => Move.Path;
    public FacingMoveContinuation? Continuation => Move.Continuation;
    public AttackThrowStoneAiOutOfReachMoveFirstOutcome? Outcome => Move.Diagnostic;
}

/// <summary>Own fresh pair selection in explicit in-reach or known-score out-of-reach Move-first transactions.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-198/202/203/205/206/207.
/// No cached plans, unknown cast scores, ACT-first execution, post-ACT reselection or host activation.</remarks>
public static class AttackThrowStoneAiMoveFirstExecution
{
    public static AttackThrowStoneAiMoveFirstResult StartInReach(FacingTurnState state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiPreparedSelectionProfile selectionProfile,
        SwordAiScenarioTeam team, SwordAiCommandDispatchProfile dispatchProfile)
    {
        var result = StartCore<AttackThrowStoneAiPreparedSelectionOutcome, AttackThrowStoneAiMoveFirstOutcome>(state, request, weapon,
            attackProfile, throwStoneProfile, scoreProfile, refreshProfile, preparationProfile, selectionProfile, dispatchProfile,
            (battle, rng) => {
                var selected = AttackThrowStoneAiPreparedSelection.Evaluate(battle, request, throwStoneAbilityId, weapon,
                    attackProfile, throwStoneProfile, scoreProfile, refreshProfile, preparationProfile, selectionProfile, team, rng);
                return new(selected.Accepted, selected.Outcome, selected.Outcome?.Selection, selected.Outcome?.MovementScenario.Preparation.Common.Origin ?? default,
                    selected.RngState, selected.Failure);
            }, (decision, move, rng) => new(decision, move, rng));
        return new(result.Failure, result.Move);
    }

    public static AttackThrowStoneAiOutOfReachMoveFirstResult StartOutOfReach(FacingTurnState state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiPreparedSelectionProfile selectionProfile,
        SwordAiScenarioTeam team, SwordAiCommandDispatchProfile dispatchProfile)
    {
        var result = StartCore<AttackThrowStoneAiOutOfReachSelectionOutcome, AttackThrowStoneAiOutOfReachMoveFirstOutcome>(state, request, weapon,
            attackProfile, throwStoneProfile, scoreProfile, refreshProfile, preparationProfile, selectionProfile, dispatchProfile,
            (battle, rng) => {
                var selected = AttackThrowStoneAiOutOfReachSelection.Evaluate(battle, request, throwStoneAbilityId, weapon,
                    attackProfile, throwStoneProfile, scoreProfile, refreshProfile, preparationProfile, selectionProfile, team, rng);
                return new(selected.Accepted, selected.Outcome, selected.Outcome?.Selection, selected.Outcome?.MovementScenario.Preparation.Common.Origin ?? default,
                    selected.RngState, selected.Failure);
            }, (decision, move, rng) => new(decision, move, rng));
        return new(result.Failure, result.Move);
    }

    // Only the two owning entry points supply these fresh selectors. No public delegate or cached-result input exists.
    private sealed record FreshSelection<T>(bool Accepted, T? Detail, AttackThrowStoneAiSelectionOutcome? Selection, TilePosition Origin,
        FftRandomState Rng, AttackThrowStoneAiPreparedSelectionFailure? Failure) where T : class;
    private static (AttackThrowStoneAiMoveFirstFailure? Failure, FacingMoveResult<TOutcome> Move) StartCore<TDecision, TOutcome>(
        FacingTurnState state, SwordAiRefreshRequest request, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiPreparedSelectionProfile selectionProfile,
        SwordAiCommandDispatchProfile dispatchProfile, Func<AttackBattleSnapshot, FftRandomState, FreshSelection<TDecision>> select,
        Func<TDecision, MoveUnitCommand, FftRandomState, TOutcome> describe) where TDecision : class
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(attackProfile);
        ArgumentNullException.ThrowIfNull(throwStoneProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile); ArgumentNullException.ThrowIfNull(dispatchProfile);
        AttackThrowStoneAiMoveFirstFailure? failure = null;
        var result = FacingMoveDecisionBattle.ExecuteMove<TOutcome>(state, request.ActorId, context =>
        {
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            {
                failure = new(AttackThrowStoneAiMoveFirstRejection.UnsupportedDispatchContext);
                return context.Reject<TOutcome>();
            }
            var selection = select(context.Battle, context.RngState);
            if (!selection.Accepted)
            {
                failure = new(AttackThrowStoneAiMoveFirstRejection.SelectionRejected, selection.Failure);
                return context.Reject<TOutcome>();
            }
            var plan = selection.Selection!.Plan; var origin = selection.Origin;
            // ai_command_build after fresh selection: both choices are unused (Framework admission).
            // Only Move-first with a genuinely different destination dispatches MOVE; never repeat the earlier no-progress guard.
            if (plan.ActBeforeMove || plan.Destination == origin)
            {
                failure = new(AttackThrowStoneAiMoveFirstRejection.MoveFirstRequired);
                return context.Reject<TOutcome>();
            }
            var move = new MoveUnitCommand(context.ActorId, plan.Destination);
            var diagnostic = describe(selection.Detail!, move, selection.Rng);
            return plan.Action switch
            {
                AttackThrowStoneAiAttack attack => context.PrepareMoveThenAttack(move, attackProfile.Movement,
                    new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId), weapon, selection.Rng, diagnostic),
                AttackThrowStoneAiStone stone => context.PrepareMoveThenTargetedAbility(move, attackProfile.Movement,
                    new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId, stone.Value.Candidate.TargetId),
                    throwStoneProfile.Ability, selection.Rng, diagnostic),
                _ => context.PrepareMoveThenWait(move, attackProfile.Movement, new FacingChoice.ChooseClosedEnemy(request.TargetId),
                    selection.Rng, diagnostic)
            };
        });
        if (!result.Accepted && failure is null)
            failure = new(AttackThrowStoneAiMoveFirstRejection.BoundaryRejected, BoundaryReason: result.Rejection);
        return (failure, result);
    }
}
