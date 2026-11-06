using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Framework-owned fresh planning, actual path Move and exact-session retained Attack/targeted ACT/Wait.</summary>
/// <remarks>ZOD-167/168/205. Each published command is atomic; follow-up failure never rewinds a successful Move.</remarks>
public static class FacingMoveDecisionBattle
{
    public static FacingMoveResult<T> ExecuteMove<T>(FacingTurnState state, BattleUnitId actorId,
        Func<FacingMoveDecisionContext, FacingMoveDecision<T>> planner)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(planner);
        FacingMoveResult<T> Reject(FacingMoveRejection reason, FacingTurnRejection facing = FacingTurnRejection.None,
            TurnCycleRejection turn = TurnCycleRejection.None, FlatMovementRejection path = FlatMovementRejection.None) =>
            new(reason, facing, turn, path, state, [], null, null, default);
        var guard = FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None) return Reject(FacingMoveRejection.AuthorityRejected, guard);
        var actorTurn = state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (actorTurn.MovementTaken || actorTurn.ActionTaken) return Reject(FacingMoveRejection.UnavailableChoices);
        var context = new FacingMoveDecisionContext(state.Battle, actorId);
        FacingMoveDecision<T> decision;
        try { decision = planner(context); }
        finally { context.Close(); }
        return ApplyFreshMove(state, actorId, context, decision);
    }

    // Both Framework entry points supply the context belonging to their own just-completed callback.
    // No second planner, public cached-proposal entry or new token authority is introduced.
    internal static FacingMoveResult<T> ApplyFreshMove<T>(FacingTurnState state, BattleUnitId actorId,
        FacingMoveDecisionContext context, FacingMoveDecision<T> decision)
    {
        FacingMoveResult<T> Reject(FacingMoveRejection reason, FacingTurnRejection facing = FacingTurnRejection.None,
            TurnCycleRejection turn = TurnCycleRejection.None, FlatMovementRejection path = FlatMovementRejection.None) =>
            new(reason, facing, turn, path, state, [], null, null, default);
        if (decision is null || !ReferenceEquals(decision.Owner, context)) return Reject(FacingMoveRejection.InvalidDecision);
        if (!ReferenceEquals(context.Battle, state.Battle) || context.ActorId != actorId) return Reject(FacingMoveRejection.InvalidDecision);
        if (decision.Kind == FacingMoveIntentKind.Rejected) return Reject(FacingMoveRejection.DecisionRejected);
        var actor = state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (actor.MovementTaken || actor.ActionTaken) return Reject(FacingMoveRejection.UnavailableChoices);
        if (decision.Move!.UnitId != actorId || !ValidFollowup(context.Battle, actorId, decision.Kind, decision.Attack, decision.Facing,
                decision.TargetedAbility, decision.AbilityProfile))
            return Reject(FacingMoveRejection.InvalidIntent);
        if (!RandomStateGenerator.IsForwardCheckpoint(context.RngState, decision.Rng)) return Reject(FacingMoveRejection.InvalidRandomProgress);

        var old = state.Battle.TurnBattle;
        var battle = new BattleState(old.Battle.Map, old.Battle.Units, decision.Rng);
        var turns = new TurnCycleState(old.Turns.Units, decision.Rng, old.Turns.Ticks, old.Turns.Phase);
        var staged = new FacingTurnState(new(new(battle, turns), state.Battle.Combatants), state.Profile, state.Revision, null, state.Lineage);
        var moved = FacingTurnPathMovement.MoveAlongPath(staged, decision.Move, decision.Profile!);
        if (!moved.Accepted) return Reject(FacingMoveRejection.MoveRejected, moved.FacingRejection, moved.TurnRejection, moved.PathRejection);
        var continuation = new FacingMoveContinuation(moved.State, actorId, decision.Kind, decision.Attack, decision.Weapon, decision.Facing,
            decision.TargetedAbility, decision.AbilityProfile);
        return new(FacingMoveRejection.None, FacingTurnRejection.None, TurnCycleRejection.None, FlatMovementRejection.None,
            moved.State, moved.Events, moved.Path, continuation, decision.Diagnostic);
    }

    public static FacingMoveContinueResult Continue(FacingTurnState state, BattleUnitId actorId, FacingMoveContinuation? continuation)
    {
        ArgumentNullException.ThrowIfNull(state);
        FacingMoveContinueResult Reject(FacingMoveRejection reason, FacingTurnRejection facing = FacingTurnRejection.None) =>
            new(reason, FacingTurnBattle.Reject(state, facing));
        if (continuation is null) return Reject(FacingMoveRejection.MissingContinuation);
        if (!ReferenceEquals(state, continuation.ExpectedState) || !ReferenceEquals(state.Battle, continuation.ExpectedBattle) ||
            !ReferenceEquals(state.Lineage, continuation.Lineage) || state.Revision != continuation.Revision)
            return Reject(FacingMoveRejection.StaleContinuation);
        if (actorId != continuation.ActorId) return Reject(FacingMoveRejection.WrongActor);
        var guard = FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None) return Reject(FacingMoveRejection.AuthorityRejected, guard);
        var turn = state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (!turn.MovementTaken || turn.ActionTaken) return Reject(FacingMoveRejection.UnavailableChoices);
        var result = continuation.Kind switch
        {
            FacingMoveIntentKind.Attack => FacingTurnBattle.Attack(state, continuation.Attack!, continuation.Weapon),
            FacingMoveIntentKind.TargetedAbility => FacingTurnBattle.UseTargetedAbility(state, continuation.TargetedAbility!, continuation.AbilityProfile!),
            _ => FacingTurnBattle.WaitWithFacing(state, actorId, continuation.Facing!)
        };
        return result.Accepted ? new(FacingMoveRejection.None, result)
            : new(FacingMoveRejection.CommandRejected, result with { State = state, Events = [] });
    }

    private static bool ValidFollowup(AttackBattleSnapshot state, BattleUnitId actorId, FacingMoveIntentKind kind,
        AttackUnitCommand? attack, FacingChoice? facing, UseTargetedAbilityCommand? targetedAbility, TargetedAbilityBattleProfile? abilityProfile)
    {
        bool KnownEnemy(BattleUnitId target) => target != actorId && state.Combatants.Any(u => u.UnitId == target && u.Hp > 0);
        return kind switch
        {
            FacingMoveIntentKind.Attack => attack is not null && attack.ActorId == actorId && KnownEnemy(attack.TargetId),
            // Original range may be false before Move. All other admission must already hold; real range is rechecked after Move.
            FacingMoveIntentKind.TargetedAbility => targetedAbility is not null && abilityProfile is not null &&
                targetedAbility.ActorId == actorId && KnownEnemy(targetedAbility.TargetId) &&
                TargetedAbilityBattleSimulator.ValidateCommand(state, targetedAbility, abilityProfile) is
                    AttackCommandRejection.None or AttackCommandRejection.OutOfRange,
            FacingMoveIntentKind.Wait => facing switch
            {
                FacingChoice.Fixed choice => Enum.IsDefined(choice.Facing),
                FacingChoice.KeepCurrent => true,
                FacingChoice.ChooseClosedEnemy choice => KnownEnemy(choice.EnemyId),
                _ => false
            },
            _ => false
        };
    }
}
