using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record AiRosterProximityOutcome(ImmutableArray<BattleUnitId> TargetableEnemies,
    ImmutableDictionary<TilePosition, byte> WrittenValues);

/// <summary>Source scenario0 proximity writes only; cells outside the mask remain unreported, not cleared.</summary>
/// <remarks>ZOD-275; resolved target delays compare against the pre-refresh cached actor byte.</remarks>
public static class AiRosterProximity
{
    public static AiRosterProximityOutcome Evaluate(AiTargetRoster roster, IEnumerable<TilePosition> scenario0)
    {
        ArgumentNullException.ThrowIfNull(roster); ArgumentNullException.ThrowIfNull(scenario0);
        var tiles = scenario0.Distinct().OrderBy(t => t.Layer).ThenBy(t => t.Y).ThenBy(t => t.X).ToImmutableArray();
        foreach (var tile in tiles) AiRosterUnit.ValidatePosition(tile);
        var enemies = roster.Units.Where(u => !AiRosterEligibility.IsOutOfPlay(u) && u.Decision.Enemy &&
            u.TurnDelay <= roster.CachedActorDelay && !u.DisabledThroughActorDelay).ToImmutableArray();
        var writes = ImmutableDictionary.CreateBuilder<TilePosition, byte>();
        foreach (var tile in tiles)
        {
            byte value = 0;
            foreach (var unit in enemies)
                value = unchecked((byte)(value + Math.Max(0, 10 - AiRosterNearest.Distance(tile, unit.Position))));
            writes.Add(tile, value);
        }
        return new(enemies.Select(u => u.UnitId).ToImmutableArray(), writes.ToImmutable());
    }
}
