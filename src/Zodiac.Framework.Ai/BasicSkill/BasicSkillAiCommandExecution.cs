using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum BasicSkillAiCommandKind { Move, Attack, ThrowStone, Accumulate, End }
public enum BasicSkillAiCommandRejection { UnavailableChoices, UnsupportedDispatchContext, SelectionRejected, DispatchMismatch, BoundaryRejected }
/// <summary>Failure reasons only; no private selection, progressed RNG, path or token is published.</summary>
public sealed record BasicSkillAiCommandFailure(BasicSkillAiCommandRejection Reason,
    BasicSkillAiSelectionFailure? Selection = null, FacingDecisionRejection Boundary = FacingDecisionRejection.None);
public sealed record BasicSkillAiCommandOutcome(BasicSkillAiSelectionOutcome Decision, RandomState DecisionRng, BasicSkillAiCommandKind Kind);
public sealed record BasicSkillAiWorkspaceCommandResult(BasicSkillAiCommandResult Command, AiPriorityWorkspace Workspace);
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

/// <summary>Own one fresh supplied or derived scenario selection and dispatch its first actual Move, ACT or END.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-240–242/253. Both choices must be unused.
/// No cached plan input, post-ACT reselection, complete turn or host activation.</remarks>
public static class BasicSkillAiCommandExecution
{
    /// <summary>Publish successor priority knowledge only with the accepted actual command.</summary>
    public static BasicSkillAiWorkspaceCommandResult ExecuteWithWorkspace(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiDerivedProfile profile, BasicSkillAiSelectionProfile selection, SwordAiCommandDispatchProfile dispatch,
        AiPriorityWorkspace workspace) => BasicSkillAiCommandPipeline.ExecuteWithWorkspace(state, request, profile, selection, dispatch, workspace);

    /// <summary>Derive the fresh scenario and provenance-safe selection inside Framework before issuing one actual command.</summary>
    public static BasicSkillAiCommandResult ExecuteDerived(FacingTurnState state, AccumulateAiPreparationRequest request,
        BasicSkillAiDerivedProfile profile, BasicSkillAiSelectionProfile selection, SwordAiCommandDispatchProfile dispatch)
    {
        var result = BasicSkillAiCommandPipeline.ExecuteDerived(state, request, profile, selection, dispatch,
            (decision, rng, kind) => new BasicSkillAiCommandOutcome(decision, rng, kind));
        return new(result.Failure, result.Execution);
    }

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
    internal static BasicSkillAiWorkspaceCommandResult ExecuteWithWorkspace(FacingTurnState state,
        AccumulateAiPreparationRequest request, BasicSkillAiDerivedProfile profile, BasicSkillAiSelectionProfile selection,
        SwordAiCommandDispatchProfile dispatch, AiPriorityWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(state); BasicSkillAiDerivedComposition.ValidateArguments(state.Battle, request, profile);
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(workspace); workspace.ValidateMap(state.Battle);
        var successor = workspace;
        var result = ExecuteFresh(state, request, profile.Preparation, dispatch, BasicSkillAiDispatchMode.Any,
            (battle, rng) => {
                var query = BasicSkillAiDerivedSelection.EvaluateWithWorkspace(battle, request, profile, selection, workspace, rng);
                successor = query.Workspace;
                return BasicSkillAiDerivedSelection.ForCommand(query.Query);
            }, (decision, rng, kind) => new BasicSkillAiCommandOutcome(decision, rng, kind));
        return new(new(result.Failure, result.Execution), result.Execution.Accepted ? successor : workspace);
    }

    internal static (BasicSkillAiCommandFailure? Failure, FacingDecisionResult<T> Execution) Execute<T>(
        FacingTurnState state, AccumulateAiPreparationRequest request, ThrowStoneAiScanScenario scenario,
        BasicSkillAiProfile profile, BasicSkillAiSelectionProfile selection, SwordAiCommandDispatchProfile dispatch,
        BasicSkillAiDispatchMode mode, Func<BasicSkillAiSelectionOutcome, RandomState, BasicSkillAiCommandKind, T> describe)
    {
        ArgumentNullException.ThrowIfNull(state); BasicSkillAiComposition.ValidateArguments(state.Battle, request, scenario, profile);
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(dispatch);
        return ExecuteFresh(state, request, profile.Preparation, dispatch, mode,
            (battle, rng) => BasicSkillAiSelection.Evaluate(battle, request, scenario, profile, selection, rng), describe);
    }

    internal static (BasicSkillAiCommandFailure? Failure, FacingDecisionResult<T> Execution) ExecuteDerived<T>(
        FacingTurnState state, AccumulateAiPreparationRequest request, BasicSkillAiDerivedProfile profile,
        BasicSkillAiSelectionProfile selection, SwordAiCommandDispatchProfile dispatch,
        Func<BasicSkillAiSelectionOutcome, RandomState, BasicSkillAiCommandKind, T> describe)
    {
        ArgumentNullException.ThrowIfNull(state); BasicSkillAiDerivedComposition.ValidateArguments(state.Battle, request, profile);
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(dispatch);
        return ExecuteFresh(state, request, profile.Preparation, dispatch, BasicSkillAiDispatchMode.Any,
            (battle, rng) => BasicSkillAiDerivedSelection.ForCommand(BasicSkillAiDerivedSelection.Evaluate(battle, request, profile, selection, rng)), describe);
    }

