using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public static class BattleRawStatGrowth
{
    /// <summary>Use the level already established by the caller; always consumes five discarded draws on success.</summary>
    public static BattleRawGrowthResult Calculate(BattleRawStats stats, BattleGrowthRates growth, byte level,
        bool levelDown, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(stats); ArgumentNullException.ThrowIfNull(growth);
        uint[] values = [stats.Hp, stats.Mp, stats.Speed, stats.PhysicalAttack, stats.MagicAttack];
        byte[] rates = [growth.Hp, growth.Mp, growth.Speed, growth.PhysicalAttack, growth.MagicAttack];
        foreach (uint value in values)
            if (value > 0xffffff) throw new ArgumentOutOfRangeException(nameof(stats), "Raw stats must fit unsigned24 storage.");
        // Whole-query rejection is managed admission policy, not the original sequential mutation order.
        if (rng.DrawCount > ulong.MaxValue - 5) return new(false, null, rng);
        var generator = new RandomStateGenerator(); var next = rng;
        for (int i = 0; i < values.Length; i++)
        {
            uint divisor = (uint)Math.Max(1, (int)rates[i]) + level;
            next = generator.Next(next).State; // Output is discarded even for zero delta or a saturated stat.
            uint delta = values[i] / divisor;
            values[i] = Math.Min(0xffffffu, levelDown ? values[i] - delta : values[i] + delta);
        }
        return new(true, new(values[0], values[1], values[2], values[3], values[4]), next);
    }
}
