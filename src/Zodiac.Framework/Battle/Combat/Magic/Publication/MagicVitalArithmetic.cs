namespace Zodiac.Framework;

/// <summary>Source11464943bbdbd2a0af022d90cb7f10455cf911df signed32 expressions and unsigned16 stores.</summary>
public static class MagicVitalArithmetic
{
    public static MagicVitalChange Apply(MagicVitalInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.Hit) return new(input.Hp, input.Mp, false);
        int hp = Math.Clamp(input.Hp - input.HpDamage + input.HpHealing, 0, input.MaxHp);
        int mp = Math.Clamp(input.Mp - input.MpDamage + input.MpHealing, 0, input.MaxMp);
        return new((ushort)hp, (ushort)mp, hp != input.Hp || mp != input.Mp);
    }
}
