namespace Zodiac.Framework;

public static class Mode1BattleAttributes
{
    /// <summary>Numeric mode1 result when no equipment contributes. Do not use its clamped current fields before adding armor.</summary>
    public static Mode1AttributeProjection CalculateWithoutEquipment(BattleRawStats raw, BattleStatMultipliers multipliers,
        ushort hp, ushort mp, BattleAttributeValues baseAttributes, bool egg = false)
    {
        var maxima = DeriveMaxima(raw, multipliers, egg);
        return new(Math.Min(hp, maxima.Hp), maxima.Hp, Math.Min(mp, maxima.Mp), maxima.Mp, baseAttributes);
    }

    internal static (ushort Hp, ushort Mp) DeriveMaxima(BattleRawStats raw, BattleStatMultipliers multipliers, bool egg)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (raw.Hp > 0xffffff || raw.Mp > 0xffffff || raw.Speed > 0xffffff || raw.PhysicalAttack > 0xffffff || raw.MagicAttack > 0xffffff)
            throw new ArgumentOutOfRangeException(nameof(raw), "Raw stats must fit unsigned24 storage.");
        uint divisor = egg ? 10u : 100u; uint cap = egg ? ushort.MaxValue : 999u;
        ushort Maximum(uint value, byte multiplier)
        {
            uint derived = (value * multiplier / divisor) >> 14;
            return (ushort)Math.Min(cap, Math.Max(1u, derived));
        }
        return (Maximum(raw.Hp, multipliers.Hp), Maximum(raw.Mp, multipliers.Mp));
    }
}
