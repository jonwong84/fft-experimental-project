namespace Zodiac.Framework;

/// <summary>The five unsigned24 source raw stats, independent of derived battle attributes.</summary>
public sealed record BattleRawStats(uint Hp, uint Mp, uint Speed, uint PhysicalAttack, uint MagicAttack);
