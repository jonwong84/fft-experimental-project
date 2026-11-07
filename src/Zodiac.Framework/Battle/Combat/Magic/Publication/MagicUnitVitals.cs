namespace Zodiac.Framework;

/// <summary>Explicit original-width vital/status facts, separate from formula inputs and the narrow sword state.</summary>
public sealed record MagicUnitVitals(ushort Hp, ushort MaxHp, ushort MaxMp, byte Brave, bool Critical = false);
