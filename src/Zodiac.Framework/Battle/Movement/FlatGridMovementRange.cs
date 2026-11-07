using System.Collections.Frozen;

namespace Zodiac.Framework;

/// <summary>
/// Calculates movement destinations on an unobstructed flat grid.
/// </summary>
/// <remarks>
/// This narrow model does not account for height, terrain costs, obstacles, occupancy,
/// jumps, status effects, or transitions between layers.
/// </remarks>
public static class FlatGridMovementRange
{
    public static IReadOnlySet<TilePosition> Calculate(BattleMapDefinition map, TilePosition origin, int move)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentOutOfRangeException.ThrowIfNegative(move);

        if (!map.TryGetTile(origin, out _))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Origin must be inside the map.");
        }

        var bounds = new Tessera.Engine.GridBounds(map.Width, map.Depth, map.LayerCount);
        var engineOrigin = new Tessera.Engine.GridPosition(origin.X, origin.Y, origin.Layer);
        return Tessera.Engine.FlatGridMovementRange.Calculate(bounds, engineOrigin, move)
            .Select(position => new TilePosition(position.X, position.Y, position.Layer))
            .ToFrozenSet();
    }
}