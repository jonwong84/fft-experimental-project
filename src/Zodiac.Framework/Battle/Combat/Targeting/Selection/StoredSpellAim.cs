namespace Zodiac.Framework;

/// <summary>Original targeting discriminator and signed coordinates, with a stable follow identity instead of a slot index.</summary>
public sealed record StoredSpellAim(byte TargetingType, short X, short Y, short Layer, BattleUnitId? TargetUnitId = null);
