namespace Zodiac.Framework;

/// <summary>Copied original multiplier bytes in raw-stat order; mode1 retains the existing base PA/MA/Speed.</summary>
public readonly record struct BattleStatMultipliers(byte Hp, byte Mp, byte Speed, byte PhysicalAttack, byte MagicAttack);
