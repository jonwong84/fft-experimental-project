using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Managed assertions, not original packed FFT flags. Every fact must be explicitly certified.</summary>
[Flags]
public enum AiScoreFacts
{
    None = 0,
    NonMonster = 1,
    AllSevenEquipmentSlotsUnchanged = 2,
    StableOpponents = 4,
    ZeroCurrentAndBaselineGolem = 8,
    ClosedProfile = NonMonster | AllSevenEquipmentSlotsUnchanged | StableOpponents | ZeroCurrentAndBaselineGolem
}

public sealed record AiScoreUnitCertificate(BattleUnitId UnitId, AiScoreFacts Facts);

/// <summary>Explicit scoring admission for the same two stable human opponents as the candidate profile.</summary>
/// <remarks>
/// Certifies original non-monster classification, all seven equipment entries unchanged (including empties),
/// cached allied-actor/hostile-target relation for either actor, no team/control changes or stale targeting flags,
/// and zero current AND baseline Golem on both scored teams. Unrepresented facts remain the caller's assertion;
/// a weapon ID cannot prove them. MP=maxMP=0 is already enforced by the live model.
/// </remarks>
public sealed class AiScoreProfile
{
    public ImmutableArray<AiScoreUnitCertificate> Units { get; }

    public AiScoreProfile(IEnumerable<AiScoreUnitCertificate> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        Units = units.ToImmutableArray();
        if (Units.Length != 2 || Units.Any(u => u is null || string.IsNullOrWhiteSpace(u.UnitId.Value)) ||
            Units.Select(u => u.UnitId).Distinct().Count() != 2)
            throw new ArgumentException("Specify exactly two distinct stable unit certificates.", nameof(units));
    }

    internal bool Supports(AttackBattleSnapshot state, SwordAiCandidateProfile candidateProfile) =>
        Supports(state, candidateProfile.Units.Select(u => u.UnitId));

    internal bool Supports(AttackBattleSnapshot state, IEnumerable<BattleUnitId> candidateIds)
    {
        var ids = Units.Select(u => u.UnitId).ToHashSet();
        return Units.All(u => u.Facts == AiScoreFacts.ClosedProfile) &&
            state.Combatants.Length == 2 && ids.SetEquals(state.Combatants.Select(u => u.UnitId)) &&
            ids.SetEquals(candidateIds) &&
            state.Combatants.All(u => u.Mp == 0 && u.MaxMp == 0);
    }
}

public enum AiScoringDisposition { Rejected, NotValuable, Scored }
public enum AiScoringRejection { None, UnsupportedScoreProfile, ProjectionRejected }

public sealed record AiUnitScoreContribution(BattleUnitId UnitId, int TieKey, bool Enemy,
    int HpTerm, int StatusTerm, int EquipmentTerm, int UnitTotal, int SignedContribution, short RunningPriority);
public sealed record AiCandidateScore(short Priority, byte HitPercent,
    ImmutableArray<AiUnitScoreContribution> Contributions);

/// <summary>Private absolute source scoring shared by freshly owned ordinary candidate simulations.</summary>
internal static class AiCandidateScoring
{
    // Internal only: callers own a freshly validated private simulation, never a caller-populated public result.
    internal static AiCandidateScore ScoreFreshUnits(ImmutableArray<AiProjectedUnit> units,
        BattleUnitId targetId, byte hitPercent)
    {
        short priority = 0;
        var contributions = ImmutableArray.CreateBuilder<AiUnitScoreContribution>(units.Length);
        // The trusted fresh projection supplies original TieKey order, consistent HP/status and only two units.
        foreach (var unit in units)
        {
            int hp = unit.Hp * 128 / unit.MaxHp;
            int status = unit.Status switch
            {
                AttackUnitStatus.None => 0,
                AttackUnitStatus.Critical => -32,
                AttackUnitStatus.Dead => -192,
                _ => throw new InvalidOperationException("Fresh ordinary projection produced an unsupported status.")
            };
            const int equipment = 7 * 51; // Equal unchanged human terms cancel only after each unit is scored.
            int total = hp + status + equipment;
            bool enemy = unit.UnitId == targetId;
            int signed = enemy ? -total : total;
            priority = unchecked((short)(priority + signed)); // Source narrows after each unit; admitted final range -160..320.
            contributions.Add(new(unit.UnitId, unit.TieKey, enemy, hp, status, equipment, total, signed, priority));
        }
        return new(priority, hitPercent, contributions.ToImmutable());
    }
}
