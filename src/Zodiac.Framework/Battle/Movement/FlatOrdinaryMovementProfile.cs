using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Explicit certification of stable opponents and ordinary ground for the closed movement query.</summary>
/// <remarks>
/// The caller certifies no mounts, stepping-stone flags, traps, pickups or unrepresented movement/status/team
/// effects. IDs do not imply teams or original terrain types. Both effective terrain costs are also checked.
/// </remarks>
public sealed class FlatOrdinaryMovementProfile
{
    public TerrainSurfaceId Ground { get; }
    public BattleUnitId FirstOpponent { get; }
    public BattleUnitId SecondOpponent { get; }

    public FlatOrdinaryMovementProfile(TerrainSurfaceId ground, BattleUnitId firstOpponent, BattleUnitId secondOpponent)
    {
        if (string.IsNullOrWhiteSpace(ground.Value)) throw new ArgumentException("Explicit ground certification required.", nameof(ground));
        if (string.IsNullOrWhiteSpace(firstOpponent.Value) || string.IsNullOrWhiteSpace(secondOpponent.Value)
            || firstOpponent == secondOpponent) throw new ArgumentException("Certify two distinct stable opponents.");
        Ground = ground; FirstOpponent = firstOpponent; SecondOpponent = secondOpponent;
    }

    /// <summary>Pure geometry/profile admission, without granting a movement transition.</summary>
    public bool Supports(AttackBattleSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var battle = state.TurnBattle.Battle;
        var map = battle.Map;
        if (map.Width > 16 || map.Depth > 16 || map.LayerCount != 1 || battle.Units.Count != 2
            || !battle.TryGetUnit(FirstOpponent, out var first) || !battle.TryGetUnit(SecondOpponent, out var second)
            || first!.Position == second!.Position || state.Combatants.Any(unit => unit.Hp == 0)) return false;
        foreach (var unit in battle.Units)
        {
            var movement = unit.MovementProfile;
            if (movement.Move > byte.MaxValue || movement.Jump > byte.MaxValue || movement.BodyHeight != 6
                || movement.Mode != UnitMovementMode.Normal || movement.Traits != UnitMovementTraits.None
                || !movement.TryGetTerrainCost(Ground, out var cost) || cost.IsBlocked || cost.MovePoints != 1) return false;
        }
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, 0), out var tile);
            if (tile.Surface != Ground || tile.Height != 10 || tile.HalfHeight != 0 || tile.WaterDepth != 0
                || tile.CeilingDepth != 0 || tile.Slope.ToPackedByte() != 0 || tile.MovementFlags != BattleTileMovementFlags.None) return false;
        }
        return true;
    }
}
