using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Managed assertions, not original packed FFT flags. Every fact must be explicitly certified.</summary>
[Flags]
public enum SwordAiScoreFacts
{
    None = 0,
    NonMonster = 1,
    AllSevenEquipmentSlotsUnchanged = 2,
    StableOpponents = 4,
    ZeroCurrentAndBaselineGolem = 8,
    ClosedProfile = NonMonster | AllSevenEquipmentSlotsUnchanged | StableOpponents | ZeroCurrentAndBaselineGolem
}

public sealed record SwordAiScoreUnitCertificate(BattleUnitId UnitId, SwordAiScoreFacts Facts);

/// <summary>Explicit scoring admission for the same two stable human opponents as the candidate profile.</summary>
/// <remarks>
/// Certifies original non-monster classification, all seven equipment entries unchanged (including empties),
/// cached allied-actor/hostile-target relation for either actor, no team/control changes or stale targeting flags,
/// and zero current AND baseline Golem on both scored teams. Unrepresented facts remain the caller's assertion;
/// a weapon ID cannot prove them. MP=maxMP=0 is already enforced by the live model.
/// </remarks>
public sealed class SwordAiScoreProfile
{
    public ImmutableArray<SwordAiScoreUnitCertificate> Units { get; }

    public SwordAiScoreProfile(IEnumerable<SwordAiScoreUnitCertificate> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        Units = units.ToImmutableArray();
        if (Units.Length != 2 || Units.Any(u => u is null || string.IsNullOrWhiteSpace(u.UnitId.Value)) ||
            Units.Select(u => u.UnitId).Distinct().Count() != 2)
            throw new ArgumentException("Specify exactly two distinct stable unit certificates.", nameof(units));
    }

    internal bool Supports(AttackBattleSnapshot state, SwordAiCandidateProfile candidateProfile)
    {
        var ids = Units.Select(u => u.UnitId).ToHashSet();
        return Units.All(u => u.Facts == SwordAiScoreFacts.ClosedProfile) &&
            state.Combatants.Length == 2 && ids.SetEquals(state.Combatants.Select(u => u.UnitId)) &&
            ids.SetEquals(candidateProfile.Units.Select(u => u.UnitId)) &&
            state.Combatants.All(u => u.Mp == 0 && u.MaxMp == 0);
    }
}

public enum SwordAiScoringDisposition { Rejected, NotValuable, Scored }
public enum SwordAiScoringRejection { None, UnsupportedScoreProfile, ProjectionRejected }

public sealed record SwordAiUnitScoreContribution(BattleUnitId UnitId, int TieKey, bool Enemy,
    int HpTerm, int StatusTerm, int EquipmentTerm, int UnitTotal, int SignedContribution, short RunningPriority);
public sealed record SwordAiCandidateScore(short Priority, byte HitPercent,
    ImmutableArray<SwordAiUnitScoreContribution> Contributions);

/// <summary>No score for rejected or NotValuable inputs. A retained projection is fresh from this invocation.</summary>
/// <remarks>The score stage adds no RNG progress; the complete wrapper returns the projection's stream verbatim.</remarks>
public sealed record SwordAiScoringResult(SwordAiScoringDisposition Disposition, SwordAiScoringRejection Rejection,
    SwordAiCandidateResult? Projection, SwordAiCandidateScore? Score, FftRandomState RngState);

/// <summary>Pure opt-in absolute state scoring of one freshly projected ordinary sword candidate.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-126/127.
/// No external/cached outcome entry, enumeration, comparison, ranking, policy, command or live RNG commitment.
/// </remarks>
public static class SwordAiCandidateScoring
{
    public static SwordAiScoringResult Evaluate(AttackBattleSnapshot state, SwordAiCandidate candidate,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        if (!scoreProfile.Supports(state, candidateProfile))
            return new(SwordAiScoringDisposition.Rejected, SwordAiScoringRejection.UnsupportedScoreProfile, null, null, decisionRng);

        // One call site: provenance comes from original inputs, not a publicly constructible outcome record.
        var projection = SwordAiCandidateProjection.Evaluate(state, candidate, weapon, candidateProfile, decisionRng);
        if (projection.Disposition == SwordAiCandidateDisposition.Rejected)
            return new(SwordAiScoringDisposition.Rejected, SwordAiScoringRejection.ProjectionRejected, projection, null, projection.RngState);
        if (projection.Disposition == SwordAiCandidateDisposition.NotValuable)
            return new(SwordAiScoringDisposition.NotValuable, SwordAiScoringRejection.None, projection, null, projection.RngState);

        var outcome = projection.Outcome!;
        return new(SwordAiScoringDisposition.Scored, SwordAiScoringRejection.None, projection,
            ScoreFreshUnits(outcome.Units, outcome.TargetId, (byte)outcome.HitPercent), projection.RngState);
    }

    // Internal only: callers own a freshly validated private simulation, never a caller-populated public result.
    internal static SwordAiCandidateScore ScoreFreshUnits(ImmutableArray<SwordAiProjectedUnit> units,
        BattleUnitId targetId, byte hitPercent)
    {
        short priority = 0;
        var contributions = ImmutableArray.CreateBuilder<SwordAiUnitScoreContribution>(units.Length);
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
