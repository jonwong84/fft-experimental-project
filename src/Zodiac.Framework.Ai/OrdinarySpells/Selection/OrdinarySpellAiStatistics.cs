using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record OrdinarySpellAiStatisticEntry(int MpCost, bool AffectedBySilence, bool Evadeable,
    bool RequiresMonsterSkill = false);

/// <summary>Source initialized-list statistics. Current MP/usability does not change these shares.</summary>
public sealed class OrdinarySpellAiStatistics
{
    public int InitializedEntryCount { get; }
    public int MpQuarterShare { get; }
    public int SilenceQuarterShare { get; }
    public int EvadeQuarterShare { get; }
    public int LowestMpCost { get; }
    public int HighestMpCost { get; }

    private OrdinarySpellAiStatistics(int count, int mp, int silence, int evade, int minimum, int maximum)
    {
        InitializedEntryCount = count; MpQuarterShare = mp; SilenceQuarterShare = silence;
        EvadeQuarterShare = evade; LowestMpCost = minimum; HighestMpCost = maximum;
    }

    public static OrdinarySpellAiStatistics ForOrdinaryHumanList(OrdinarySpellAiList list, OrdinarySpellAiSupports supports)
    {
        ArgumentNullException.ThrowIfNull(list); ArgumentNullException.ThrowIfNull(supports);
        // Bare Attack and unconditional initialized Frog Attack remain in the denominator.
        // Primary Frog Attack AI is not Evadeable, although its secondary attack is.
        return Calculate(new[] { new OrdinarySpellAiStatisticEntry(0, false, true), new(0, false, false) }
            .Concat(list.Entries.Select(e => new OrdinarySpellAiStatisticEntry(
                supports.HalfMp ? e.Slot.Spell.MpCost >> 1 : e.Slot.Spell.MpCost,
                e.Slot.Behavior.AffectedBySilence, e.Slot.Behavior.Evadeable))));
    }

    public static OrdinarySpellAiStatistics Calculate(IEnumerable<OrdinarySpellAiStatisticEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var all = entries.ToImmutableArray();
        if (all.Any(e => e is null || e.MpCost < 0)) throw new ArgumentException("Explicit nonnegative initialized costs are required.", nameof(entries));
        var counted = all.Where(e => !e.RequiresMonsterSkill).ToArray();
        var costs = counted.Where(e => e.MpCost != 0).Select(e => e.MpCost).ToArray();
        int Quarter(int count) => all.Length == 0 ? 0 : checked(count * 4) / all.Length;
        return new(all.Length, Quarter(costs.Length), Quarter(counted.Count(e => e.AffectedBySilence)),
            Quarter(counted.Count(e => e.Evadeable)), costs.Length == 0 ? 255 : costs.Min(), costs.Length == 0 ? 0 : costs.Max());
    }
}
