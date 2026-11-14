using System.Collections.Immutable;

namespace Zodiac.Framework;

public static class JobPointArithmetic
{
    /// <summary>Safe source signed32 addition domain; explicit thresholds, no file reads or unlock application.</summary>
    public static JobPointBalance Add(ushort current, ushort total, int amount, ImmutableArray<ushort> thresholds)
    {
        if (amount < 0 || amount > int.MaxValue - ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(amount));
        if (thresholds.IsDefault) throw new ArgumentException("Explicit job-level thresholds are required.", nameof(thresholds));
        ushort nextCurrent = (ushort)Math.Min(9999, current + amount);
        ushort nextTotal = (ushort)Math.Min(9999, total + amount);
        int level = 0;
        foreach (ushort threshold in thresholds) if (nextTotal >= threshold) level++;
        return new(nextCurrent, nextTotal, level);
    }

    /// <summary>Original byte job-ID adapter; generic Chemist..Mime map to1..19, all others to0.</summary>
    public static int OriginalRewardSlot(byte originalJobId) => originalJobId is >= 75 and <= 93 ? originalJobId - 74 : 0;
}
