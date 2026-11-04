using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum AttackThrowStoneAiCommandKind { Move, Attack, ThrowStone, EndTurn }
public enum AttackThrowStoneAiCommandRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, UnsupportedPlan, BoundaryRejected }
public sealed record AttackThrowStoneAiCommandFailure(AttackThrowStoneAiCommandRejection Reason,
    AttackThrowStoneAiPreparedSelectionFailure? BeforeActFailure = null,
    AttackThrowStoneAiPostActSelectionFailure? AfterActFailure = null,
    FacingDecisionRejection BoundaryRejection = FacingDecisionRejection.None);
public sealed record AttackThrowStoneAiCommandOutcome(AttackThrowStoneAiCommandKind Kind,
    AttackThrowStoneAiCommandSelection Decision, RandomState DecisionRng);
public sealed record AttackThrowStoneAiCommandResult(AttackThrowStoneAiCommandFailure? Failure,
    FacingDecisionResult<AttackThrowStoneAiCommandOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public FlatMovementPath? Path => Execution.Path;
    public FacingMoveContinuation? Continuation => Execution.Continuation;
    public AttackThrowStoneAiCommandOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own one fresh pair decision and dispatch one actual command, deriving reach and order privately.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-221/223.
/// Fresh-entry assertions remain required. No cached plan, speculative retry, whole-turn transaction or host activation.</remarks>
public static class AttackThrowStoneAiCommandExecution
{
    public static AttackThrowStoneAiCommandResult Execute(FacingTurnState state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, SwordAiCommandDispatchProfile dispatchProfile)
    {
        ArgumentNullException.ThrowIfNull(state);
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state.Battle, request, configuration);
        ArgumentNullException.ThrowIfNull(dispatchProfile);
        AttackThrowStoneAiCommandFailure? failure = null;
        var result = FacingDecisionBattle.Execute<AttackThrowStoneAiCommandOutcome>(state, request.Turn.ActorId, context => {
            var turn = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (turn.MovementTaken)
            { failure = new(AttackThrowStoneAiCommandRejection.UnavailableChoices); return context.Reject<AttackThrowStoneAiCommandOutcome>(); }
            if (!dispatchProfile.InitializedActorContext || !dispatchProfile.PresentCompactRecords ||
                !dispatchProfile.CanonicalCoordinates || !dispatchProfile.OrdinaryContinuation)
            { failure = new(AttackThrowStoneAiCommandRejection.UnsupportedDispatchContext); return context.Reject<AttackThrowStoneAiCommandOutcome>(); }
            var selected = AttackThrowStoneAiCommandSelectionPipeline.Evaluate(context.Battle, request, configuration, context.RngState, turn.ActionTaken);
            if (selected.Failure is not null)
            { failure = selected.Failure; return context.Reject<AttackThrowStoneAiCommandOutcome>(); }
            var decision = selected.Outcome!; var plan = decision.Plan;
            context.Battle.TurnBattle.Battle.TryGetUnit(context.ActorId, out var actor);
            var facing = new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId);
            AttackThrowStoneAiCommandOutcome Describe(AttackThrowStoneAiCommandKind kind) => new(kind, decision, selected.RngState);
            // Source fresh dispatch: MOVE first when allowed and needed, otherwise ACT-used/empty END, otherwise ACT.
            if ((turn.ActionTaken || !plan.ActBeforeMove) && plan.Destination != actor!.Position)
            {
                var move = new MoveUnitCommand(context.ActorId, plan.Destination);
                var diagnostic = Describe(AttackThrowStoneAiCommandKind.Move);
                if (turn.ActionTaken)
                    return context.PrepareTurnEndingMove(move, configuration.Attack.Movement, facing, selected.RngState, diagnostic);
                return plan.Action switch {
                    AttackThrowStoneAiAttack attack => context.PrepareMoveThenAttack(move, configuration.Attack.Movement,
                        new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId), request.Weapon, selected.RngState, diagnostic),
                    AttackThrowStoneAiStone stone => context.PrepareMoveThenTargetedAbility(move, configuration.Attack.Movement,
                        new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId, stone.Value.Candidate.TargetId),
                        configuration.ThrowStone.Ability, selected.RngState, diagnostic),
                    _ => context.PrepareMoveThenWait(move, configuration.Attack.Movement, facing, selected.RngState, diagnostic)
                };
            }
            if (turn.ActionTaken || plan.Action is null)
                return context.PrepareWait(facing, selected.RngState, Describe(AttackThrowStoneAiCommandKind.EndTurn));
            switch (plan.Action)
            {
                case AttackThrowStoneAiAttack attack:
                    return context.PrepareAttack(new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId), request.Weapon,
                        selected.RngState, Describe(AttackThrowStoneAiCommandKind.Attack));
                case AttackThrowStoneAiStone stone:
                    return context.PrepareTargetedAbility(new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId, stone.Value.Candidate.TargetId),
                        configuration.ThrowStone.Ability, selected.RngState, Describe(AttackThrowStoneAiCommandKind.ThrowStone));
                default:
                    failure = new(AttackThrowStoneAiCommandRejection.UnsupportedPlan);
                    return context.Reject<AttackThrowStoneAiCommandOutcome>();
            }
        });
        if (!result.Accepted && failure is null)
            failure = new(AttackThrowStoneAiCommandRejection.BoundaryRejected, BoundaryRejection: result.Rejection);
        return new(failure, result);
    }
}
