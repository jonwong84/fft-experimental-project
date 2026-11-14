using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

public sealed record DefaultAbilityPreparedJob(string Id, byte OriginalId, BattleGrowthRates Growth,
    BattleStatMultipliers Multipliers, byte Move, byte Jump, string SkillsetId);
