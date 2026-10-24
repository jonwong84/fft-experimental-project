using System.Collections.Frozen;

namespace Zodiac.Engine;

/// <summary>
/// Calculates movement destinations on an unobstructed flat grid.
/// </summary>
/// <remarks>
/// This narrow model does not account for height, terrain costs, obstacles, occupancy,
/// jumps, status effects, or transitions between layers. The origin is excluded, even with a positive budget.
/// </remarks>
public static class FlatGridMovementRange
{
    /// <summary>Returns an immutable set of destinations within the nonnegative step budget.</summary>
    /// <remarks>Only set membership is contractual; enumeration order is unspecified.</remarks>
    public static IReadOnlySet<GridPosition> Calculate(GridBounds bounds, GridPosition origin, int move)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentOutOfRangeException.ThrowIfNegative(move);

        if (!bounds.Contains(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Origin must be inside the bounds.");
        }

        var visited = new HashSet<GridPosition> { origin };
        var destinations = new HashSet<GridPosition>();
        var frontier = new Queue<(GridPosition Position, int Distance)>();
        frontier.Enqueue((origin, 0));

        while (frontier.TryDequeue(out var current))
        {
            if (current.Distance >= move)
            {
                continue;
            }

            if (current.Position.X > 0)
            {
                Visit(new GridPosition(current.Position.X - 1, current.Position.Y, origin.Layer));
            }

            if (current.Position.X < bounds.Width - 1)
            {
                Visit(new GridPosition(current.Position.X + 1, current.Position.Y, origin.Layer));
            }

            if (current.Position.Y > 0)
            {
                Visit(new GridPosition(current.Position.X, current.Position.Y - 1, origin.Layer));
            }

            if (current.Position.Y < bounds.Depth - 1)
            {
                Visit(new GridPosition(current.Position.X, current.Position.Y + 1, origin.Layer));
            }

            void Visit(GridPosition position)
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
