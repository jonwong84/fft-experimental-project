using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Zodiac.Content.Definitions;

namespace Zodiac.Content.ContentPackages;

/// <summary>A separately versioned executable-content layer; it never mutates learning metadata.</summary>
public sealed partial class SwordContentPackage
{
    public const string Format = "fft.sword-content";
    public const int SchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;
    public int Version { get; }
    public string PackageId { get; }
    public string ProfileId { get; }
    public string RequiredMetadataPackageId { get; }
    public ImmutableArray<SwordDefinition> Swords { get; }

    public SwordContentPackage(string packageId, string profileId, string requiredMetadataPackageId,
        IEnumerable<SwordDefinition> swords)
        : this(packageId, profileId, requiredMetadataPackageId, swords, SchemaVersion) { }

    public SwordContentPackage(string packageId, string profileId, string requiredMetadataPackageId,
        IEnumerable<SwordDefinition> swords, int version)
    {
        var validation = new PackageJsonValidation(packageId);
        if (version is not (1 or 2))
            throw validation.Failure(null, "$.schemaVersion", "Unsupported schema version.");
        Version = version;
        PackageId = validation.Id(packageId, null, "$.packageId");
        ProfileId = validation.Id(profileId, null, "$.profileId");
        RequiredMetadataPackageId = validation.Id(requiredMetadataPackageId, null, "$.requiredMetadataPackageId");
        if (PackageId == RequiredMetadataPackageId)
            throw validation.Failure(null, "$.requiredMetadataPackageId", "A sword layer must have its own distinct package ID.");
        if (swords is null) throw validation.Failure(null, "$.swords", "Definition collection is required.");
        var values = ImmutableArray.CreateBuilder<SwordDefinition>();
        var ids = new HashSet<WeaponId>();
        string owner = PackageId[..PackageId.IndexOf(':')];
        foreach (SwordDefinition sword in swords)
        {
            string path = $"$.swords[{values.Count}].id";
            if (values.Count == ContentPackage.MaxDefinitionsPerKind)
                throw validation.Failure(null, "$.swords", $"Collection exceeds {ContentPackage.MaxDefinitionsPerKind} definitions.");
            if (sword is null) throw validation.Failure(null, $"$.swords[{values.Count}]", "Definition cannot be null.");
            string id = validation.Id(sword.Id.Value, sword.Id.Value, path);
            if (id[..id.IndexOf(':')] != owner)
                throw validation.Failure(id, path, $"Definition must belong to package namespace '{owner}'.");
            if (!ids.Add(sword.Id)) throw validation.Failure(id, path, $"Duplicate sword ID '{id}'.");
            if (version == 1 && (sword.CapabilityFlags != SwordCapabilityFlags.None || sword.AddedEffect.TypeRaw != 128))
                throw validation.Failure(id, $"$.swords[{values.Count}]", "Schema 1 requires no capability flags and empty proc type 128; select schema 2 explicitly.");
            values.Add(sword);
        }
        Swords = values.ToImmutable();
    }

    /// <summary>Explicit format upgrade; preserves every definition without activating new behavior.</summary>
    public SwordContentPackage UpgradeToV2() => new(PackageId, ProfileId, RequiredMetadataPackageId, Swords, CurrentSchemaVersion);

    /// <summary>Explicitly verifies the selected metadata dependency; no directory discovery or implicit activation.</summary>
    public void ValidateAgainst(ContentPackage metadata)
    {
        var validation = new PackageJsonValidation(PackageId);
        if (metadata is null) throw validation.Failure(null, "$.requiredMetadataPackageId", "Selected metadata package is required.");
        if (metadata.PackageId != RequiredMetadataPackageId)
            throw validation.Failure(null, "$.requiredMetadataPackageId", "Selected metadata package ID does not match the dependency.");
        if (metadata.ProfileId != ProfileId)
            throw validation.Failure(null, "$.profileId", "Sword and metadata profiles do not match.");
    }

