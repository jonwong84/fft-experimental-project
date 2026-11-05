using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record AiRosterNearestOutcome(BattleUnitId TargetId, int? Distance,
    ImmutableArray<BattleUnitId> EligibleUnits)
{
    public bool UsedActorFallback => Distance is null;
}

/// <summary>Only source ELIGIBLE_ENEMY, not the different healer, imminent, enemy or arbitrary-unit nearest modes.</summary>
/// <remarks>ZOD-273; first source-order tie wins; an empty eligible set falls back to the actor; no RNG.</remarks>
public static class AiRosterNearest
{
    public static AiRosterNearestOutcome FindEligibleEnemy(AiTargetRoster roster, IEnumerable<BattleUnitId> targetable)
    {
        ArgumentNullException.ThrowIfNull(roster); var members = roster.CopyMembership(targetable);
        var actor = roster.ById[roster.ActorId]; var target = roster.ActorId; int? best = null;
        var eligible = ImmutableArray.CreateBuilder<BattleUnitId>();
        foreach (var unit in roster.Units)
        {
            if (AiRosterEligibility.IsUntargetable(unit) || unit.Live.Petrified || !unit.Decision.Enemy ||
                unit.Decision.DeadWithoutReraise || !members.Contains(unit.UnitId) || !unit.Decision.Considered || unit.Live.BloodSuck) continue;
            eligible.Add(unit.UnitId); int distance = Distance(actor.Position, unit.Position);
            if (best is null || distance < best) { best = distance; target = unit.UnitId; }
        }
        return new(target, best, eligible.ToImmutable());
    }

    internal static int Distance(TilePosition first, TilePosition second) => Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
}
