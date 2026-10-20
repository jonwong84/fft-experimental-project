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

        var visited = new HashSet<TilePosition> { origin };
        var destinations = new HashSet<TilePosition>();
        var frontier = new Queue<(TilePosition Position, int Distance)>();
        frontier.Enqueue((origin, 0));

        while (frontier.TryDequeue(out var current))
        {
            if (current.Distance >= move)
            {
                continue;
            }

            if (current.Position.X > 0)
            {
                Visit(new TilePosition(current.Position.X - 1, current.Position.Y, origin.Layer));
            }

            if (current.Position.X < map.Width - 1)
            {
                Visit(new TilePosition(current.Position.X + 1, current.Position.Y, origin.Layer));
            }

            if (current.Position.Y > 0)
            {
                Visit(new TilePosition(current.Position.X, current.Position.Y - 1, origin.Layer));
            }

            if (current.Position.Y < map.Depth - 1)
            {
                Visit(new TilePosition(current.Position.X, current.Position.Y + 1, origin.Layer));
            }

            void Visit(TilePosition position)
            {
                if (visited.Add(position))
                {
                    destinations.Add(position);
                    frontier.Enqueue((position, current.Distance + 1));
                }
            }
        }

        return destinations.ToFrozenSet();
    }
}