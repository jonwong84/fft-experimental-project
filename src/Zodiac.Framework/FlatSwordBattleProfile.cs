namespace Zodiac.Framework;

/// <summary>Explicit opt-in for ordinary sword critical displacement on open, dry, flat ground.</summary>
/// <remarks>
/// The caller certifies the surface as ordinary ground and selects the existing ordinary-unit,
/// neutral empty-proc sword projection (no immortality, importance-3, mounts or reactions).
/// Raw original terrain/team flags are not inferred from IDs. No scenario or save activation.
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-114/115.
/// </remarks>
public sealed class FlatSwordBattleProfile
{
    public const string RulesId = "fft:rules/sword-flat-knockback-v1";
    public TerrainSurfaceId OrdinaryGroundSurface { get; }

    public FlatSwordBattleProfile(TerrainSurfaceId ordinaryGroundSurface)
    {
        if (string.IsNullOrWhiteSpace(ordinaryGroundSurface.Value))
            throw new ArgumentException("Select an ordinary ground surface explicitly.", nameof(ordinaryGroundSurface));
        OrdinaryGroundSurface = ordinaryGroundSurface;
    }

    /// <summary>Validate the closed combat geometry; this does not construct state or execute a command.</summary>
    public void ValidateState(AttackBattleSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.ValidateSupportedGeometry();
        if (state.Combatants.Length != 2)
            throw new NotSupportedException("Flat sword displacement requires exactly two ordinary units.");
        var map = state.TurnBattle.Battle.Map;
        if (map.LayerCount != 1)
            throw new NotSupportedException("Flat sword displacement requires one open lower-layer rectangle.");
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, 0), out var tile);
            if (tile.Surface != OrdinaryGroundSurface || tile.MovementFlags != BattleTileMovementFlags.None ||
                tile.Height != 10 || tile.HalfHeight != 0 || tile.WaterDepth != 0 ||
                tile.CeilingDepth != 0 || tile.Slope.ToPackedByte() != 0)
                throw new NotSupportedException("Flat sword displacement requires clear, dry, flat ordinary ground throughout the map.");
        }
    }
}

/// <summary>Settled forced displacement; never consumes the target's voluntary movement choice.</summary>
public sealed record SwordKnockbackEvent(BattleUnitId UnitId, TilePosition Before, TilePosition After) : AttackResultEvent;
