namespace Zodiac.Framework.Ai;

/// <summary>Internal projection after existing ordinary-opponent admission; it does not admit a new state domain.</summary>
internal static class AiOrdinaryPairRoster
{
    internal static AiTargetRoster Create(AttackBattleSnapshot state, BattleUnitId actorId, BattleUnitId targetId)
    {
        // Existing candidate certificates establish original slot order in TieKey and no status/control/mount effects.
        var turns = state.TurnBattle.Turns.Units;
        var actor = turns.Single(u => u.Id == actorId);
        var units = turns.Select(turn => {
            var combat = state.Combatants.Single(u => u.UnitId == turn.Id);
            var body = state.TurnBattle.Battle.Units.Single(u => u.Id == turn.Id);
            return new AiRosterUnit(turn.Id, turn.TieKey, body.Position,
                new(Dead: combat.Status == AttackUnitStatus.Dead, Critical: combat.Status == AttackUnitStatus.Critical),
                new(Enemy: turn.Id == targetId, DeadWithoutReraise: combat.Status == AttackUnitStatus.Dead, Considered: true),
                AiRosterEligibility.OrdinaryTurnDelay((byte)turn.Ct, (byte)turn.Speed), disabledThroughActorDelay: false);
        });
        return new(actorId, (byte)AiRosterEligibility.OrdinaryTurnDelay((byte)actor.Ct, (byte)actor.Speed), units);
    }
}
