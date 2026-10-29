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

/// <summary>Own fresh in-reach pair selection inside one confirmed Move-first transaction.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-198/203/205/206.
/// No cached plans, ACT-first/out-of-reach execution, post-ACT reselection or host activation.</remarks>
public static class AttackThrowStoneAiMoveFirstExecution
{
    public static AttackThrowStoneAiMoveFirstResult StartInReach(FacingTurnState state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiPreparedSelectionProfile selectionProfile,
        SwordAiScenarioTeam team, SwordAiCommandDispatchProfile dispatchProfile)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(attackProfile);
        ArgumentNullException.ThrowIfNull(throwStoneProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile); ArgumentNullException.ThrowIfNull(dispatchProfile);
        AttackThrowStoneAiMoveFirstFailure? failure = null;
        var result = FacingMoveDecisionBattle.ExecuteMove<AttackThrowStoneAiMoveFirstOutcome>(state, request.ActorId, context =>
        {
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            {
                failure = new(AttackThrowStoneAiMoveFirstRejection.UnsupportedDispatchContext);
                return context.Reject<AttackThrowStoneAiMoveFirstOutcome>();
            }
            var selection = AttackThrowStoneAiPreparedSelection.Evaluate(context.Battle, request, throwStoneAbilityId, weapon,
                attackProfile, throwStoneProfile, scoreProfile, refreshProfile, preparationProfile, selectionProfile, team, context.RngState);
            if (!selection.Accepted)
            {
                failure = new(AttackThrowStoneAiMoveFirstRejection.SelectionRejected, selection.Failure);
                return context.Reject<AttackThrowStoneAiMoveFirstOutcome>();
            }
            var decision = selection.Outcome!; var plan = decision.Selection.Plan;
            var origin = decision.MovementScenario.Preparation.Common.Origin;
            // ai_command_build after fresh selection: both choices are unused (Framework admission).
            // Only Move-first with a genuinely different destination dispatches MOVE; never repeat the earlier no-progress guard.
            if (plan.ActBeforeMove || plan.Destination == origin)
            {
                failure = new(AttackThrowStoneAiMoveFirstRejection.MoveFirstRequired);
                return context.Reject<AttackThrowStoneAiMoveFirstOutcome>();
            }
            var move = new MoveUnitCommand(context.ActorId, plan.Destination);
            var diagnostic = new AttackThrowStoneAiMoveFirstOutcome(decision, move, selection.RngState);
            return plan.Action switch
            {
                AttackThrowStoneAiAttack attack => context.PrepareMoveThenAttack(move, attackProfile.Movement,
                    new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId), weapon, selection.RngState, diagnostic),
                AttackThrowStoneAiStone stone => context.PrepareMoveThenTargetedAbility(move, attackProfile.Movement,
                    new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId, stone.Value.Candidate.TargetId),
                    throwStoneProfile.Ability, selection.RngState, diagnostic),
                _ => context.PrepareMoveThenWait(move, attackProfile.Movement, new FacingChoice.ChooseClosedEnemy(request.TargetId),
                    selection.RngState, diagnostic)
            };
        });
        if (!result.Accepted && failure is null)
            failure = new(AttackThrowStoneAiMoveFirstRejection.BoundaryRejected, BoundaryReason: result.Rejection);
        return new(failure, result);
    }
}
