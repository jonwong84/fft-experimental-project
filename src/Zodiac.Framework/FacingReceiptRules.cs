namespace Zodiac.Framework;

/// <summary>Shared exact-result receipt guards for separately typed pair and roster sessions.</summary>
internal static class FacingReceiptRules
{
    internal static FacingTurnRejection ValidateWait(AttackBattleSnapshot battle, FacingCompletionReceipt? pending,
        ulong revision, BattleUnitId actorId)
    {
        if (pending is not null) return FacingTurnRejection.FacingPending;
        if (!battle.Combatants.Any(u => u.UnitId == actorId)) return FacingTurnRejection.UnknownActor;
        if (battle.TurnBattle.Turns.ActiveUnitId != actorId) return FacingTurnRejection.InactiveActor;
        return revision == ulong.MaxValue ? FacingTurnRejection.RevisionExhausted : FacingTurnRejection.None;
    }

    internal static FacingTurnRejection ValidateCompletion(AttackBattleSnapshot battle, FacingCompletionReceipt? pending,
        ulong revision, object lineage, BattleUnitId actorId, FacingCompletionReceipt? receipt)
    {
        if (pending is null) return FacingTurnRejection.MissingCompletion;
        if (!ReferenceEquals(receipt, pending) || !ReferenceEquals(pending.ExpectedBattle, battle) ||
            !ReferenceEquals(pending.Lineage, lineage) || pending.Revision != revision) return FacingTurnRejection.StaleCompletion;
        if (actorId != pending.ActorId) return FacingTurnRejection.WrongActor;
        if (battle.TurnBattle.Turns.ActiveUnitId is not null) return FacingTurnRejection.InconsistentCompletion;
        return revision == ulong.MaxValue ? FacingTurnRejection.RevisionExhausted : FacingTurnRejection.None;
    }
}
