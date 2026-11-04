using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum BasicSkillAiCommandKind { Move, Attack, ThrowStone, Accumulate, End }
public enum BasicSkillAiCommandRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, DispatchMismatch, BoundaryRejected }
/// <summary>Failure reasons only; no private selection, progressed RNG, path or token is published.</summary>
public sealed record BasicSkillAiCommandFailure(BasicSkillAiCommandRejection Reason,
    BasicSkillAiSelectionFailure? Selection = null, FacingDecisionRejection Boundary = FacingDecisionRejection.None);
public sealed record BasicSkillAiCommandOutcome(BasicSkillAiSelectionOutcome Decision, RandomState DecisionRng, BasicSkillAiCommandKind Kind);
public sealed record BasicSkillAiCommandResult(BasicSkillAiCommandFailure? Failure,
    FacingDecisionResult<BasicSkillAiCommandOutcome> Execution)
{
    public bool Accepted => Execution.Accepted;
    public FacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public FlatMovementPath? Path => Execution.Path;
    public FacingMoveContinuation? Continuation => Execution.Continuation;
    public BasicSkillAiCommandOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own one fresh supplied-scenario selection and dispatch its first actual Move, ACT or END.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-240–242. Both choices must be unused.
/// No cached plan input, derived scenario, post-ACT reselection, complete turn or host activation.</remarks>
public static class BasicSkillAiCommandExecution
{
    public static BasicSkillAiCommandResult Execute(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection,
        SwordAiCommandDispatchProfile dispatch) => ExecuteMode(state, request, scenario, profile, selection, dispatch, BasicSkillAiDispatchMode.Any);

    internal static BasicSkillAiCommandResult ExecuteMode(FacingTurnState state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection,
        SwordAiCommandDispatchProfile dispatch, BasicSkillAiDispatchMode mode)
    {
        var result = BasicSkillAiCommandPipeline.Execute(state, request, scenario, profile, selection, dispatch, mode,
            (decision, rng, kind) => new BasicSkillAiCommandOutcome(decision, rng, kind));
        return new(result.Failure, result.Execution);
    }
}

internal enum BasicSkillAiDispatchMode { Any, ActFirst, MoveFirst, SameTileEnd }

// Only owning entry points call this private selection pipeline; public callers cannot supply a selector or plan.
internal static class BasicSkillAiCommandPipeline
{
    internal static (BasicSkillAiCommandFailure? Failure, FacingDecisionResult<T> Execution) Execute<T>(
        FacingTurnState state, AccumulateAiPreparationRequest request, ThrowStoneAiScanScenario scenario,
        BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection, SwordAiCommandDispatchProfile dispatch,
        BasicSkillAiDispatchMode mode, Func<BasicSkillAiSelectionOutcome, RandomState, BasicSkillAiCommandKind, T> describe)
    {
        ArgumentNullException.ThrowIfNull(state); BasicSkillAiComposition.ValidateArguments(state.Battle, request, scenario, profile);
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(dispatch);
        BasicSkillAiCommandFailure? failure = null;
        var result = FacingDecisionBattle.Execute<T>(state, request.Turn.ActorId, context => {
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (actor.MovementTaken || actor.ActionTaken)
            { failure = new(BasicSkillAiCommandRejection.UnavailableChoices); return context.Reject<T>(); }
            if (!dispatch.InitializedActorContext || !dispatch.PresentCompactRecords || !dispatch.CanonicalCoordinates || !dispatch.OrdinaryContinuation)
            { failure = new(BasicSkillAiCommandRejection.UnsupportedDispatchContext); return context.Reject<T>(); }
            var selected = BasicSkillAiSelection.Evaluate(context.Battle, request, scenario, profile, selection, context.RngState);
            if (!selected.Accepted)
            { failure = new(BasicSkillAiCommandRejection.SelectionRejected, selected.Failure); return context.Reject<T>(); }
            var plan = selected.Outcome!.Plan;
            var origin = context.Battle.TurnBattle.Battle.Units.Single(u => u.Id == context.ActorId).Position;
            // ai_command_build after fresh selection. The saved-choice no-progress guard ran earlier in source.
            var kind = !plan.ActBeforeMove && plan.Destination != origin ? BasicSkillAiCommandKind.Move : plan.Action switch {
                BasicSkillAiAttack => BasicSkillAiCommandKind.Attack,
                BasicSkillAiStone => BasicSkillAiCommandKind.ThrowStone,
                BasicSkillAiAccumulate => BasicSkillAiCommandKind.Accumulate,
                _ => BasicSkillAiCommandKind.End
            };
            if (mode == BasicSkillAiDispatchMode.ActFirst && (!plan.ActBeforeMove || plan.Action is null) ||
                mode == BasicSkillAiDispatchMode.MoveFirst && kind != BasicSkillAiCommandKind.Move ||
                mode == BasicSkillAiDispatchMode.SameTileEnd && kind != BasicSkillAiCommandKind.End)
            { failure = new(BasicSkillAiCommandRejection.DispatchMismatch); return context.Reject<T>(); }
            var diagnostic = describe(selected.Outcome, selected.RngState, kind);
            var facing = new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId);
            if (kind == BasicSkillAiCommandKind.Move)
            {
                var move = new MoveUnitCommand(context.ActorId, plan.Destination);
                var movement = profile.Preparation.Attack.Movement;
                return plan.Action switch {
                    BasicSkillAiAttack attack => context.PrepareMoveThenAttack(move, movement,
                        new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId), request.Weapon, selected.RngState, diagnostic),
                    BasicSkillAiStone stone => context.PrepareMoveThenTargetedAbility(move, movement,
                        new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId, stone.Value.Candidate.TargetId),
                        profile.Preparation.ThrowStone.Ability, selected.RngState, diagnostic),
                    null => context.PrepareMoveThenWait(move, movement, facing, selected.RngState, diagnostic),
                    _ => throw new InvalidOperationException("The closed self entry must retain ACT-first on its complete tie.")
                };
            }
            return plan.Action switch {
                BasicSkillAiAttack attack => context.PrepareAttack(new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId),
                    request.Weapon, selected.RngState, diagnostic),
                BasicSkillAiStone stone => context.PrepareTargetedAbility(new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId,
                    stone.Value.Candidate.TargetId), profile.Preparation.ThrowStone.Ability, selected.RngState, diagnostic),
                BasicSkillAiAccumulate self => context.PrepareImmediateAbility(new(self.ActorId, self.Value.Projection!.Outcome!.Candidate.AbilityId),
                    profile.Preparation.Accumulate.Ability, selected.RngState, diagnostic),
                null => context.PrepareWait(facing, selected.RngState, diagnostic),
                _ => throw new InvalidOperationException("Unsupported privately selected action.")
            };
        });
        if (!result.Accepted && failure is null) failure = new(BasicSkillAiCommandRejection.BoundaryRejected, Boundary: result.Rejection);
        return (failure, result);
    }
}
