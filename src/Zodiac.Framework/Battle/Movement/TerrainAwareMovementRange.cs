using System.Collections.Frozen;

namespace Zodiac.Framework;

/// <summary>
/// Calculates a flat, same-layer movement range using the effective terrain costs in a unit profile.
/// </summary>
/// <remarks>
/// This is a deliberately bounded slice of FFT movement propagation. It accounts for cardinal
/// neighbors, the cost of entering each tile, blocked terrain, and tile flags that prevent stopping.
/// It does not model
/// slopes, height, jumps, occupancy, water depth, movement modes, or cross-layer movement.
/// </remarks>
public static class TerrainAwareMovementRange
{
    public static IReadOnlySet<TilePosition> Calculate(
        BattleMapDefinition map,
        TilePosition origin,
        UnitMovementProfile movementProfile) => Calculate(map, origin, movementProfile, null);

    /// <summary>Calculates range with explicitly supplied tiles that prevent traversal.</summary>
    public static IReadOnlySet<TilePosition> Calculate(
        BattleMapDefinition map,
        TilePosition origin,
        UnitMovementProfile movementProfile,
        IReadOnlySet<TilePosition>? traversalBlocked) => Calculate(map, origin, movementProfile, traversalBlocked, false);

    internal static IReadOnlySet<TilePosition> Calculate(
        BattleMapDefinition map, TilePosition origin, UnitMovementProfile movementProfile,
        IReadOnlySet<TilePosition>? traversalBlocked, bool useAdjacentGeometry)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(movementProfile);

        if (!map.TryGetTile(origin, out _))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Origin must be inside the map.");
        }

        // The original routine seeds remaining_range to Move + 1, subtracts the
        // destination terrain cost, and rejects a result <= 0. This is equivalent
        // to allowing total entered-tile cost <= Move.
        var bestCost = new Dictionary<TilePosition, int> { [origin] = 0 };
        var pending = new PriorityQueue<TilePosition, int>();
        pending.Enqueue(origin, 0);

        while (pending.TryDequeue(out TilePosition current, out int currentCost))
        {
            if (!bestCost.TryGetValue(current, out int recordedCost) || currentCost != recordedCost)
            {
                continue;
            }

            Visit(current.X - 1, current.Y);
            Visit(current.X + 1, current.Y);
            Visit(current.X, current.Y - 1);
            Visit(current.X, current.Y + 1);

            void Visit(int x, int y)
            {
                var destination = new TilePosition(x, y, origin.Layer);
                if (!map.TryGetTile(destination, out BattleTile tile) || tile.IsBlocked
                    || traversalBlocked?.Contains(destination) == true)
                {
                    return;
                }

                if (!movementProfile.TryGetTerrainCost(tile.Surface, out TerrainMovementCost terrainCost))
                {
                    throw new InvalidOperationException(
                        $"Movement profile has no cost for terrain surface '{tile.Surface}'.");
                }

                if (terrainCost.IsBlocked)
                {
                    return;
                }

                if (useAdjacentGeometry)
                {
                    map.TryGetTile(current, out BattleTile sourceTile);
                    if (!OrdinaryAdjacentStepRange.CanStep(sourceTile, tile, current, destination, movementProfile.Jump))
                        return;
                }

                int nextCost = checked(currentCost + terrainCost.MovePoints);
                if (nextCost > movementProfile.Move
                    || (bestCost.TryGetValue(destination, out int oldCost) && oldCost <= nextCost))
                {
                    return;
                }

                bestCost[destination] = nextCost;
                pending.Enqueue(destination, nextCost);
            }
        }

        var destinations = new HashSet<TilePosition>();
        foreach (TilePosition position in bestCost.Keys)
        {
            if (position != origin
                && map.TryGetTile(position, out BattleTile tile)
                && !tile.CannotStop
                && !tile.IsUntargetable)
            {
                destinations.Add(position);
            }
        }

        return destinations.ToFrozenSet();
    }
}
