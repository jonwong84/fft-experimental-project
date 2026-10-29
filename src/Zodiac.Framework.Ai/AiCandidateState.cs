using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Private two-unit candidate preparation shared by fresh ordinary Attack and Throw Stone projections.</summary>
internal static class AiCandidateState
{
    // Owned callers certify the same living baseline, distinct actor/target and ordinary effect-free profile.
    internal static AiProjectedUnit[] CreateUnits(AttackBattleSnapshot state, BattleUnitId actorId, TilePosition destination,
        int targetHp, int turnEndCt, bool moved, ImmutableArray<AiUnitBaseline> baseline)
    {
        var battle = state.TurnBattle.Battle;
        return state.TurnBattle.Turns.Units.OrderBy(u => u.TieKey).Select(turn =>
        {
            var combat = state.Combatants.Single(u => u.UnitId == turn.Id);
            battle.TryGetUnit(turn.Id, out var body);
            bool actor = turn.Id == actorId;
            int hp = actor ? combat.Hp : targetHp;
            var status = hp == 0 ? AttackUnitStatus.Dead : hp <= combat.MaxHp / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None;
            return new AiProjectedUnit(turn.Id, actor ? destination : body!.Position, combat.Facing,
                hp, combat.MaxHp, status, hp == 0 ? (byte)3 : baseline.Single(u => u.UnitId == turn.Id).DeathCounter,
                turn.Speed, turn.TieKey, actor ? turnEndCt : turn.Ct,
                actor ? moved : turn.MovementTaken, actor || turn.ActionTaken);
        }).ToArray();
    }
}
