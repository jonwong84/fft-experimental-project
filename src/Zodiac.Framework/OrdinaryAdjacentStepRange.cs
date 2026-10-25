namespace Zodiac.Framework;

/// <summary>Dry, non-steep same-layer geometry combined with the existing terrain-cost prototype.</summary>
/// <remarks>Unsupported inputs raise NotSupportedException; this is not full PS1 movement.</remarks>
public static class OrdinaryAdjacentStepRange
{
    public static IReadOnlySet<TilePosition> Calculate(BattleMapDefinition map, TilePosition origin,
        UnitMovementProfile profile, IReadOnlySet<TilePosition>? traversalBlocked = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(profile);
        if (!map.TryGetTile(origin, out var originTile))
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (profile.Mode != UnitMovementMode.Normal || profile.Traits != UnitMovementTraits.None
            || profile.BodyHeight != 6 || profile.Jump > byte.MaxValue)
            throw new NotSupportedException("Adjacent-step prototype requires Normal mode, no traits, body height 6 and byte-range Jump.");
        if (originTile.IsBlocked)
            throw new NotSupportedException("Adjacent-step prototype requires an unblocked origin.");

        // Restrict the map so ceiling sampling resolves to the source's 0x7f sentinel.
        // Other-layer ceilings and layer transitions require a separately traced policy.
        for (int layer = 0; layer < map.LayerCount; layer++)
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, layer), out var tile);
            if (tile.IsBlocked) continue;
            if (layer != origin.Layer)
                throw new NotSupportedException("Adjacent-step prototype requires every other layer to be blocked.");
            ValidateTile(tile, profile.Jump, new(x, y, layer));
        }
        return TerrainAwareMovementRange.Calculate(map, origin, profile, traversalBlocked, useAdjacentGeometry: true);
    }

    private static void ValidateTile(BattleTile tile, int jump, TilePosition position)
    {
        byte slope = tile.Slope.ToPackedByte();
        if (tile.WaterDepth != 0 || tile.CeilingDepth != 0
            || slope is not (0x00 or 0x25 or 0x52 or 0x58 or 0x85)
            || tile.HalfHeight > 3 || tile.HalfHeight > Math.Min(jump, 7)
            || (slope == 0 && tile.HalfHeight != 0))
            throw new NotSupportedException($"Unsupported water, ceiling or steep/corner slope at {position}.");
        int maxEdge = Math.Max(Math.Max(tile.Slope.East, tile.Slope.West), Math.Max(tile.Slope.North, tile.Slope.South));
        // Long arithmetic validates managed values before performing the original bounded arithmetic.
        if (2L * tile.Height + (long)tile.HalfHeight * maxEdge > 121)
            throw new NotSupportedException($"Adjacent-step prototype requires every edge height <= 121 at {position}.");
    }

    internal static bool CanStep(BattleTile source, BattleTile destination, TilePosition from, TilePosition to, int jump)
    {
        // Caller enumerates only same-layer cardinal neighbors and validates the safe numeric domain.
        (int exit, int entry) = (to.X - from.X, to.Y - from.Y) switch
        {
            (1, 0) => (source.Slope.East, destination.Slope.West),
            (-1, 0) => (source.Slope.West, destination.Slope.East),
            (0, 1) => (source.Slope.North, destination.Slope.South),
            (0, -1) => (source.Slope.South, destination.Slope.North),
            _ => throw new ArgumentException("Expected a cardinal adjacent step."),
        };
        int sourceHeight = source.Height * 2 + source.HalfHeight * exit;
        int destinationHeight = destination.Height * 2 + destination.HalfHeight * entry;
        return Math.Abs(destinationHeight - sourceHeight) <= 2 * Math.Min(jump, 7);
    }
}
