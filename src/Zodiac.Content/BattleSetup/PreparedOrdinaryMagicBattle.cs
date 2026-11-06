using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Content.Definitions;
using Zodiac.Content.Definitions.Job;

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
    public string Ruleset { get; }
    public ImmutableDictionary<string, JobDefinition> JobDefinitions { get; }

    private PreparedOrdinaryMagicBattle(OrdinaryMagicPackageDocument document)
    {
        if (document.Format != "fft.ordinary-magic-battle" || document.SchemaVersion != 1 ||
            document.Profile != Profile || document.Ruleset is not ("original-fft" or "authored-ordinary"))
            throw new InvalidDataException("Unsupported ordinary magic package format, version, profile or ruleset.");
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
                u.LearnedSpells.Select(s => new AssetId(s)).ToImmutableHashSet(), job.Move);
        }).ToImmutableArray();
        var map = new BattleMapDefinition(document.Width, document.Depth, 1,
            Enumerable.Repeat(new BattleTile(document.Height), document.Width * document.Depth).ToArray());
        Definition = new(map, units, spells, catalog, document.JobThresholds.ToImmutableArray(), new(document.Seed, 0));
        UnitNames = document.Units.ToImmutableDictionary(u => new BattleUnitId(u.Id), u => u.Name);
        SpellNames = document.Spells.ToImmutableDictionary(s => new AssetId(s.Id), s =>
            !string.IsNullOrWhiteSpace(s.Name) && s.Name.Length <= 32 && !s.Name.Any(char.IsControl)
                ? s.Name : throw new InvalidDataException("Spell name is invalid."));
        Ruleset = document.Ruleset;
        canonicalJson = JsonSerializer.Serialize(document, Options) + "\n";
    }

    public static PreparedOrdinaryMagicBattle Load(LooseAssetStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return ReadJson(new UTF8Encoding(false, true).GetString(store.ReadAllBytes(AssetId, MaxJsonBytes)));
    }

    public static PreparedOrdinaryMagicBattle Create(OrdinaryMagicPackageDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return ReadJson(JsonSerializer.Serialize(document, Options));
    }

    public static PreparedOrdinaryMagicBattle ReadJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new InvalidDataException("Prepared magic package exceeds its load budget.");
        try
        {
            var document = JsonSerializer.Deserialize<OrdinaryMagicPackageDocument>(json, Options)
                ?? throw new InvalidDataException("Prepared magic document is empty.");
            return new(document);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException or OverflowException or KeyNotFoundException)
        {
            throw new InvalidDataException("Invalid prepared magic content: " + e.Message, e);
        }
    }

    public string ToJson() => canonicalJson;
}
