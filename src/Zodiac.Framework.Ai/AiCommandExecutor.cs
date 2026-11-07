using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Own a fresh AI decision inside Framework, then publish exactly one actual supported roster command.</summary>
/// <remarks>ZOD-314; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No cached plan or caller RNG admission.</remarks>
public static class AiCommandExecutor
{
    public static AiCommandResult Execute(RosterFacingTurnState state, RosterAiRefreshRequest request,
        AiDecisionConfiguration configuration,
        AiDispatchProfile dispatch, BattleFacing? facing = null, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration); configuration.Validate();
        ArgumentNullException.ThrowIfNull(dispatch);
        var simulation = configuration.Simulation; var profile = configuration.Abilities;
        workspace ??= AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
        workspace.ValidateMap(state.Battle); var successor = workspace;
        AiCommandFailure? failure = null;
        var result = RosterDecisionBattle.Execute<AiCommandOutcome>(state, request.ActorId, context => {
            if (!dispatch.InitializedActorContext || !dispatch.PresentCompactRecords ||
                !dispatch.CanonicalCoordinates || !dispatch.OrdinaryContinuation ||
                simulation.Battle.Ground != context.Profile.Ground || !simulation.Battle.Members.SequenceEqual(context.Profile.Members))
            { failure = new(AiCommandRejection.UnsupportedDispatchContext); return context.Reject<AiCommandOutcome>(); }
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == context.ActorId);
            var selected = AiDecisionMaker.Decide(context.Battle, request, configuration, context.RngState, workspace);
            if (!selected.Accepted)
            { failure = new(AiCommandRejection.SelectionRejected, selected.Failure); return context.Reject<AiCommandOutcome>(); }
            var plan = selected.Outcome!.Plan; var action = plan.Action;
            var origin = context.Battle.TurnBattle.Battle.Units.Single(u => u.Id == context.ActorId).Position;
            var kind = !actor.MovementTaken && (actor.ActionTaken || !plan.ActBeforeMove) && plan.Destination != origin
                ? AiCommandKind.Move : actor.ActionTaken || action is null ? AiCommandKind.End : action.Kind switch {
                    AiHumanAbilityKind.Attack => AiCommandKind.Attack,
                    AiHumanAbilityKind.Accumulate => AiCommandKind.Accumulate,
                    AiHumanAbilityKind.ThrowStone => AiCommandKind.ThrowStone,
                    AiHumanAbilityKind.Dash => AiCommandKind.Dash,
                    _ => throw new InvalidOperationException("An unsupported entry passed owned selection.") };
            var diagnostic = new AiCommandOutcome(selected.Outcome, selected.RngState, kind, actor.ActionTaken);
            successor = selected.Workspace;
            return Prepare(context, profile, plan, kind, actor.ActionTaken, facing, selected.RngState, diagnostic);
        });
        if (!result.Accepted && failure is null) failure = new(AiCommandRejection.BoundaryRejected, Boundary: result.Rejection);
        return new(failure, result, result.Accepted ? successor : workspace);
    }

    private static RosterDecision<AiCommandOutcome> Prepare(RosterDecisionContext context,
        AiAbilityEvaluationProfile profile, AiActionPlan plan, AiCommandKind kind,
        bool afterAct, BattleFacing? facing, RandomState rng, AiCommandOutcome diagnostic)
    {
        var action = plan.Action;
        if (kind == AiCommandKind.Move)
        {
            var move = new MoveUnitCommand(context.ActorId, plan.Destination);
            if (afterAct) return context.PrepareTurnEndingMove(move, facing, rng, diagnostic);
            return action?.Kind switch {
                AiHumanAbilityKind.Attack => context.PrepareMoveThenAttack(move, new(context.ActorId, action.TargetId!.Value), profile.Weapon, rng, diagnostic),
                AiHumanAbilityKind.Accumulate => context.PrepareMoveThenImmediateAbility(move, new(context.ActorId, action.AbilityId!.Value), profile.Accumulate!.Ability, rng, diagnostic),
                AiHumanAbilityKind.Dash => context.PrepareMoveThenDashAbility(move, new(context.ActorId, action.AbilityId!.Value, action.TargetId!.Value), profile.Dash!, rng, diagnostic),
                AiHumanAbilityKind.ThrowStone => context.PrepareMoveThenTargetedAbility(move, new(context.ActorId, action.AbilityId!.Value, action.TargetId!.Value),
                    profile.ThrowStone!, profile.Heights!.Shared, rng, diagnostic),
                null => context.PrepareMoveThenWait(move, facing, rng, diagnostic),
                _ => throw new InvalidOperationException("An unsupported follow-up passed owned selection.") };
        }
        return kind switch {
            AiCommandKind.Attack => context.PrepareAttack(new(context.ActorId, action!.TargetId!.Value), profile.Weapon, rng, diagnostic),
            AiCommandKind.Accumulate => context.PrepareImmediateAbility(new(context.ActorId, action!.AbilityId!.Value), profile.Accumulate!.Ability, rng, diagnostic),
            AiCommandKind.Dash => context.PrepareDashAbility(new(context.ActorId, action!.AbilityId!.Value, action.TargetId!.Value), profile.Dash!, rng, diagnostic),
            AiCommandKind.ThrowStone => context.PrepareTargetedAbility(new(context.ActorId, action!.AbilityId!.Value, action.TargetId!.Value),
                profile.ThrowStone!, profile.Heights!.Shared, rng, diagnostic),
            AiCommandKind.End => context.PrepareWait(facing, rng, diagnostic),
            _ => throw new InvalidOperationException("Unexpected actual roster command.") };
    }
}
