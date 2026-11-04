using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum BasicSkillAiActFirstRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, ActFirstRequired, BoundaryRejected }
public sealed record BasicSkillAiActFirstFailure(BasicSkillAiActFirstRejection Reason,
    BasicSkillAiSelectionFailure? Selection = null, FacingDecisionRejection Boundary = FacingDecisionRejection.None);
/// <summary>Selection diagnostics only. Predicted D1 grants no post-ACT Move or continuation.</summary>
public sealed record BasicSkillAiActFirstOutcome(BasicSkillAiSelectionOutcome Decision, RandomState DecisionRng);
public sealed record BasicSkillAiActFirstResult(BasicSkillAiActFirstFailure? Failure,
    FacingDecisionResult<BasicSkillAiActFirstOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public BasicSkillAiActFirstOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own fresh supplied-scenario three-entry selection and execute one actual ACT-first command.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-237/239. No cached selection input,
/// derived scenarios, Move-first tokens, post-ACT selection, automatic END or host activation.</remarks>
public static class BasicSkillAiActFirstExecution
{
    public static BasicSkillAiActFirstResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection,
        SwordAiCommandDispatchProfile dispatch)
    {
        ArgumentNullException.ThrowIfNull(state); BasicSkillAiComposition.ValidateArguments(state.Battle, request, scenario, profile);
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(dispatch);
        BasicSkillAiActFirstFailure? failure = null;
        var result = FacingDecisionBattle.Execute<BasicSkillAiActFirstOutcome>(state, request.Turn.ActorId, context => {
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (actor.MovementTaken || actor.ActionTaken)
            { failure = new(BasicSkillAiActFirstRejection.UnavailableChoices); return context.Reject<BasicSkillAiActFirstOutcome>(); }
            if (!dispatch.InitializedActorContext || !dispatch.PresentCompactRecords || !dispatch.CanonicalCoordinates || !dispatch.OrdinaryContinuation)
            { failure = new(BasicSkillAiActFirstRejection.UnsupportedDispatchContext); return context.Reject<BasicSkillAiActFirstOutcome>(); }
            var selected = BasicSkillAiSelection.Evaluate(context.Battle, request, scenario, profile, selection, context.RngState);
            if (!selected.Accepted)
            { failure = new(BasicSkillAiActFirstRejection.SelectionRejected, selected.Failure); return context.Reject<BasicSkillAiActFirstOutcome>(); }
            var plan = selected.Outcome!.Plan;
            if (!plan.ActBeforeMove || plan.Action is null)
            { failure = new(BasicSkillAiActFirstRejection.ActFirstRequired); return context.Reject<BasicSkillAiActFirstOutcome>(); }
            var diagnostic = new BasicSkillAiActFirstOutcome(selected.Outcome, selected.RngState);
            return plan.Action switch {
                BasicSkillAiAttack attack => context.PrepareAttack(new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId),
                    request.Weapon, selected.RngState, diagnostic),
                BasicSkillAiStone stone => context.PrepareTargetedAbility(new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId,
                    stone.Value.Candidate.TargetId), profile.Preparation.ThrowStone.Ability, selected.RngState, diagnostic),
                BasicSkillAiAccumulate self => context.PrepareImmediateAbility(new(self.ActorId, self.Value.Projection!.Outcome!.Candidate.AbilityId),
                    profile.Preparation.Accumulate.Ability, selected.RngState, diagnostic),
                _ => throw new InvalidOperationException("Unsupported privately selected action.")
            };
        });
        if (!result.Accepted && failure is null) failure = new(BasicSkillAiActFirstRejection.BoundaryRejected, Boundary: result.Rejection);
        return new(failure, result);
    }
}
