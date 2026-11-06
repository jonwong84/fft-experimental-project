using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Content.Definitions;
using Zodiac.Content.Definitions.Job;
using Zodiac.Content.ContentPackages;

namespace Zodiac.Content.BattleSetup;

/// <summary>Hash-verified prepared JSON only; no original-format reader or executable dependency at runtime.</summary>
public sealed class PreparedOrdinaryMagicBattle
{
    public static AssetId AssetId => new("fft.ps1:battle/ordinary-magic");
    public const int MaxJsonBytes = 2 * 1024 * 1024;
    private const string Profile = "ordinary-human-bare-v1";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NewLine = "\n",
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
        MaxDepth = 24,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private readonly string canonicalJson;
    public OrdinaryMagicBattleDefinition Definition { get; }
    public ImmutableDictionary<BattleUnitId, string> UnitNames { get; }
    public ImmutableDictionary<AssetId, string> SpellNames { get; }
    public ImmutableArray<AssetId> SpellOrder { get; }
    public string Ruleset { get; }
    public ImmutableDictionary<string, JobDefinition> JobDefinitions { get; }
    public ContentPackage? MetadataPackage { get; }
    public ImmutableDictionary<AssetId, AbilityMetadataDefinition> AbilityMetadata { get; }

    private PreparedOrdinaryMagicBattle(OrdinaryMagicPackageDocument document, ContentPackage? metadata)
    {
        ValidateHeader(document);
        if (document.Width < 1 || document.Depth < 1 || (long)document.Width * document.Depth > 256 ||
            document.Jobs.Length > 4096 || document.Unlocks.Length > 4096 || document.Spells.Length > 4096 ||
            document.Units.Length > 256 || document.Jobs.Any(j => j is null || j.Growth is null) || document.Spells.Any(s => s is null) ||
            document.Unlocks.Any(j => j is null || j.Requires is null) || document.Units.Any(u => u is null || u.Career is null || u.LearnedSpells is null))
            throw new InvalidDataException("Invalid or over-budget prepared magic collections.");
        var jobs = document.Jobs.ToDictionary(j => j.Id, StringComparer.Ordinal);
        JobDefinitions = jobs.ToImmutableDictionary(p => p.Key, p => new JobDefinition(new(p.Key), new(p.Value.SkillsetId),
            new JobStats(p.Value.Move, p.Value.Jump)
            {
                HpGrowth = p.Value.Growth.Hp, HpMultiplier = p.Value.Multipliers.Hp,
                MpGrowth = p.Value.Growth.Mp, MpMultiplier = p.Value.Multipliers.Mp,
                SpeedGrowth = p.Value.Growth.Speed, SpeedMultiplier = p.Value.Multipliers.Speed,
                PaGrowth = p.Value.Growth.PhysicalAttack, PaMultiplier = p.Value.Multipliers.PhysicalAttack,
                MaGrowth = p.Value.Growth.MagicAttack, MaMultiplier = p.Value.Multipliers.MagicAttack
            }, false, []), StringComparer.Ordinal);
        var catalog = new JobUnlockCatalog(document.Unlocks.Select(j => new JobUnlockDefinition(new(j.Id),
            j.Requires.Select(p => new JobLevelRequirement(new(p.Key), p.Value)).ToImmutableArray())),
            new(document.BaseJobId), new(document.BardJobId), new(document.DancerJobId));
        var spells = document.Spells.Select(s => new MagicCastDefinition(new(s.Id), s.OriginalId, s.Formula,
            s.MpCost, s.ChargeTime, s.AffectedBySilence, Targeting: s.Targeting)).ToImmutableArray();
        var units = document.Units.Select(u =>
        {
            if (string.IsNullOrWhiteSpace(u.Name) || u.Name.Length > 32 || u.Name.Any(char.IsControl) || !jobs.TryGetValue(u.JobId, out var job))
                throw new InvalidDataException("Unit names and job references must be explicit and valid.");
            var maxima = Mode1BattleAttributes.CalculateWithoutEquipment(u.RawStats, job.Multipliers, u.Hp, u.Mp, u.BaseAttributes);
            var stats = JobDefinitions[u.JobId].JobStats;
            var growth = new BattleGrowthRates(checked((byte)stats.HpGrowth), checked((byte)stats.MpGrowth),
                checked((byte)stats.SpeedGrowth), checked((byte)stats.PaGrowth), checked((byte)stats.MaGrowth));
            var multipliers = new BattleStatMultipliers(checked((byte)stats.HpMultiplier), checked((byte)stats.MpMultiplier),
                checked((byte)stats.SpeedMultiplier), checked((byte)stats.PaMultiplier), checked((byte)stats.MaMultiplier));
            var id = new BattleUnitId(u.Id);
            var current = new MagicCurrentUnit(id, (byte)Math.Min(99, (int)u.BaseAttributes.MagicAttack), u.Faith,
                new(u.Sign, u.Male, u.Female), new(), new(), Equipment: OrdinaryMagicBattleDefinition.BareEquipment, Mp: u.Mp);
            return new OrdinaryMagicUnitDefinition(new(id, u.SourceOrder, new(u.X, u.Y, 0), u.Team, u.Team), current,
                new(u.Hp, maxima.MaxHp, maxima.MaxMp, u.Brave, u.Hp <= maxima.MaxHp / 5), u.RawStats, growth,
                multipliers, u.BaseAttributes, u.Level, u.Experience, job.OriginalId, new(job.Id),
                u.Career.ToImmutableDictionary(p => new AssetId(p.Key), p => p.Value),
                u.LearnedSpells.Select(s => new AssetId(s)).ToImmutableHashSet(), job.Move, job.Jump, u.Facing,
                PlayerControlled: u.PlayerControlled);
        }).ToImmutableArray();
        // This profile authors ordinary cost1 ground; it never infers a cost from an original terrain ID.
        var ground = new TerrainSurfaceId("fft.demo:terrain/ordinary-ground");
        var map = new BattleMapDefinition(document.Width, document.Depth, 1,
            Enumerable.Repeat(new BattleTile(ground, document.Height), document.Width * document.Depth).ToArray());
        Definition = new(map, units, spells, catalog, document.JobThresholds.ToImmutableArray(), new(document.Seed, 0), ground);
        if (document.SchemaVersion == 1 && metadata is not null || document.SchemaVersion >= 2 &&
            (metadata is null || metadata.PackageId != document.MetadataPackageId || metadata.Kind != ContentPackageKind.Baseline ||
             !metadata.Jobs.IsEmpty || metadata.Abilities.Length != Definition.Spells.Count ||
             document.Ruleset == "original-fft" && metadata.ProfileId != "fft.ps1:profile/scus-94221"))
            throw new InvalidDataException("A version2 spell package requires its matching selected ability/skillset metadata companion.");
        MetadataPackage = metadata;
        AbilityMetadata = metadata?.Abilities.ToImmutableDictionary(a => new AssetId(a.Id.Value)) ??
            ImmutableDictionary<AssetId, AbilityMetadataDefinition>.Empty;
        if (document.SchemaVersion >= 2 && !Definition.Spells.Keys.ToHashSet().SetEquals(AbilityMetadata.Keys))
            throw new InvalidDataException("Every executable spell must have exactly one matching metadata definition.");
        if (metadata is not null && (JobDefinitions.Values.Any(j => !metadata.Skillsets.Any(s => s.Id == j.PrimarySkillsetId)) ||
            !metadata.Skillsets.SelectMany(s => s.Slots).Where(s => s.AbilityId is not null)
                .Select(s => new AssetId(s.AbilityId!.Value.Value)).ToHashSet().SetEquals(Definition.Spells.Keys)))
            throw new InvalidDataException("Selected skillsets must bind every spell and every prepared job primary skillset.");
        if (document.Units.Any(u => document.SchemaVersion < 3 && u.LearnedPrimarySlots is not null ||
            document.SchemaVersion == 3 && (u.LearnedPrimarySlots is null || u.LearnedPrimarySlots.Length > 22 ||
                u.LearnedPrimarySlots.Distinct().Count() != u.LearnedPrimarySlots.Length)))
            throw new InvalidDataException("Version3 requires an explicit unique learned primary slot list per unit; older versions use grants only.");
        if (metadata is not null)
        {
            var learning = new OrdinaryMagicLearningDefinition(metadata.Abilities.Select(a =>
                new AbilityLearningRule(new(a.Id.Value), a.LearnOnHit, checked((byte)a.LearnOnHitRateRaw))),
                JobDefinitions.Select(j => new KeyValuePair<AssetId, ImmutableArray<AbilityLearningSlot>>(new(j.Key),
                    metadata.Skillsets.Single(s => s.Id == j.Value.PrimarySkillsetId).Slots.Where(s => s.AbilityId is not null)
                        .Select(s => new AbilityLearningSlot(s.Slot, new(s.AbilityId!.Value.Value))).ToImmutableArray())));
            var captured = units.Select(u => u with
            {
                LearnedPrimarySlots = document.SchemaVersion == 3
                    ? document.Units.Single(d => d.Id == u.Current.UnitId.Value).LearnedPrimarySlots!.ToImmutableHashSet()
                    : learning.PrimarySlots(u.RewardJobId).Where(s => u.LearnedSpells.Contains(s.AbilityId))
                        .Select(s => s.Index).ToImmutableHashSet()
            }).ToImmutableArray();
            Definition = new(map, captured, spells, catalog, document.JobThresholds.ToImmutableArray(), new(document.Seed, 0), ground, learning);
        }
        UnitNames = document.Units.ToImmutableDictionary(u => new BattleUnitId(u.Id), u => u.Name);
        SpellNames = document.Spells.ToImmutableDictionary(s => new AssetId(s.Id), s =>
            !string.IsNullOrWhiteSpace(s.Name) && s.Name.Length <= 32 && !s.Name.Any(char.IsControl)
                ? s.Name : throw new InvalidDataException("Spell name is invalid."));
        SpellOrder = document.Spells.Select(s => new AssetId(s.Id)).ToImmutableArray();
        Ruleset = document.Ruleset;
        canonicalJson = JsonSerializer.Serialize(document, Options) + "\n";
    }

