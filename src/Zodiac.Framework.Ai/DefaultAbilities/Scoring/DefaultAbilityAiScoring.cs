using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record DefaultAbilityAiScoringUnit(BattleUnitId UnitId, int SourceOrder, MagicUnitVitals Vitals,
    ushort Mp, bool Dead, bool Enemy, DefaultAbilityAiStatistics Statistics);
public sealed record DefaultAbilityAiScoreContribution(BattleUnitId UnitId, int HpTerm, int StatusTerm,
    int EquipmentTerm, int MpTerm, int Total, int SignedContribution, short RunningPriority);
public sealed record DefaultAbilityAiScore(short Priority, byte HitPercent, ImmutableArray<DefaultAbilityAiScoreContribution> Contributions);

/// <summary>Absolute bare-human source state scoring, not a delta and not a complete delayed candidate rank.</summary>
public static class DefaultAbilityAiScoring
{
    public static int MpTerm(ushort mp, ushort maxMp, DefaultAbilityAiStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        if (mp > maxMp) throw new ArgumentOutOfRangeException(nameof(mp));
        if (maxMp == 0) return 0;
        int value = mp * 64 / maxMp;
        if (mp < statistics.LowestMpCost) value >>= 1;
        return value * statistics.MpQuarterShare / 4;
    }

    public static DefaultAbilityAiScore Calculate(IEnumerable<DefaultAbilityAiScoringUnit> units, byte hitPercent,
        short initialPriority = 0)
        => CalculateCore(units, hitPercent, initialPriority, false);

    // Only a fresh simulated event-limit owner may retain the source's unset-hit sentinel.
    internal static DefaultAbilityAiScore CalculateUnresolved(IEnumerable<DefaultAbilityAiScoringUnit> units) =>
        CalculateCore(units, byte.MaxValue, 0, true);

    private static DefaultAbilityAiScore CalculateCore(IEnumerable<DefaultAbilityAiScoringUnit> units, byte hitPercent,
        short initialPriority, bool unresolved)
    {
        ArgumentNullException.ThrowIfNull(units);
        var ordered = units.OrderBy(u => u?.SourceOrder).ToImmutableArray();
        if (ordered.IsEmpty || ordered.Any(u => u is null || u.Vitals is null || u.Statistics is null ||
                string.IsNullOrWhiteSpace(u.UnitId.Value) || u.SourceOrder < 0 || u.Vitals.MaxHp == 0 ||
                u.Vitals.Hp > u.Vitals.MaxHp || u.Mp > u.Vitals.MaxMp || u.Dead != (u.Vitals.Hp == 0) ||
                u.Vitals.Critical != (!u.Dead && u.Vitals.Hp <= u.Vitals.MaxHp / 5)) ||
            ordered.Select(u => u.UnitId).Distinct().Count() != ordered.Length ||
            ordered.Select(u => u.SourceOrder).Distinct().Count() != ordered.Length || hitPercent > 100 && !(unresolved && hitPercent == byte.MaxValue))
            throw new ArgumentException("Explicit coherent ordinary score units and unique source slots are required.", nameof(units));
        short priority = initialPriority;
        var terms = ImmutableArray.CreateBuilder<DefaultAbilityAiScoreContribution>();
        foreach (var unit in ordered)
        {
            int hp = unit.Vitals.Hp * 128 / unit.Vitals.MaxHp;
            int status = unit.Dead ? -192 : unit.Vitals.Critical ? -32 : 0;
            const int equipment = 7 * 51;
            int mp = MpTerm(unit.Mp, unit.Vitals.MaxMp, unit.Statistics);
            int total = hp + status + equipment + mp;
            int signed = unit.Enemy ? -total : total;
            priority = unchecked((short)(priority + signed));
            terms.Add(new(unit.UnitId, hp, status, equipment, mp, total, signed, priority));
        }
        return new(priority, hitPercent, terms.ToImmutable());
    }
}
