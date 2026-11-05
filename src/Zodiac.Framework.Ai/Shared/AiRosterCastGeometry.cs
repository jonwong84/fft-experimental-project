using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Private ordinary instantaneous enemy masks shared by fresh provenance and entry owners.</summary>
internal static class AiRosterCastGeometry
{
    internal static ImmutableArray<AiRosterUnit> UsefulEnemies(RosterAiGeneralPreparationOutcome p) =>
        p.Preparation.Targets.Units.Where(u => u.Decision.Enemy && p.ActiveTargets.UnitActive[u.UnitId]).ToImmutableArray();

    internal static ImmutableArray<TilePosition> Origins(ImmutableArray<TilePosition> scenario,
        ImmutableArray<AiRosterUnit> targets, int range) =>
        scenario.Where(tile => targets.Any(u => AiRosterNearest.Distance(tile, u.Position) <= range)).ToImmutableArray();

    internal static ImmutableArray<AiCastOriginPriority> BindRanked(RosterAiGeneralScenarioOutcome m,
        ImmutableArray<TilePosition> origins, AiPriorityWorkspace workspace, out TilePosition? missing)
    {
        var keys = ImmutableArray.CreateBuilder<AiCastOriginPriority>(); missing = null;
        foreach (var tile in origins)
        {
            var cell = m.Cells.Single(c => c.Tile == tile); short value;
            if (cell.WrittenPriority is { } fresh) value = fresh;
            else if (!workspace.KnownPriorities.TryGetValue(tile, out value)) { missing = tile; return []; }
            keys.Add(new(tile, value, cell.Proximity, cell.PriorityWritten ? AiCastPriorityProvenance.Fresh : AiCastPriorityProvenance.Retained));
        }
        return keys.OrderBy(k => k.OriginKey).ThenByDescending(k => k.Tile.Layer)
            .ThenByDescending(k => k.Tile.Y).ThenByDescending(k => k.Tile.X).ToImmutableArray();
    }
}
