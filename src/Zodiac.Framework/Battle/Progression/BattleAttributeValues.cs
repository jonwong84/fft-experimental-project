namespace Zodiac.Framework;

/// <summary>Three original byte attributes; containing fields distinguish base, bonus and actual values.</summary>
public readonly record struct BattleAttributeValues(byte PhysicalAttack, byte MagicAttack, byte Speed);
