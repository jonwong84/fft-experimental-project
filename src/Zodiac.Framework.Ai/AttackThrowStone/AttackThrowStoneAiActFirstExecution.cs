using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum AttackThrowStoneAiActFirstRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, ActFirstRequired, BoundaryRejected }
/// <summary>Reason-only failure; no partial decision or adopted RNG survives a rejected ACT.</summary>
public sealed record AttackThrowStoneAiActFirstFailure(AttackThrowStoneAiActFirstRejection Reason,
    AttackThrowStoneAiPreparedSelectionFailure? SelectionFailure = null, FacingDecisionRejection BoundaryReason = FacingDecisionRejection.None);
/// <summary>Predicted movement destination is diagnostic only; it authorizes no post-ACT Move.</summary>
public sealed record AttackThrowStoneAiActFirstOutcome(AttackThrowStoneAiPreparedSelectionOutcome Decision, RandomState DecisionRng);
public sealed record AttackThrowStoneAiActFirstResult(AttackThrowStoneAiActFirstFailure? Failure,
    FacingDecisionResult<AttackThrowStoneAiActFirstOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public AttackThrowStoneAiActFirstOutcome? Outcome => Execution.Diagnostic;
}
public sealed record AttackThrowStoneAiOutOfReachActFirstOutcome(AttackThrowStoneAiOutOfReachSelectionOutcome Decision, RandomState DecisionRng);
public sealed record AttackThrowStoneAiOutOfReachActFirstResult(AttackThrowStoneAiActFirstFailure? Failure,
    FacingDecisionResult<AttackThrowStoneAiOutOfReachActFirstOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public AttackThrowStoneAiOutOfReachActFirstOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own fresh pair selection and one actual ACT-first Attack or Throw Stone through explicit reach entries.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-198/203/204/208/216/217.
/// No cached decision, unknown cast keys, post-ACT Move token/reselection or host activation.</remarks>
public static class AttackThrowStoneAiActFirstExecution
{
    public static AttackThrowStoneAiActFirstResult StartInReach(FacingTurnState state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, SwordAiCommandDispatchProfile dispatchProfile)
    {
        var result = StartCore<AttackThrowStoneAiPreparedSelectionOutcome, AttackThrowStoneAiActFirstOutcome>(state, request, configuration, dispatchProfile,
            (battle, rng) => {
                var selected = AttackThrowStoneAiPreparedSelection.Evaluate(battle, request, configuration, rng);
                return new(selected.Outcome, selected.Outcome?.Selection, selected.RngState, selected.Failure);
            }, (decision, rng) => new(decision, rng));
        return new(result.Failure, result.Execution);
    }

    public static AttackThrowStoneAiOutOfReachActFirstResult StartOutOfReach(FacingTurnState state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, SwordAiCommandDispatchProfile dispatchProfile)
    {
        var result = StartCore<AttackThrowStoneAiOutOfReachSelectionOutcome, AttackThrowStoneAiOutOfReachActFirstOutcome>(state, request, configuration, dispatchProfile,
            (battle, rng) => {
                var selected = AttackThrowStoneAiOutOfReachSelection.Evaluate(battle, request, configuration, rng);
                return new(selected.Outcome, selected.Outcome?.Selection, selected.RngState, selected.Failure);
            }, (decision, rng) => new(decision, rng));
        return new(result.Failure, result.Execution);
    }

    private sealed record FreshSelection<T>(T? Detail, AttackThrowStoneAiSelectionOutcome? Selection, RandomState Rng,
        AttackThrowStoneAiPreparedSelectionFailure? Failure) where T : class;
    private static (AttackThrowStoneAiActFirstFailure? Failure, FacingDecisionResult<TOutcome> Execution) StartCore<TDecision, TOutcome>(
        FacingTurnState state, AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration,
        SwordAiCommandDispatchProfile dispatchProfile, Func<AttackBattleSnapshot, RandomState, FreshSelection<TDecision>> select,
        Func<TDecision, RandomState, TOutcome> describe) where TDecision : class
    {
        ArgumentNullException.ThrowIfNull(state);
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state.Battle, request, configuration);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        AttackThrowStoneAiActFirstFailure? failure = null;
        var result = FacingDecisionBattle.Execute<TOutcome>(state, request.Turn.ActorId, context =>
        {
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (actor.MovementTaken || actor.ActionTaken)
            {
                failure = new(AttackThrowStoneAiActFirstRejection.UnavailableChoices);
                return context.Reject<TOutcome>();
            }
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            {
                failure = new(AttackThrowStoneAiActFirstRejection.UnsupportedDispatchContext);
                return context.Reject<TOutcome>();
            }
            var selection = select(context.Battle, context.RngState);
            if (selection.Failure is not null)
            {
                failure = new(AttackThrowStoneAiActFirstRejection.SelectionRejected, selection.Failure);
                return context.Reject<TOutcome>();
            }
            var plan = selection.Selection!.Plan;
            // Source fresh dispatch with both choices unused: ACT-first skips MOVE and emits the selected nonempty ACT.
            if (!plan.ActBeforeMove || plan.Action is null)
            {
                failure = new(AttackThrowStoneAiActFirstRejection.ActFirstRequired);
                return context.Reject<TOutcome>();
            }
            var diagnostic = describe(selection.Detail!, selection.Rng);
            return plan.Action switch
            {
                AttackThrowStoneAiAttack attack => context.PrepareAttack(new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId),
                    request.Weapon, selection.Rng, diagnostic),
                AttackThrowStoneAiStone stone => context.PrepareTargetedAbility(
                    new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId, stone.Value.Candidate.TargetId),
                    configuration.ThrowStone.Ability, selection.Rng, diagnostic),
                _ => throw new InvalidOperationException("Unsupported private pair action.")
            };
        });
        if (!result.Accepted && failure is null)
            failure = new(AttackThrowStoneAiActFirstRejection.BoundaryRejected, BoundaryReason: result.Rejection);
        return (failure, result);
    }
}
