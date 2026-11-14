using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Framework-owned fresh planning, RNG continuity and one authoritative command transaction.</summary>
public static class FacingDecisionBattle
{
    public static FacingDecisionResult<T> Execute<T>(FacingTurnState state, BattleUnitId actorId,
        Func<FacingDecisionContext, FacingDecision<T>> planner)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(planner);
        FacingDecisionResult<T> Reject(FacingDecisionRejection reason, FacingTurnResult? failed = null,
            FlatMovementRejection path = FlatMovementRejection.None) =>
            new(reason, failed is null ? FacingTurnBattle.Reject(state, FacingTurnRejection.None)
                : failed with { State = state, Events = [] }, default) { PathRejection = path };
        var guard = FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, guard));
        var context = new FacingDecisionContext(state.Battle, actorId);
        FacingDecision<T> decision;
        try { decision = planner(context); }
        finally { context.Close(); }
        if (decision is null || !ReferenceEquals(decision.Owner, context)) return Reject(FacingDecisionRejection.InvalidDecision);
        if (decision.Kind == FacingDecisionKind.Rejected) return Reject(FacingDecisionRejection.DecisionRejected);
        if (decision.Kind == FacingDecisionKind.PlannedMove)
        {
            // The same private planner issued this Move proposal. Reuse existing validation/adoption/path/token machinery.
            var moved = FacingMoveDecisionBattle.ApplyFreshMove(state, actorId, context.MoveContext, decision.PlannedMove!);
            var transition = new FacingTurnResult(moved.Accepted, moved.FacingRejection, AttackCommandRejection.None,
                moved.TurnRejection, BattleTransitionRejection.None, moved.State, moved.Events);
            var rejection = moved.Rejection switch
            {
                FacingMoveRejection.None => FacingDecisionRejection.None,
                FacingMoveRejection.InvalidRandomProgress => FacingDecisionRejection.InvalidRandomProgress,
                FacingMoveRejection.InvalidDecision => FacingDecisionRejection.InvalidDecision,
                FacingMoveRejection.UnavailableChoices => FacingDecisionRejection.UnavailableChoices,
                _ => FacingDecisionRejection.CommandRejected
            };
            return new(rejection, transition, moved.Diagnostic) { Path = moved.Path, PathRejection = moved.PathRejection,
                Continuation = moved.Continuation, MoveRejection = moved.Rejection };
        }
        if (!RandomStateGenerator.IsForwardCheckpoint(context.RngState, decision.Rng)) return Reject(FacingDecisionRejection.InvalidRandomProgress);
        if (decision.Kind == FacingDecisionKind.Attack && decision.Attack!.ActorId != actorId)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
        if (decision.Kind == FacingDecisionKind.TargetedAbility && decision.TargetedAbility!.ActorId != actorId)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
        if (decision.Kind == FacingDecisionKind.ImmediateAbility && decision.ImmediateAbility!.ActorId != actorId)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
        if (decision.Kind == FacingDecisionKind.TurnEndingMove)
        {
            if (decision.Move!.UnitId != actorId)
                return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
            if (actor.MovementTaken || !actor.ActionTaken) return Reject(FacingDecisionRejection.UnavailableChoices);
        }

        var old = state.Battle.TurnBattle;
        var battle = new BattleState(old.Battle.Map, old.Battle.Units, decision.Rng);
        var turns = new TurnCycleState(old.Turns.Units, decision.Rng, old.Turns.Ticks, old.Turns.Phase);
        var staged = new FacingTurnState(new(new(battle, turns), state.Battle.Combatants), state.Profile, state.Revision, null, state.Lineage);
        if (decision.Kind == FacingDecisionKind.TurnEndingMove)
        {
            var moved = FacingTurnPathMovement.MoveAlongPath(staged, decision.Move!, decision.MovementProfile!);
            if (!moved.Accepted)
                return Reject(FacingDecisionRejection.CommandRejected,
                    new(false, moved.FacingRejection, AttackCommandRejection.None, moved.TurnRejection,
                        BattleTransitionRejection.None, state, []), moved.PathRejection);
            // FinishFacing verifies the actual receipt, ended actor, exact battle, lineage and revision.
            var finished = FacingTurnBattle.FinishFacing(moved.State, actorId, moved.State.PendingFacing, decision.Facing!);
            if (!finished.Accepted) return Reject(FacingDecisionRejection.CommandRejected, finished);
            return new(FacingDecisionRejection.None, finished with { Events = moved.Events.AddRange(finished.Events) }, decision.Diagnostic)
                { Path = moved.Path };
        }
        var result = decision.Kind switch
        {
            FacingDecisionKind.Attack => FacingTurnBattle.Attack(staged, decision.Attack!, decision.Weapon),
            FacingDecisionKind.TargetedAbility => FacingTurnBattle.UseTargetedAbility(staged, decision.TargetedAbility!, decision.AbilityProfile!),
            FacingDecisionKind.ImmediateAbility => FacingTurnBattle.UseImmediateAbility(staged, decision.ImmediateAbility!, decision.ImmediateProfile!),
            _ => FacingTurnBattle.WaitWithFacing(staged, actorId, decision.Facing!)
        };
        return result.Accepted ? new(FacingDecisionRejection.None, result, decision.Diagnostic)
            : Reject(FacingDecisionRejection.CommandRejected, result);
    }
}