    // Only closed owning factories supply selectors. All share context, guards, dispatch and typed proposals.
    private static (BasicSkillAiCommandFailure? Failure, FacingDecisionResult<T> Execution) ExecuteFresh<T>(
        FacingTurnState state, AccumulateAiPreparationRequest request, AccumulateAiPreparationConfiguration preparation,
        SwordAiCommandDispatchProfile dispatch, BasicSkillAiDispatchMode mode,
        Func<AttackBattleSnapshot, RandomState, BasicSkillAiSelectionResult> select,
        Func<BasicSkillAiSelectionOutcome, RandomState, BasicSkillAiCommandKind, T> describe)
    {
        BasicSkillAiCommandFailure? failure = null;
        var result = FacingDecisionBattle.Execute<T>(state, request.Turn.ActorId, context => {
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            if (actor.MovementTaken || actor.ActionTaken)
            { failure = new(BasicSkillAiCommandRejection.UnavailableChoices); return context.Reject<T>(); }
            if (!dispatch.InitializedActorContext || !dispatch.PresentCompactRecords || !dispatch.CanonicalCoordinates || !dispatch.OrdinaryContinuation)
            { failure = new(BasicSkillAiCommandRejection.UnsupportedDispatchContext); return context.Reject<T>(); }
            var selected = select(context.Battle, context.RngState);
            if (!selected.Accepted)
            { failure = new(BasicSkillAiCommandRejection.SelectionRejected, selected.Failure); return context.Reject<T>(); }
            var plan = selected.Outcome!.Plan;
            var origin = context.Battle.TurnBattle.Battle.Units.Single(u => u.Id == context.ActorId).Position;
            // ai_command_build after fresh selection. The saved-choice no-progress guard ran earlier in source.
            var kind = Kind(plan, origin);
            if (mode == BasicSkillAiDispatchMode.ActFirst && (!plan.ActBeforeMove || plan.Action is null) ||
                mode == BasicSkillAiDispatchMode.MoveFirst && kind != BasicSkillAiCommandKind.Move ||
                mode == BasicSkillAiDispatchMode.SameTileEnd && kind != BasicSkillAiCommandKind.End)
            { failure = new(BasicSkillAiCommandRejection.DispatchMismatch); return context.Reject<T>(); }
            var diagnostic = describe(selected.Outcome, selected.RngState, kind);
            return Prepare(context, request, preparation, plan, selected.RngState, diagnostic, kind);
        });
        if (!result.Accepted && failure is null) failure = new(BasicSkillAiCommandRejection.BoundaryRejected, Boundary: result.Rejection);
        return (failure, result);
    }

    internal static BasicSkillAiCommandKind Kind(BasicSkillAiSelectedPlan plan, TilePosition origin) =>
        !plan.ActBeforeMove && plan.Destination != origin ? BasicSkillAiCommandKind.Move : plan.Action switch {
            BasicSkillAiAttack => BasicSkillAiCommandKind.Attack, BasicSkillAiStone => BasicSkillAiCommandKind.ThrowStone,
            BasicSkillAiAccumulate => BasicSkillAiCommandKind.Accumulate, _ => BasicSkillAiCommandKind.End };

    // Only fresh owning callbacks call this mapper; no public diagnostic/plan can enter an invocation as authority.
    internal static FacingDecision<T> Prepare<T>(FacingDecisionContext context, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration preparation, BasicSkillAiSelectedPlan plan, RandomState decisionRng,
        T diagnostic, BasicSkillAiCommandKind kind)
    {
        var facing = new FacingChoice.ChooseClosedEnemy(request.Turn.TargetId);
        if (kind == BasicSkillAiCommandKind.Move)
        {
            var move = new MoveUnitCommand(context.ActorId, plan.Destination);
            var movement = preparation.Attack.Movement;
            return plan.Action switch {
                BasicSkillAiAttack attack => context.PrepareMoveThenAttack(move, movement,
                    new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId), request.Weapon, decisionRng, diagnostic),
                BasicSkillAiStone stone => context.PrepareMoveThenTargetedAbility(move, movement,
                    new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId, stone.Value.Candidate.TargetId),
                    preparation.ThrowStone.Ability, decisionRng, diagnostic),
                null => context.PrepareMoveThenWait(move, movement, facing, decisionRng, diagnostic),
                _ => throw new InvalidOperationException("The closed self entry must retain ACT-first on its complete tie.")
            };
        }
        return plan.Action switch {
            BasicSkillAiAttack attack => context.PrepareAttack(new(attack.Value.Candidate.ActorId, attack.Value.Candidate.TargetId),
                request.Weapon, decisionRng, diagnostic),
            BasicSkillAiStone stone => context.PrepareTargetedAbility(new(stone.Value.Candidate.ActorId, stone.Value.Candidate.AbilityId,
                stone.Value.Candidate.TargetId), preparation.ThrowStone.Ability, decisionRng, diagnostic),
            BasicSkillAiAccumulate self => context.PrepareImmediateAbility(new(self.ActorId, self.Value.Projection!.Outcome!.Candidate.AbilityId),
                preparation.Accumulate.Ability, decisionRng, diagnostic),
            null => context.PrepareWait(facing, decisionRng, diagnostic),
            _ => throw new InvalidOperationException("Unsupported privately selected action.")
        };
    }

}
