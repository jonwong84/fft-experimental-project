namespace Zodiac.Framework;

/// <summary>Vital fields only; Changed is not the source's complete action outcome.</summary>
public sealed record MagicVitalChange(ushort Hp, ushort Mp, bool Changed);
