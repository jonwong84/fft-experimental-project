using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum AiRosterTargetFilter { Any = 0, Enemies = 1, Allies = 2, Specific = 3, Status = 4 }
public sealed record AiRosterActiveOutcome(ImmutableArray<BattleUnitId> SelectedUnits,
    ImmutableDictionary<BattleUnitId, bool> UnitActive)
{
    public bool AnyActive => !SelectedUnits.IsEmpty;
}

/// <summary>Rebuild every active flag from cached targetability/Considered and live Confusion/status facts.</summary>
/// <remarks>ZOD-274; unknown signed filter values deliberately follow ANY, as in source.</remarks>
internal static class AiRosterActiveTargets
{
    public static AiRosterActiveOutcome Select(AiTargetRoster roster, AiRosterTargetFilter filter,
        BattleUnitId? specificTarget = null, bool excludeActor = false)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var selected = ImmutableArray.CreateBuilder<BattleUnitId>(); var flags = ImmutableDictionary.CreateBuilder<BattleUnitId, bool>();
        foreach (var unit in roster.Units)
        {
            bool active = !unit.Decision.Untargetable && (unit.Live.Confused || unit.Decision.Considered) &&
                (!excludeActor || unit.UnitId != roster.ActorId) && (filter switch {
                    AiRosterTargetFilter.Enemies => unit.Decision.Enemy,
                    AiRosterTargetFilter.Allies => !unit.Decision.Enemy,
                    AiRosterTargetFilter.Specific => unit.UnitId == specificTarget,
                    AiRosterTargetFilter.Status => unit.Live.Dead || unit.Live.Petrified || unit.Live.Critical || unit.Live.DeathSentence,
                    _ => true
                });
            flags.Add(unit.UnitId, active); if (active) selected.Add(unit.UnitId);
        }
        return new(selected.ToImmutable(), flags.ToImmutable());
    }
}
