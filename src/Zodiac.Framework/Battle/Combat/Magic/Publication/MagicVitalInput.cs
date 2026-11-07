namespace Zodiac.Framework;

/// <summary>Original unsigned stored fields and signed work, after earlier application branches are resolved.</summary>
public sealed record MagicVitalInput(ushort Hp, ushort MaxHp, ushort Mp, ushort MaxMp, bool Hit,
    short HpDamage, short HpHealing, short MpDamage = 0, short MpHealing = 0);