    public static PreparedOrdinaryMagicBattle Load(LooseAssetStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var utf8 = new UTF8Encoding(false, true);
        string json = utf8.GetString(store.ReadAllBytes(AssetId, MaxJsonBytes));
        var document = ParseDocument(json);
        ContentPackage? metadata = null;
        if (document.MetadataPackageId is { } id)
        {
            try { metadata = ContentPackage.ReadJson(utf8.GetString(store.ReadAllBytes(new(id), ContentPackage.MaxJsonBytes))); }
            catch (ContentPackageValidationException error) { throw new InvalidDataException("Invalid spell metadata companion: " + error.Message, error); }
        }
        return ReadJson(json, metadata);
    }

    public static PreparedOrdinaryMagicBattle Create(OrdinaryMagicPackageDocument document, ContentPackage? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return ReadJson(JsonSerializer.Serialize(document, Options), metadata is null ? null : ContentPackage.ReadJson(metadata.ToJson()));
    }

    public static PreparedOrdinaryMagicBattle ReadJson(string json, ContentPackage? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        try { return new(ParseDocument(json), metadata); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException or KeyNotFoundException)
        { throw new InvalidDataException("Invalid prepared magic content: " + e.Message, e); }
    }

    private static void ValidateHeader(OrdinaryMagicPackageDocument document)
    {
        if (document.Format != "fft.ordinary-magic-battle" || document.SchemaVersion is not (1 or 2 or 3) ||
            document.Profile != Profile || document.Ruleset is not ("original-fft" or "authored-ordinary") ||
            document.SchemaVersion == 1 && document.MetadataPackageId is not null ||
            document.SchemaVersion >= 2 && string.IsNullOrWhiteSpace(document.MetadataPackageId))
            throw new InvalidDataException("Unsupported ordinary magic package format, version, profile, ruleset or companion reference.");
        if (document.MetadataPackageId is { } id) _ = new AssetId(id);
    }

    private static OrdinaryMagicPackageDocument ParseDocument(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new InvalidDataException("Prepared magic package exceeds its load budget.");
        try
        {
            var document = JsonSerializer.Deserialize<OrdinaryMagicPackageDocument>(json, Options)
                ?? throw new InvalidDataException("Prepared magic document is empty.");
            ValidateHeader(document);
            return document;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException or OverflowException or KeyNotFoundException)
        {
            throw new InvalidDataException("Invalid prepared magic content: " + e.Message, e);
        }
    }

    public string ToJson() => canonicalJson;
}
