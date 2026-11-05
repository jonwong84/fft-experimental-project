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
}
