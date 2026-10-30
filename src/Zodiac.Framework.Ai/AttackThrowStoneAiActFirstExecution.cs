using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum AttackThrowStoneAiActFirstRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, ActFirstRequired, BoundaryRejected }
/// <summary>Reason-only failure; no partial decision or adopted RNG survives a rejected ACT.</summary>
public sealed record AttackThrowStoneAiActFirstFailure(AttackThrowStoneAiActFirstRejection Reason,
    AttackThrowStoneAiPreparedSelectionFailure? SelectionFailure = null, FacingDecisionRejection BoundaryReason = FacingDecisionRejection.None);
/// <summary>Predicted movement destination is diagnostic only; it authorizes no post-ACT Move.</summary>
public sealed record AttackThrowStoneAiActFirstOutcome(AttackThrowStoneAiPreparedSelectionOutcome Decision, FftRandomState DecisionRng);
public sealed record AttackThrowStoneAiActFirstResult(AttackThrowStoneAiActFirstFailure? Failure,
    FacingDecisionResult<AttackThrowStoneAiActFirstOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public AttackThrowStoneAiActFirstOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own fresh in-reach pair selection and one actual ACT-first Attack or Throw Stone.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-198/203/204/208.
/// No cached decision, post-ACT Move token/reselection, out-of-reach entry or host activation.</remarks>
public static class AttackThrowStoneAiActFirstExecution
{
    public static AttackThrowStoneAiActFirstResult StartInReach(FacingTurnState state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, SwordAiCommandDispatchProfile dispatchProfile)
    {
        ArgumentNullException.ThrowIfNull(state);
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state.Battle, request, configuration);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        AttackThrowStoneAiActFirstFailure? failure = null;
        var result = FacingDecisionBattle.Execute<AttackThrowStoneAiActFirstOutcome>(state, request.Turn.ActorId, context =>
        {
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (actor.MovementTaken || actor.ActionTaken)
            {
                failure = new(AttackThrowStoneAiActFirstRejection.UnavailableChoices);
                return context.Reject<AttackThrowStoneAiActFirstOutcome>();
            }
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            {
                failure = new(AttackThrowStoneAiActFirstRejection.UnsupportedDispatchContext);
                return context.Reject<AttackThrowStoneAiActFirstOutcome>();
            }
            var selection = AttackThrowStoneAiPreparedSelection.Evaluate(context.Battle, request, configuration, context.RngState);
            if (!selection.Accepted)
            {
                failure = new(AttackThrowStoneAiActFirstRejection.SelectionRejected, selection.Failure);
                return context.Reject<AttackThrowStoneAiActFirstOutcome>();
            }
            var decision = selection.Outcome!; var plan = decision.Selection.Plan;
            // Source fresh dispatch with both choices unused: ACT-first skips MOVE and emits the selected nonempty ACT.
            if (!plan.ActBeforeMove || plan.Action is null)
            {
                failure = new(AttackThrowStoneAiActFirstRejection.ActFirstRequired);
                return context.Reject<AttackThrowStoneAiActFirstOutcome>();
            }
            var diagnostic = new AttackThrowStoneAiActFirstOutcome(decision, selection.RngState);
            return plan.Action switch
            {
                AttackThrowStoneAiAttack attack => context.PrepareAttack(new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId),
                    request.Weapon, selection.RngState, diagnostic),
                AttackThrowStoneAiStone stone => context.PrepareTargetedAbility(
                    new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId, stone.Value.Candidate.TargetId),
                    configuration.ThrowStone.Ability, selection.RngState, diagnostic),
                _ => throw new InvalidOperationException("Unsupported private pair action.")
            };
        });
        if (!result.Accepted && failure is null)
            failure = new(AttackThrowStoneAiActFirstRejection.BoundaryRejected, BoundaryReason: result.Rejection);
        return new(failure, result);
    }
}
