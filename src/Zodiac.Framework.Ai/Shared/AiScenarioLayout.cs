using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

internal sealed record AiScenarioCell(TilePosition Tile, short Priority, byte Proximity)
{
    internal int Key => Priority * 65536 + Proximity;
}
internal sealed record AiScenarioPartition(ImmutableArray<TilePosition> ScoreMask, int Distance, int Reach,
    bool OutOfReach, bool FilterFallback);
internal sealed record AiScenarioLayoutOutcome(ImmutableArray<TilePosition> Scenario1, ImmutableArray<TilePosition> Scenario2,
    ImmutableArray<AiMovementCellProvenance> Cells, TilePosition D0, TilePosition D1, int BaselineIndex,
    int EffectiveReach, SwordAiReachAdjustment? Adjustment, int? BaselineLimit, bool DestinationFallback, RandomState RngState);

/// <summary>Private source mask/destination arithmetic shared by admitted pair and roster owners. No simulation inputs or command authority.</summary>
/// <remarks>ZOD-287/289; source 11464943bbdbd2a0af022d90cb7f10455cf911df, scenarios_build/destination_select.</remarks>
internal static class AiScenarioLayout
{
    internal static AiScenarioPartition Partition(ImmutableArray<TilePosition> reachable, TilePosition origin,
        int move, int reach, Func<TilePosition, int> distance)
    {
        int d = distance(origin); bool outside = d > reach;
        int limit = outside ? d - 1 : move + d;
        var mask = reachable.Where(p => distance(p) <= limit).ToImmutableArray();
        return new(mask.IsEmpty ? reachable : mask, d, reach, outside, mask.IsEmpty);
    }

    // Caller catches draw-counter exhaustion and rolls the complete query back to its incoming RNG.
    internal static AiScenarioLayoutOutcome Finish(AiScenarioPartition partition, ImmutableArray<TilePosition> reachable,
        TilePosition origin, Func<TilePosition, int> distance, ImmutableArray<AiScenarioCell> scored,
        ImmutableDictionary<TilePosition, byte> proximity, bool nonBlue, bool canAct, bool rangedEnemy, RandomState rng)
    {
        int effective = partition.Reach; SwordAiReachAdjustment? adjustment = null;
        if (partition.OutOfReach && nonBlue)
        {
            var draw = new RandomStateGenerator().Next(rng); rng = draw.State;
            int modulo = (int)(draw.Value % 7); effective = modulo == 0 ? effective / 2 : effective;
            adjustment = new(draw.Value, modulo, partition.Reach, effective);
        }
        int? bound = null; bool fallback = false; TilePosition d0, d1;
        if (partition.OutOfReach)
        {
            var within = scored.Where(c => distance(c.Tile) <= effective).ToImmutableArray();
            fallback = within.IsEmpty;
            d0 = d1 = Select(fallback ? scored : within, distance, fallback);
        }
        else
        {
            d0 = d1 = Select(scored, distance, false);
            if (canAct)
            {
                bound = rangedEnemy ? Math.Max(3, partition.Distance - 1) : partition.Distance - 1;
                var within = scored.Where(c => distance(c.Tile) <= bound.Value).ToImmutableArray();
                fallback = within.IsEmpty;
                d0 = Select(fallback ? scored : within, distance, fallback);
            }
        }
        var written = scored.ToImmutableDictionary(c => c.Tile, c => c.Priority);
        var scenario2 = reachable.Where(p => !written.ContainsKey(p)).ToImmutableArray();
        var scenario1 = reachable.Where(p => written.ContainsKey(p) || p == origin).ToImmutableArray();
        var cells = reachable.Select(p => new AiMovementCellProvenance(p, proximity[p],
            written.TryGetValue(p, out short value) ? value : null)).ToImmutableArray();
        return new(scenario1, scenario2, cells, d0, d1, scored.Select(c => c.Tile).ToImmutableArray().IndexOf(d0),
            effective, adjustment, bound, fallback, rng);
    }

    internal static TilePosition Select(ImmutableArray<AiScenarioCell> cells, Func<TilePosition, int> distance, bool distanceFirst) =>
        (distanceFirst ? cells.OrderBy(c => distance(c.Tile)).ThenBy(c => c.Key)
            : cells.OrderBy(c => c.Key).ThenBy(c => distance(c.Tile)))
        .ThenByDescending(c => c.Tile.Layer).ThenByDescending(c => c.Tile.Y).ThenByDescending(c => c.Tile.X).First().Tile;
}
