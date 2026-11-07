namespace Zodiac.Framework;

/// <summary>Original byte growth parameters in source HP, MP, Speed, PA, MA order.</summary>
public sealed record BattleGrowthRates(byte Hp, byte Mp, byte Speed, byte PhysicalAttack, byte MagicAttack);
