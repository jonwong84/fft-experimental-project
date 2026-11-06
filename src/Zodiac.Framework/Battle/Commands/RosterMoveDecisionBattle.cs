namespace Zodiac.Framework;

/// <summary>Execute only the frozen command belonging to one exact actual roster Move result. No planner, replacement payload or RNG adoption.</summary>
/// <remarks>ZOD-310/313; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Failed follow-up never rewinds an accepted Move.</remarks>
public static class RosterMoveDecisionBattle
{
    public static RosterMoveContinueResult Continue(RosterFacingTurnState state, BattleUnitId actorId, RosterMoveContinuation? continuation)
    {
        ArgumentNullException.ThrowIfNull(state);
        RosterMoveContinueResult Reject(FacingMoveRejection reason, FacingTurnRejection facing = FacingTurnRejection.None) =>
            new(reason, new(false, facing, AttackCommandRejection.None, TurnCycleRejection.None, FlatMovementRejection.None, state, []));
        if (continuation is null) return Reject(FacingMoveRejection.MissingContinuation);
        if (!ReferenceEquals(state, continuation.ExpectedState) || !ReferenceEquals(state.Battle, continuation.ExpectedBattle) ||
            !ReferenceEquals(state.Lineage, continuation.Lineage) || state.Revision != continuation.Revision)
            return Reject(FacingMoveRejection.StaleContinuation);
        if (actorId != continuation.ActorId) return Reject(FacingMoveRejection.WrongActor);
        var guard = FacingReceiptRules.ValidateWait(state.Battle, state.PendingFacing, state.Revision, actorId);
        if (guard != FacingTurnRejection.None) return Reject(FacingMoveRejection.AuthorityRejected, guard);
        var turn = state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (!turn.MovementTaken || turn.ActionTaken) return Reject(FacingMoveRejection.UnavailableChoices);
        var applied = continuation.Followup(state);
        return applied.Accepted ? new(FacingMoveRejection.None, applied)
            : new(FacingMoveRejection.CommandRejected, applied with { State = state, Events = [], Path = null });
    }
}
