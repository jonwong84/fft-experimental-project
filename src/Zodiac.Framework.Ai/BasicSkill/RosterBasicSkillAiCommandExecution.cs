using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record RosterBasicSkillAiDispatchProfile(bool InitializedActorContext = false,
    bool PresentCompactRecords = false, bool CanonicalCoordinates = false, bool OrdinaryContinuation = false);
public sealed record RosterBasicSkillAiCommandFailure(BasicSkillAiCommandRejection Reason,
    RosterBasicSkillAiSelectionFailure? Selection = null, FacingDecisionRejection Boundary = FacingDecisionRejection.None);
public sealed record RosterBasicSkillAiCommandOutcome(RosterBasicSkillAiSelectionOutcome Decision,
    RandomState DecisionRng, BasicSkillAiCommandKind Kind, bool AfterAct);
public sealed record RosterBasicSkillAiCommandResult(RosterBasicSkillAiCommandFailure? Failure,
    RosterDecisionResult<RosterBasicSkillAiCommandOutcome> Execution, AiPriorityWorkspace Workspace)
{
    public bool Accepted => Execution.Accepted;
    public RosterFacingTurnState State => Execution.State;
    public ImmutableArray<AttackResultEvent> Events => Execution.Events;
    public FlatMovementPath? Path => Execution.Path;
    public RosterMoveContinuation? Continuation => Execution.Continuation;
    public RosterBasicSkillAiCommandOutcome? Outcome => Execution.Diagnostic;
}

/// <summary>Own fresh supported roster selection inside Framework, then publish exactly one actual command.</summary>
/// <remarks>ZOD-314; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No cached plan or caller RNG admission.</remarks>
public static class RosterBasicSkillAiCommandExecution
{
    public static RosterBasicSkillAiCommandResult Execute(RosterFacingTurnState state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile general, RosterAiMovementProfile movement, RosterBasicSkillAiProfile profile,
        RosterBasicSkillAiDispatchProfile dispatch, BattleFacing? facing = null, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(simulation); ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(lists); ArgumentNullException.ThrowIfNull(general);
        ArgumentNullException.ThrowIfNull(movement); ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(dispatch);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
        workspace.ValidateMap(state.Battle); var successor = workspace;
        RosterBasicSkillAiCommandFailure? failure = null;
        var result = RosterDecisionBattle.Execute<RosterBasicSkillAiCommandOutcome>(state, request.ActorId, context => {
            if (!dispatch.InitializedActorContext || !dispatch.PresentCompactRecords ||
                !dispatch.CanonicalCoordinates || !dispatch.OrdinaryContinuation ||
                simulation.Battle.Ground != context.Profile.Ground || !simulation.Battle.Members.SequenceEqual(context.Profile.Members))
            { failure = new(BasicSkillAiCommandRejection.UnsupportedDispatchContext); return context.Reject<RosterBasicSkillAiCommandOutcome>(); }
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            var selected = RosterBasicSkillAiSelection.Evaluate(context.Battle, request, simulation, refresh, lists,
                general, movement, profile, context.RngState, workspace);
            if (!selected.Accepted)
            { failure = new(BasicSkillAiCommandRejection.SelectionRejected, selected.Failure); return context.Reject<RosterBasicSkillAiCommandOutcome>(); }
            var plan = selected.Outcome!.Plan; var action = plan.Action;
            var origin = context.Battle.TurnBattle.Battle.Units.Single(u => u.Id == context.ActorId).Position;
            var kind = !actor.MovementTaken && (actor.ActionTaken || !plan.ActBeforeMove) && plan.Destination != origin
                ? BasicSkillAiCommandKind.Move : actor.ActionTaken || action is null ? BasicSkillAiCommandKind.End : action.Kind switch {
                    AiHumanAbilityKind.Attack => BasicSkillAiCommandKind.Attack,
                    AiHumanAbilityKind.Accumulate => BasicSkillAiCommandKind.Accumulate,
                    AiHumanAbilityKind.ThrowStone => BasicSkillAiCommandKind.ThrowStone,
                    _ => throw new InvalidOperationException("An unsupported entry passed owned selection.") };
            var diagnostic = new RosterBasicSkillAiCommandOutcome(selected.Outcome, selected.RngState, kind, actor.ActionTaken);
            successor = selected.Workspace;
            return Prepare(context, profile, plan, kind, actor.ActionTaken, facing, selected.RngState, diagnostic);
        });
        if (!result.Accepted && failure is null) failure = new(BasicSkillAiCommandRejection.BoundaryRejected, Boundary: result.Rejection);
        return new(failure, result, result.Accepted ? successor : workspace);
    }

    private static RosterDecision<RosterBasicSkillAiCommandOutcome> Prepare(RosterDecisionContext context,
        RosterBasicSkillAiProfile profile, RosterBasicSkillAiSelectedPlan plan, BasicSkillAiCommandKind kind,
        bool afterAct, BattleFacing? facing, RandomState rng, RosterBasicSkillAiCommandOutcome diagnostic)
    {
        var action = plan.Action;
        if (kind == BasicSkillAiCommandKind.Move)
        {
            var move = new MoveUnitCommand(context.ActorId, plan.Destination);
            if (afterAct) return context.PrepareTurnEndingMove(move, facing, rng, diagnostic);
            return action?.Kind switch {
                AiHumanAbilityKind.Attack => context.PrepareMoveThenAttack(move, new(context.ActorId, action.TargetId!.Value), profile.Weapon, rng, diagnostic),
                AiHumanAbilityKind.Accumulate => context.PrepareMoveThenImmediateAbility(move, new(context.ActorId, action.AbilityId!.Value), profile.Accumulate!.Ability, rng, diagnostic),
                AiHumanAbilityKind.ThrowStone => context.PrepareMoveThenTargetedAbility(move, new(context.ActorId, action.AbilityId!.Value, action.TargetId!.Value),
                    profile.ThrowStone!, profile.Heights!.Shared, rng, diagnostic),
                null => context.PrepareMoveThenWait(move, facing, rng, diagnostic),
                _ => throw new InvalidOperationException("An unsupported follow-up passed owned selection.") };
        }
        return kind switch {
            BasicSkillAiCommandKind.Attack => context.PrepareAttack(new(context.ActorId, action!.TargetId!.Value), profile.Weapon, rng, diagnostic),
            BasicSkillAiCommandKind.Accumulate => context.PrepareImmediateAbility(new(context.ActorId, action!.AbilityId!.Value), profile.Accumulate!.Ability, rng, diagnostic),
            BasicSkillAiCommandKind.ThrowStone => context.PrepareTargetedAbility(new(context.ActorId, action!.AbilityId!.Value, action.TargetId!.Value),
                profile.ThrowStone!, profile.Heights!.Shared, rng, diagnostic),
            BasicSkillAiCommandKind.End => context.PrepareWait(facing, rng, diagnostic),
            _ => throw new InvalidOperationException("Unexpected actual roster command.") };
    }
}
