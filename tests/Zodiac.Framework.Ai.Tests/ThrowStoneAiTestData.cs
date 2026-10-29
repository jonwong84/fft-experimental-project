using System.Text.Json;

namespace Zodiac.Framework.Ai.Tests;

internal static class ThrowStoneAiTestData
{
    internal static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    internal static readonly AssetId Stone = new("sample:ability/throw-stone");
    internal static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    internal static readonly FftRandomState Decision = new(1, 7), Live = new(57, 100);
    internal static TargetedAbilityBattleProfile AbilityProfile(bool granted = true) => new(Ground, [new(Stone)],
        [new(A, granted ? [Stone] : [], 0), new(B, [Stone], 0)]);
    internal static AttackBattleSnapshot State(TilePosition? target = null, int pa = 5, int hp = 50, int maxHp = 50,
        int evade = 0, BattleFacing facing = BattleFacing.West, bool moved = false, bool acted = false,
        bool active = true, bool second = false, bool reverse = false, bool badGround = false,
        int move = 3, int ct = 15, int targetCt = 95, ulong ticks = 20)
    {
        var actor = second ? B : A; var enemy = second ? A : B;
        var movement = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        BattleUnitState[] bodies = [new(actor, new(1, 1, 0), movement, moved), new(enemy, target ?? new(4, 1, 0), movement, true)];
        TurnUnitState[] turns = [new(actor, 5, ct, 2, active, moved, acted), new(enemy, 5, targetCt, 17, false, true, true)];
        // No weapon is required by this ability; current PA belongs to the battle snapshot.
        AttackUnitState[] combat = [new(actor, 50, 50, 0, 0, pa, 0, 0, 0, 0, 70, 0, BattleFacing.East, AttackUnitStatus.None, 0, null),
            new(enemy, hp, maxHp, 0, 0, 5, evade, 0, 0, 0, 70, 0, facing,
                hp == 0 ? AttackUnitStatus.Dead : hp <= maxHp / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, null)];
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), 64).ToArray();
        if (badGround) tiles[^1] = new(Ground, 11);
        return new(new(new(new(8, 8, 1, tiles), reverse ? bodies.Reverse() : bodies, Live),
            new(reverse ? turns.Reverse() : turns, Live, ticks, active ? TurnCyclePhase.UnitTurn : TurnCyclePhase.PickUnit)),
            reverse ? combat.Reverse() : combat);
    }
    internal static UseTargetedAbilityCommand Command(bool second = false) => new(second ? B : A, Stone, second ? A : B);
    internal static string Serialize(object? value) => JsonSerializer.Serialize(value);
    internal static string Snapshot(AttackBattleSnapshot state) => Serialize(new {
        Combat = state.Combatants.OrderBy(u => u.UnitId.Value),
        Turns = state.TurnBattle.Turns.Units.OrderBy(u => u.Id.Value), state.TurnBattle.Turns.Phase, state.TurnBattle.Turns.Ticks,
        state.TurnBattle.Turns.RngState, BattleRng = state.TurnBattle.Battle.RngState, state.TurnBattle.Battle.Map,
        Bodies = state.TurnBattle.Battle.Units.OrderBy(u => u.Id.Value).Select(u => new { u.Id, u.Position, u.HasMoved, u.MovementProfile.Move }) });
}