    public static SwordContentPackage ReadJson(string json)
    {
        if (json is null) throw new ContentPackageValidationException(null, null, "$", "JSON is required.");
        if (json.Length > ContentPackage.MaxJsonBytes || Encoding.UTF8.GetByteCount(json) > ContentPackage.MaxJsonBytes)
            throw new ContentPackageValidationException(null, null, "$", $"JSON exceeds {ContentPackage.MaxJsonBytes} UTF-8 bytes.");
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = ContentPackage.MaxJsonDepth });
            JsonElement root = document.RootElement;
            var v = new PackageJsonValidation(PackageJsonValidation.GuessString(root, "packageId"));
            v.Fields(root, null, "$", "format", "schemaVersion", "packageId", "profileId", "requiredMetadataPackageId", "swords");
            if (v.Text(root.GetProperty("format"), null, "$.format") != Format)
                throw v.Failure(null, "$.format", "Unsupported package format.");
            int version = v.Integer(root.GetProperty("schemaVersion"), null, "$.schemaVersion", 0, int.MaxValue);
            if (version is not (1 or 2))
                throw v.Failure(null, "$.schemaVersion", "Unsupported schema version.");
            string package = v.Id(root.GetProperty("packageId"), null, "$.packageId");
            string profile = v.Id(root.GetProperty("profileId"), null, "$.profileId");
            string dependency = v.Id(root.GetProperty("requiredMetadataPackageId"), null, "$.requiredMetadataPackageId");
            var swords = new List<SwordDefinition>();
            foreach (JsonElement node in v.Array(root.GetProperty("swords"), null, "$.swords", ContentPackage.MaxDefinitionsPerKind))
                swords.Add(ReadSword(node, $"$.swords[{swords.Count}]", v, version));
            return new SwordContentPackage(package, profile, dependency, swords, version);
        }
        catch (JsonException exception)
        {
            throw new ContentPackageValidationException(null, null, exception.Path ?? "$", $"Malformed JSON: {exception.Message}", exception);
        }
    }

    private static SwordDefinition ReadSword(JsonElement node, string path, PackageJsonValidation v, int version)
    {
        string? record = PackageJsonValidation.GuessString(node, "id");
        if (version == 1)
            v.Fields(node, record, path, "id", "type", "range", "power", "formula", "targetingFlags", "evade", "element", "addedEffect");
        else
            v.Fields(node, record, path, "id", "type", "range", "power", "formula", "targetingFlags", "capabilityFlags", "evade", "element", "addedEffect");
        string id = v.Id(node.GetProperty("id"), record, path + ".id");
        int type = v.Integer(node.GetProperty("type"), id, path + ".type", 3, 3);
        int range = v.Integer(node.GetProperty("range"), id, path + ".range", 1, 1);
        int power = v.Integer(node.GetProperty("power"), id, path + ".power", 0, 255);
        int formula = v.Integer(node.GetProperty("formula"), id, path + ".formula", 1, 1);
        int flags = v.Integer(node.GetProperty("targetingFlags"), id, path + ".targetingFlags", 128, 128);
        int capabilities = version == 1 ? 0 : v.Integer(node.GetProperty("capabilityFlags"), id, path + ".capabilityFlags", 0, 14);
        if ((capabilities & ~0x0e) != 0)
            throw v.Failure(id, path + ".capabilityFlags", "Unsupported sword capability bits.");
        int evade = v.Integer(node.GetProperty("evade"), id, path + ".evade", 0, 255);
        int element = v.Integer(node.GetProperty("element"), id, path + ".element", 0, 0);
        string effectPath = path + ".addedEffect";
        JsonElement effect = node.GetProperty("addedEffect");
        v.Fields(effect, id, effectPath, "typeRaw", "statusesRaw");
        int effectType = v.Integer(effect.GetProperty("typeRaw"), id, effectPath + ".typeRaw", version == 1 ? 128 : 0, 128);
        if (effectType is not (0 or 128))
            throw v.Failure(id, effectPath + ".typeRaw", "Only empty modes 0 and 128 are supported.");
        JsonElement statuses = effect.GetProperty("statusesRaw");
        JsonElement.ArrayEnumerator entries = v.Array(statuses, id, effectPath + ".statusesRaw", 5);
        if (statuses.GetArrayLength() != 5) throw v.Failure(id, effectPath + ".statusesRaw", "Exactly five status bytes are required.");
        var raw = new List<byte>();
        foreach (JsonElement entry in entries)
            raw.Add((byte)v.Integer(entry, id, $"{effectPath}.statusesRaw[{raw.Count}]", 0, 0));
        return new SwordDefinition(new WeaponId(id), (SwordWeaponType)type, range, power,
            (SwordDamageFormula)formula, (SwordTargetingFlags)flags, evade, (SwordElement)element,
            new EmptySwordAddedEffect((SwordEmptyProcMode)effectType, raw), (SwordCapabilityFlags)capabilities);
    }

    public string ToJson()
    {
        using var stream = new SwordWriteBuffer(PackageId);
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("schemaVersion", Version);
            writer.WriteString("packageId", PackageId);
            writer.WriteString("profileId", ProfileId);
            writer.WriteString("requiredMetadataPackageId", RequiredMetadataPackageId);
            writer.WriteStartArray("swords");
            foreach (SwordDefinition sword in Swords.OrderBy(sword => sword.Id.Value, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", sword.Id.Value);
                writer.WriteNumber("type", (byte)sword.Type);
                writer.WriteNumber("range", sword.Range);
                writer.WriteNumber("power", sword.Power);
                writer.WriteNumber("formula", (byte)sword.Formula);
                writer.WriteNumber("targetingFlags", (byte)sword.TargetingFlags);
                if (Version == 2) writer.WriteNumber("capabilityFlags", (byte)sword.CapabilityFlags);
                writer.WriteNumber("evade", sword.Evade);
                writer.WriteNumber("element", (byte)sword.Element);
                writer.WriteStartObject("addedEffect");
                writer.WriteNumber("typeRaw", sword.AddedEffect.TypeRaw);
                writer.WriteStartArray("statusesRaw");
                foreach (byte value in sword.AddedEffect.StatusesRaw) writer.WriteNumberValue(value);
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    private sealed class SwordWriteBuffer(string packageId) : MemoryStream
    {
        private void Check(int count)
        {
            if (Position + count > ContentPackage.MaxJsonBytes)
                throw new ContentPackageValidationException(packageId, null, "$", $"Export exceeds {ContentPackage.MaxJsonBytes} UTF-8 bytes.");
        }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
    }
}
