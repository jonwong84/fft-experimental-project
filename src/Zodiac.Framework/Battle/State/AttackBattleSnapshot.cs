using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Immutable attack state; death scheduling remains outside the bounded simulator.</summary>
public sealed class AttackBattleSnapshot
{
    public TurnBattleState TurnBattle { get; }
    public ImmutableArray<AttackUnitState> Combatants { get; }

    public AttackBattleSnapshot(TurnBattleState turnBattle, IEnumerable<AttackUnitState> combatants)
    {
        ArgumentNullException.ThrowIfNull(turnBattle);
        ArgumentNullException.ThrowIfNull(combatants);
        Combatants = combatants.ToImmutableArray();
        if (Combatants.Length != turnBattle.Battle.Units.Count) throw new ArgumentException("Every battle unit requires exactly one combat projection.");
        var seen = new HashSet<BattleUnitId>();
        foreach (AttackUnitState unit in Combatants)
        {
            ArgumentNullException.ThrowIfNull(unit);
            if (!seen.Add(unit.UnitId) || !turnBattle.Battle.TryGetUnit(unit.UnitId, out _))
                throw new ArgumentException($"Duplicate or unknown combat unit '{unit.UnitId}'.");
            if (unit.Status == AttackUnitStatus.Dead && turnBattle.Turns.ActiveUnitId == unit.UnitId)
                throw new ArgumentException("A dead unit cannot own the active turn.");
        }
        if (Combatants.Select(unit => unit.ZodiacSign).Distinct().Count() != 1)
            throw new NotSupportedException("The bounded snapshot requires the same zodiac sign for neutral compatibility.");
        TurnBattle = turnBattle;
    }

    public AttackBattleSnapshot WithTurnBattle(TurnBattleState state) => new(state, Combatants);

    /// <summary>Startup domain checks, distinct from representable Dead/Critical result snapshots.</summary>
    public void ValidateInitialSetup()
    {
        if (Combatants.Any(unit => unit.Status != AttackUnitStatus.None))
            throw new NotSupportedException("Initial attack setup requires healthy units; result snapshots may represent Dead/Critical.");
        ValidateSupportedGeometry();
    }

    internal void ValidateSupportedGeometry()
    {
        var positions = new HashSet<TilePosition>();
        foreach (BattleUnitState unit in TurnBattle.Battle.Units)
        {
            if (!positions.Add(unit.Position)) throw new ArgumentException("Attack setup cannot overlap units.");
            if (unit.MovementProfile.BodyHeight != 6 || unit.MovementProfile.Mode != UnitMovementMode.Normal || unit.MovementProfile.Traits != UnitMovementTraits.None)
                throw new NotSupportedException("Attack setup requires ordinary body height 6 and no movement modifiers.");
            TurnBattle.Battle.Map.TryGetTile(unit.Position, out BattleTile origin);
            if (origin.IsBlocked || origin.IsUntargetable || origin.CannotStop || unit.Position.Layer != 0)
                throw new NotSupportedException("Attack units require targetable, stoppable lower-layer starting tiles.");
        }
        var map = TurnBattle.Battle.Map;
        for (int layer = 0; layer < map.LayerCount; layer++)
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, layer), out BattleTile tile);
            if (!tile.IsBlocked && (layer != 0 || tile.Height != 10 || tile.HalfHeight != 0 || tile.WaterDepth != 0 || tile.CeilingDepth != 0 || tile.Slope.ToPackedByte() != 0))
                throw new NotSupportedException("Attack setup currently requires the traced dry flat lower-layer height-10 map.");
        }
    }
}
