using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Own one fresh planning invocation, verify full-word/count RNG continuity and atomically publish one real roster command.</summary>
/// <remarks>ZOD-310/311; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No cached proposal executor, AI reference or new friend access.</remarks>
public static class RosterDecisionBattle
{
    public static RosterDecisionResult<T> Execute<T>(RosterFacingTurnState state, BattleUnitId actorId,
        Func<RosterDecisionContext, RosterDecision<T>> planner)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(planner);
        RosterDecisionResult<T> Reject(FacingDecisionRejection reason, RosterFacingResult? failure = null) =>
            new(reason, new(false, failure?.FacingRejection ?? FacingTurnRejection.None, failure?.AttackRejection ?? AttackCommandRejection.None,
                failure?.TurnRejection ?? TurnCycleRejection.None, failure?.PathRejection ?? FlatMovementRejection.None, state, []), default);
        RosterFacingResult GuardFailure(FacingTurnRejection reason) => new(false, reason, AttackCommandRejection.None, TurnCycleRejection.None, FlatMovementRejection.None, state, []);
        var guard = FacingReceiptRules.ValidateWait(state.Battle, state.PendingFacing, state.Revision, actorId);
        if (guard != FacingTurnRejection.None) return Reject(FacingDecisionRejection.AuthorityRejected, GuardFailure(guard));
        var context = new RosterDecisionContext(state, actorId); RosterDecision<T> decision;
        try { decision = planner(context); } finally { context.Close(); }
        if (decision is null || !ReferenceEquals(decision.Owner, context)) return Reject(FacingDecisionRejection.InvalidDecision);
        if (decision.Application is null) return Reject(FacingDecisionRejection.DecisionRejected);
        if (decision.ActorId != actorId) return Reject(FacingDecisionRejection.AuthorityRejected, GuardFailure(FacingTurnRejection.WrongActor));
        if (!RandomStateGenerator.IsForwardCheckpoint(context.RngState, decision.Rng)) return Reject(FacingDecisionRejection.InvalidRandomProgress);
        var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (decision.Choices == RosterDecisionChoices.BothUnused && (actor.MovementTaken || actor.ActionTaken) ||
            decision.Choices == RosterDecisionChoices.ActUsedMoveUnused && (actor.MovementTaken || !actor.ActionTaken))
            return Reject(FacingDecisionRejection.UnavailableChoices);
        var admission = decision.Admission?.Invoke(context.Battle) ?? AttackCommandRejection.None;
        if (admission != AttackCommandRejection.None) return Reject(FacingDecisionRejection.CommandRejected,
            new(false, FacingTurnRejection.None, admission, TurnCycleRejection.None, FlatMovementRejection.None, state, []));
        var old = state.Battle.TurnBattle;
        var battle = new AttackBattleSnapshot(new(new(old.Battle.Map, old.Battle.Units, decision.Rng),
            new(old.Turns.Units, decision.Rng, old.Turns.Ticks, old.Turns.Phase)), state.Battle.Combatants);
        var staged = new RosterFacingTurnState(battle, state.Profile, state.Revision, null, state.Lineage);
        var applied = decision.Application(staged);
        return applied.Accepted ? applied : Reject(applied.Rejection, applied.Transition);
    }
}
