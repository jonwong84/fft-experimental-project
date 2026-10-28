using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Certifies entry after original refresh/policy work, already represented in current state/RNG.</summary>
public sealed record SwordAiExecutionProfile(bool GeneralSelectionEntryContext = false);
public enum SwordAiExecutionStage { Admission, Selection, Command }
public enum SwordAiExecutionRejection { UnsupportedContext, AuthorityRejected, UnavailableChoices, SelectionRejected, UnsupportedPlan, CommandRejected }
public enum SwordAiExecutedCommand { Attack, Wait }

/// <summary>Reason-only scan diagnostics; no hypothetical outcome or intermediate RNG escapes a failed command.</summary>
public sealed record SwordAiExecutionScanFailure(SwordAiScanRejection Reason, int? CandidateIndex,
    SwordAiScoringRejection ScoringReason, SwordAiCandidateRejection ProjectionReason);
public sealed record SwordAiExecutionSelectionFailure(SwordAiDerivedSelectionRejection Reason, SwordAiDerivedSelectionStage Stage,
    SwordAiExecutionScanFailure? AdmissionFailure, SwordAiMovementScenarioFailure? ScenarioFailure,
    SwordAiCompositionFailure? CompositionFailure, SwordAiSelectionFailure? SelectionFailure);
public sealed record SwordAiExecutionCommandFailure(FacingTurnRejection FacingRejection, AttackCommandRejection AttackRejection,
    TurnCycleRejection TurnRejection, BattleTransitionRejection MovementRejection,
    SwordAiWaitSettlementRejection? WaitRejection = null, SwordAiWaitFacingRejection WaitFacingRejection = SwordAiWaitFacingRejection.None,
    FacingDecisionRejection DecisionRejection = FacingDecisionRejection.None);
public sealed record SwordAiExecutionFailure(SwordAiExecutionRejection Reason, SwordAiExecutionStage Stage,
    FacingTurnRejection AuthorityRejection = FacingTurnRejection.None,
    SwordAiExecutionSelectionFailure? SelectionFailure = null, SwordAiExecutionCommandFailure? CommandFailure = null);
public sealed record SwordAiExecutionOutcome(SwordAiDerivedSelectionOutcome Decision, FftRandomState DecisionRng,
    SwordAiExecutedCommand Command, AttackUnitCommand? Attack, SwordAiWaitFacingOutcome? WaitFacing);
/// <summary>Rejection returns the identical input session, empty events and no partial decision/command.</summary>
public sealed record SwordAiExecutionResult(bool Accepted, SwordAiExecutionFailure? Failure, SwordAiExecutionOutcome? Outcome,
    FacingTurnState State, ImmutableArray<AttackResultEvent> Events);

