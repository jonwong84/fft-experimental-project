namespace Zodiac.Content.BattleSetup;

/// <summary>Versioned prepared data; original spell/job rules are separate from the authored encounter layout and grants.</summary>
public sealed record DefaultAbilityPackageDocument(string Format, int SchemaVersion, string Profile, string Ruleset,
    uint Seed, int Width, int Depth, byte Height, string BaseJobId, string BardJobId, string DancerJobId,
    ushort[] JobThresholds, DefaultAbilityPreparedSpell[] Spells, DefaultAbilityPreparedJob[] Jobs,
    DefaultAbilityPreparedUnlock[] Unlocks, DefaultAbilityPreparedUnit[] Units, string? MetadataPackageId = null);
