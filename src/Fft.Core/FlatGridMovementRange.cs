using System.Collections.Frozen;

namespace Fft.Core;

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

        var bounds = new Zodiac.Engine.GridBounds(map.Width, map.Depth, map.LayerCount);
        var engineOrigin = new Zodiac.Engine.GridPosition(origin.X, origin.Y, origin.Layer);
        return Zodiac.Engine.FlatGridMovementRange.Calculate(bounds, engineOrigin, move)
            .Select(position => new TilePosition(position.X, position.Y, position.Layer))
            .ToFrozenSet();
    }
}