/// <summary>One fresh in-reach decision and actual Attack or Wait after Move is already used.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df, ZOD-154/155. No refresh prefix, cached-plan executor or host activation.</remarks>
public static class SwordAiExecution
{
    public static SwordAiExecutionResult ExecuteAlreadyMoved(FacingTurnState state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, SwordAiExecutionProfile executionProfile,
        SwordAiWaitFacingProfile waitProfile, SwordAiWaitSettlementProfile endProfile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        ArgumentNullException.ThrowIfNull(derivedProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        ArgumentNullException.ThrowIfNull(executionProfile);
        ArgumentNullException.ThrowIfNull(waitProfile);
        ArgumentNullException.ThrowIfNull(endProfile);
        SwordAiExecutionResult Reject(SwordAiExecutionFailure failure) => new(false, failure, null, state, []);
        if (!executionProfile.GeneralSelectionEntryContext || !waitProfile.OriginalAiChooseAtWaitContext ||
            !waitProfile.EligibleOpponent || !endProfile.OrdinaryTurnEndContext)
            return Reject(new(SwordAiExecutionRejection.UnsupportedContext, SwordAiExecutionStage.Admission));
        var actorId = comparisonProfile.ActorId;
        var guard = FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None)
            return Reject(new(SwordAiExecutionRejection.AuthorityRejected, SwordAiExecutionStage.Admission, guard));
        var turn = state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (!turn.MovementTaken || turn.ActionTaken)
            return Reject(new(SwordAiExecutionRejection.UnavailableChoices, SwordAiExecutionStage.Admission));

        SwordAiExecutionFailure? planningFailure = null;
        var execution = FacingDecisionBattle.Execute<SwordAiExecutionOutcome>(state, actorId, context =>
        {
            var decision = SwordAiDerivedSelection.Evaluate(context.Battle, weapon, candidateProfile, scoreProfile, comparisonProfile,
                scenarioProfile, derivedProfile, selectionProfile, context.RngState);
            if (!decision.Accepted)
            {
                var failure = decision.Failure!;
                var scan = failure.AdmissionFailure;
                var reasons = new SwordAiExecutionSelectionFailure(failure.Reason, failure.Stage,
                    scan is null ? null : new(scan.Rejection, scan.FailedCandidateIndex, scan.ScoringRejection, scan.ProjectionRejection),
                    failure.ScenarioFailure, failure.CompositionFailure, failure.SelectionFailure);
                planningFailure = new(SwordAiExecutionRejection.SelectionRejected, SwordAiExecutionStage.Selection, SelectionFailure: reasons);
                return context.Reject<SwordAiExecutionOutcome>();
            }
            var outcome = decision.Outcome!;
            var plan = outcome.Selection.Plan;
            context.Battle.TurnBattle.Battle.TryGetUnit(actorId, out var actor);
            var origin = actor!.Position;
            bool supportedPlan = plan.Kind switch
            {
                SwordAiSelectedPlanKind.Attack => plan.Attack is { Candidate: var candidate } && candidate.ActorId == actorId &&
                    candidate.TargetId == comparisonProfile.TargetId && candidate.Destination == origin,
                SwordAiSelectedPlanKind.MoveOnly => plan.Attack is null,
                _ => false
            };
            if (plan.ActorId != actorId || plan.Destination != origin || !supportedPlan)
            {
                planningFailure = new(SwordAiExecutionRejection.UnsupportedPlan, SwordAiExecutionStage.Command);
                return context.Reject<SwordAiExecutionOutcome>();
            }
            if (plan.Kind == SwordAiSelectedPlanKind.Attack)
            {
                var attack = new AttackUnitCommand(actorId, comparisonProfile.TargetId);
                return context.PrepareAttack(attack, weapon, decision.RngState,
                    new SwordAiExecutionOutcome(outcome, decision.RngState, SwordAiExecutedCommand.Attack, attack, null));
            }
            var facing = SwordAiWaitFacing.Evaluate(context.Battle, actorId, comparisonProfile.TargetId,
                candidateProfile.Movement, waitProfile, decision.RngState);
            if (!facing.Accepted)
            {
                planningFailure = new(SwordAiExecutionRejection.CommandRejected, SwordAiExecutionStage.Command,
                    CommandFailure: new(FacingTurnRejection.None, AttackCommandRejection.None, TurnCycleRejection.None,
                        BattleTransitionRejection.None, SwordAiWaitSettlementRejection.FacingRejected, facing.Rejection));
                return context.Reject<SwordAiExecutionOutcome>();
            }
            return context.PrepareWait(new FacingChoice.Fixed(facing.Outcome!.Facing), decision.RngState,
                new SwordAiExecutionOutcome(outcome, decision.RngState, SwordAiExecutedCommand.Wait, null, facing.Outcome));
        });
        if (execution.Accepted) return new(true, null, execution.Diagnostic!, execution.State, execution.Events);
        if (planningFailure is not null) return Reject(planningFailure);
        var transition = execution.Transition;
        if (execution.Rejection == FacingDecisionRejection.AuthorityRejected)
            return Reject(new(SwordAiExecutionRejection.AuthorityRejected, SwordAiExecutionStage.Admission, transition.FacingRejection));
        var commandFailure = new SwordAiExecutionCommandFailure(transition.FacingRejection, transition.AttackRejection,
            transition.TurnRejection, transition.MovementRejection, DecisionRejection: execution.Rejection);
        return Reject(new(SwordAiExecutionRejection.CommandRejected, SwordAiExecutionStage.Command, CommandFailure: commandFailure));
    }
}
