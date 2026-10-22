using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Fft.Content.Definitions;
using Fft.Core;

namespace Fft.Content.Tests.Packages;

// Deliberately test-only: ZOD-015 exercises a candidate wire format, not a production package API.
internal sealed class ContentPackagePrototype
{
    private const string Format = "fft.content-metadata";
    internal string PackageId { get; }
    internal string ProfileId { get; }
    internal string Kind { get; }
    internal string? RequiredBaselinePackageId { get; }
    internal ImmutableArray<JobDefinition> Jobs { get; }
    internal ImmutableArray<SkillsetDefinition> Skillsets { get; }
    internal ImmutableArray<AbilityDefinition> Abilities { get; }

    private ContentPackagePrototype(JsonElement root)
    {
        Fields(root, "$", "format", "schemaVersion", "packageId", "profileId", "kind",
            "requiredBaselinePackageId", "jobs", "skillsets", "abilities");
        if (Text(root, "format") != Format || Number(root, "schemaVersion") != 1)
            throw new InvalidDataException("Unsupported content metadata format/schemaVersion.");
        PackageId = new AssetId(Text(root, "packageId")).Value;
        ProfileId = new AssetId(Text(root, "profileId")).Value;
        Kind = Text(root, "kind");
        if (Kind is not ("baseline" or "addon"))
            throw new InvalidDataException("Unknown package kind.");
        JsonElement required = root.GetProperty("requiredBaselinePackageId");
        RequiredBaselinePackageId = required.ValueKind == JsonValueKind.Null ? null :
            new AssetId(String(required, "requiredBaselinePackageId")).Value;
        if ((Kind == "baseline") != (RequiredBaselinePackageId is null))
            throw new InvalidDataException("Baseline requires null dependency; addon requires a baseline package ID.");

        Jobs = Array(root, "jobs").Select(ReadJob).ToImmutableArray();
        Skillsets = Array(root, "skillsets").Select(ReadSkillset).ToImmutableArray();
        Abilities = Array(root, "abilities").Select(ReadAbility).ToImmutableArray();
        ValidateIds(Jobs.Select(job => job.Id.Value), "job");
        ValidateIds(Skillsets.Select(skillset => skillset.Id.Value), "skillset");
        ValidateIds(Abilities.Select(ability => ability.Id.Value), "ability");
        if (Kind == "baseline")
            _ = new DefinitionCatalog(Jobs, Skillsets, Abilities);
    }

    internal static ContentPackagePrototype Read(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            return new ContentPackagePrototype(document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"Invalid content package: {exception.Message}", exception);
        }
    }

    internal static DefinitionCatalog Compose(ContentPackagePrototype baseline, params ContentPackagePrototype[] addons)
    {
        if (baseline.Kind != "baseline") throw new InvalidDataException("Composition requires an explicit baseline.");
        var packageIds = new HashSet<string>(StringComparer.Ordinal) { baseline.PackageId };
        foreach (ContentPackagePrototype addon in addons)
        {
            if (addon.Kind != "addon" || addon.ProfileId != baseline.ProfileId || addon.RequiredBaselinePackageId != baseline.PackageId)
                throw new InvalidDataException($"Addon '{addon.PackageId}' has an incompatible kind/profile/baseline dependency.");
            if (!packageIds.Add(addon.PackageId)) throw new InvalidDataException($"Duplicate package ID '{addon.PackageId}'.");
        }

        try
        {
            ContentPackagePrototype[] packages = [baseline, .. addons.OrderBy(addon => addon.PackageId, StringComparer.Ordinal)];
            return new DefinitionCatalog(
                packages.SelectMany(package => package.Jobs).OrderBy(job => job.Id.Value, StringComparer.Ordinal),
                packages.SelectMany(package => package.Skillsets).OrderBy(skillset => skillset.Id.Value, StringComparer.Ordinal),
                packages.SelectMany(package => package.Abilities).OrderBy(ability => ability.Id.Value, StringComparer.Ordinal));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"Invalid composed metadata: {exception.Message}", exception);
        }
    }

    private void ValidateIds(IEnumerable<string> ids, string kind)
    {
        string owner = PackageId[..PackageId.IndexOf(':')];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (id[..id.IndexOf(':')] != owner)
                throw new InvalidDataException($"Package '{PackageId}' cannot declare {kind} '{id}' outside its namespace '{owner}'.");
            if (!seen.Add(id)) throw new InvalidDataException($"Duplicate {kind} ID '{id}'.");
        }
    }

    private static JobDefinition ReadJob(JsonElement node)
    {
        Fields(node, "job", "id", "primarySkillsetId", "baseMove", "baseJump", "steppingStone", "innateAbilityIds");
        return new JobDefinition(new JobId(Text(node, "id")), new SkillsetId(Text(node, "primarySkillsetId")),
            Number(node, "baseMove"), Number(node, "baseJump"), Boolean(node, "steppingStone"),
            Array(node, "innateAbilityIds").Select(id => new AbilityId(String(id, "innateAbilityIds"))));
    }

    private static SkillsetDefinition ReadSkillset(JsonElement node)
    {
        Fields(node, "skillset", "id", "slots");
        return new SkillsetDefinition(new SkillsetId(Text(node, "id")), Array(node, "slots").Select(slot =>
        {
            Fields(slot, "slot", "index", "abilityId");
            JsonElement reference = slot.GetProperty("abilityId");
            return new SkillsetSlot(Number(slot, "index"), reference.ValueKind == JsonValueKind.Null ? null :
                new AbilityId(String(reference, "abilityId")));
        }));
    }

    private static AbilityDefinition ReadAbility(JsonElement node)
    {
        Fields(node, "ability", "id", "jpCost", "learnOnHitRateRaw", "serializedClassRaw", "flags", "unknownTypeBitsRaw", "aiFlagsRaw");
        int rawClass = Number(node, "serializedClassRaw");
        int unknown = Number(node, "unknownTypeBitsRaw");
        if (rawClass is < 0 or > 15 || unknown is not (0 or 0x10))
            throw new InvalidDataException("serializedClassRaw must be 0..15; unknownTypeBitsRaw must be 0 or 16.");
        int raw = rawClass | unknown;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement flag in Array(node, "flags"))
        {
            string name = String(flag, "flags");
            if (!seen.Add(name)) throw new InvalidDataException($"Duplicate named flag '{name}'.");
            raw |= name switch
            {
                "learn-on-hit" => 0x20,
                "show-name" => 0x40,
                "cannot-learn-with-jp" => 0x80,
                _ => throw new InvalidDataException($"Unknown named flag '{name}'."),
            };
        }

        return new AbilityDefinition(new AbilityId(Text(node, "id")), Number(node, "jpCost"),
            Number(node, "learnOnHitRateRaw"), (byte)raw,
            Array(node, "aiFlagsRaw").Select(value => Byte(value, "aiFlagsRaw")));
    }

    internal string Write()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("packageId", PackageId);
            writer.WriteString("profileId", ProfileId);
            writer.WriteString("kind", Kind);
            writer.WriteString("requiredBaselinePackageId", RequiredBaselinePackageId);
            writer.WriteStartArray("jobs");
            foreach (JobDefinition job in Jobs.OrderBy(job => job.Id.Value, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", job.Id.Value);
                writer.WriteString("primarySkillsetId", job.PrimarySkillsetId.Value);
                writer.WriteNumber("baseMove", job.BaseMove);
                writer.WriteNumber("baseJump", job.BaseJump);
                writer.WriteBoolean("steppingStone", job.SteppingStone);
                writer.WriteStartArray("innateAbilityIds");
                foreach (AbilityId id in job.InnateAbilityIds) writer.WriteStringValue(id.Value);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("skillsets");
            foreach (SkillsetDefinition skillset in Skillsets.OrderBy(skillset => skillset.Id.Value, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", skillset.Id.Value);
                writer.WriteStartArray("slots");
                foreach (SkillsetSlot slot in skillset.Slots)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("index", slot.Slot);
                    writer.WriteString("abilityId", slot.AbilityId?.Value);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("abilities");
            foreach (AbilityDefinition ability in Abilities.OrderBy(ability => ability.Id.Value, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", ability.Id.Value);
                writer.WriteNumber("jpCost", ability.JpCost);
                writer.WriteNumber("learnOnHitRateRaw", ability.LearnOnHitRateRaw);
                writer.WriteNumber("serializedClassRaw", ability.SerializedClassRaw);
                writer.WriteStartArray("flags");
                if (ability.LearnOnHit) writer.WriteStringValue("learn-on-hit");
                if (ability.ShowName) writer.WriteStringValue("show-name");
                if (!ability.CanLearnWithJp) writer.WriteStringValue("cannot-learn-with-jp");
                writer.WriteEndArray();
                writer.WriteNumber("unknownTypeBitsRaw", ability.UnknownTypeFlags);
                writer.WriteStartArray("aiFlagsRaw");
                foreach (byte flag in ability.AiFlagsRaw) writer.WriteNumberValue(flag);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static void Fields(JsonElement node, string context, params string[] expected)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"{context} must be an object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in node.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw new InvalidDataException($"Duplicate field '{context}.{property.Name}'.");
            if (!expected.Contains(property.Name, StringComparer.Ordinal)) throw new InvalidDataException($"Unknown field '{context}.{property.Name}'.");
        }
        foreach (string field in expected)
            if (!seen.Contains(field)) throw new InvalidDataException($"Missing field '{context}.{field}'.");
    }

    private static JsonElement.ArrayEnumerator Array(JsonElement node, string name)
    {
        JsonElement value = node.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"{name} must be an array.");
        return value.EnumerateArray();
    }

    private static string Text(JsonElement node, string name) => String(node.GetProperty(name), name);
    private static string String(JsonElement node, string name) => node.ValueKind == JsonValueKind.String
        ? node.GetString()! : throw new InvalidDataException($"{name} must be a string.");
    private static int Number(JsonElement node, string name) => Integer(node.GetProperty(name), name);
    private static int Integer(JsonElement node, string name) => node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out int value)
        ? value : throw new InvalidDataException($"{name} must be an integer in Int32 range.");
    private static byte Byte(JsonElement node, string name)
    {
        int value = Integer(node, name);
        return value is >= 0 and <= 255 ? (byte)value : throw new InvalidDataException($"{name} byte must be 0..255.");
    }
    private static bool Boolean(JsonElement node, string name)
    {
        JsonElement value = node.GetProperty(name);
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() :
            throw new InvalidDataException($"{name} must be a boolean.");
    }
}
