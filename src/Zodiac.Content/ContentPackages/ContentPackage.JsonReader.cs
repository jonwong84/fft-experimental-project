using System.Text;
using System.Text.Json;
using Zodiac.Content.Definitions;

namespace Zodiac.Content.ContentPackages;

public sealed partial class ContentPackage
{
    public static ContentPackage ReadJson(string json)
    {
        if (json is null) throw new ContentPackageValidationException(null, null, "$", "JSON is required.");
        if (json.Length > MaxJsonBytes || Encoding.UTF8.GetByteCount(json) > MaxJsonBytes)
            throw new ContentPackageValidationException(null, null, "$", $"JSON exceeds {MaxJsonBytes} UTF-8 bytes.");
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
            return new PackageJsonReader(document.RootElement).Read();
        }
        catch (JsonException exception)
        {
            throw new ContentPackageValidationException(null, null, exception.Path ?? "$", $"Malformed JSON: {exception.Message}", exception);
        }
    }

    private sealed class PackageJsonReader
    {
        private readonly JsonElement root;
        private readonly string? packageId;
        private readonly PackageJsonValidation validation;
        private int referenceCount;

        internal PackageJsonReader(JsonElement root)
        {
            this.root = root;
            packageId = GuessString(root, "packageId");
            validation = new PackageJsonValidation(packageId);
        }

        internal ContentPackage Read()
        {
            Fields(root, null, "$", "format", "schemaVersion", "packageId", "profileId", "kind",
                "requiredBaselinePackageId", "jobs", "skillsets", "abilities");
            if (Text(root.GetProperty("format"), null, "$.format") != Format)
                throw Failure(null, "$.format", "Unsupported package format.");
            if (Integer(root.GetProperty("schemaVersion"), null, "$.schemaVersion", 0, int.MaxValue) != SchemaVersion)
                throw Failure(null, "$.schemaVersion", "Unsupported schema version.");
            string package = Id(root.GetProperty("packageId"), null, "$.packageId");
            string profile = Id(root.GetProperty("profileId"), null, "$.profileId");
            ContentPackageKind kind = Text(root.GetProperty("kind"), null, "$.kind") switch
            {
                "baseline" => ContentPackageKind.Baseline,
                "addon" => ContentPackageKind.Addon,
                _ => throw Failure(null, "$.kind", "Unknown package kind; expected baseline or addon."),
            };
            JsonElement dependency = root.GetProperty("requiredBaselinePackageId");
            string? required = dependency.ValueKind == JsonValueKind.Null ? null : Id(dependency, null, "$.requiredBaselinePackageId");
            var jobs = new List<JobDefinition>();
            foreach (JsonElement job in Array(root.GetProperty("jobs"), null, "$.jobs", MaxDefinitionsPerKind))
                jobs.Add(ReadJob(job, $"$.jobs[{jobs.Count}]"));
            var skillsets = new List<SkillsetDefinition>();
            foreach (JsonElement skillset in Array(root.GetProperty("skillsets"), null, "$.skillsets", MaxDefinitionsPerKind))
                skillsets.Add(ReadSkillset(skillset, $"$.skillsets[{skillsets.Count}]"));
            var abilities = new List<AbilityDefinition>();
            foreach (JsonElement ability in Array(root.GetProperty("abilities"), null, "$.abilities", MaxDefinitionsPerKind))
                abilities.Add(ReadAbility(ability, $"$.abilities[{abilities.Count}]"));
            return new ContentPackage(package, profile, kind, required, jobs, skillsets, abilities);
        }

        private JobDefinition ReadJob(JsonElement node, string path)
        {
            string? record = GuessString(node, "id");
            Fields(node, record, path, "id", "primarySkillsetId", "baseMove", "baseJump", "steppingStone", "innateAbilityIds");
            string id = Id(node.GetProperty("id"), record, path + ".id");
            var primary = new SkillsetId(Id(node.GetProperty("primarySkillsetId"), id, path + ".primarySkillsetId"));
            CountReferences(1, id, path + ".primarySkillsetId");
            int move = Integer(node.GetProperty("baseMove"), id, path + ".baseMove", 0, 255);
            int jump = Integer(node.GetProperty("baseJump"), id, path + ".baseJump", 0, 127);
            JsonElement stepping = node.GetProperty("steppingStone");
            if (stepping.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw Failure(id, path + ".steppingStone", "Expected a boolean.");
            var innates = new List<AbilityId>();
            foreach (JsonElement innate in Array(node.GetProperty("innateAbilityIds"), id, path + ".innateAbilityIds", MaxInnatesPerJob))
                innates.Add(new AbilityId(Id(innate, id, $"{path}.innateAbilityIds[{innates.Count}]")));
            CountReferences(innates.Count, id, path + ".innateAbilityIds");
            return new JobDefinition(new JobId(id), primary, move, jump, stepping.GetBoolean(), innates);
        }

        private SkillsetDefinition ReadSkillset(JsonElement node, string path)
        {
            string? record = GuessString(node, "id");
            Fields(node, record, path, "id", "slots");
            string id = Id(node.GetProperty("id"), record, path + ".id");
            var slots = new List<SkillsetSlot>();
            var indices = new HashSet<int>();
            foreach (JsonElement slot in Array(node.GetProperty("slots"), id, path + ".slots", MaxSlotsPerSkillset))
            {
                string slotPath = $"{path}.slots[{slots.Count}]";
                Fields(slot, id, slotPath, "index", "abilityId");
                int index = Integer(slot.GetProperty("index"), id, slotPath + ".index", 0, int.MaxValue);
                if (!indices.Add(index)) throw Failure(id, slotPath + ".index", $"Duplicate slot index {index}.");
                JsonElement reference = slot.GetProperty("abilityId");
                slots.Add(new SkillsetSlot(index, reference.ValueKind == JsonValueKind.Null ? null :
                    new AbilityId(Id(reference, id, slotPath + ".abilityId"))));
            }
            CountReferences(slots.Count, id, path + ".slots");
            return new SkillsetDefinition(new SkillsetId(id), slots);
        }

        private AbilityDefinition ReadAbility(JsonElement node, string path)
        {
            string? record = GuessString(node, "id");
            Fields(node, record, path, "id", "jpCost", "learnOnHitRateRaw", "serializedClassRaw", "flags", "unknownTypeBitsRaw", "aiFlagsRaw");
            string id = Id(node.GetProperty("id"), record, path + ".id");
            int cost = Integer(node.GetProperty("jpCost"), id, path + ".jpCost", 0, 65535);
            int rate = Integer(node.GetProperty("learnOnHitRateRaw"), id, path + ".learnOnHitRateRaw", 0, 255);
            int raw = Integer(node.GetProperty("serializedClassRaw"), id, path + ".serializedClassRaw", 0, 15);
            int unknown = Integer(node.GetProperty("unknownTypeBitsRaw"), id, path + ".unknownTypeBitsRaw", 0, 16);
            if (unknown is not (0 or 16)) throw Failure(id, path + ".unknownTypeBitsRaw", "Only opaque bit 0x10 (0 or 16) is permitted.");
            raw |= unknown;
            var flags = new HashSet<string>(StringComparer.Ordinal);
            int flagIndex = 0;
            foreach (JsonElement flag in Array(node.GetProperty("flags"), id, path + ".flags", 3))
            {
                string flagPath = $"{path}.flags[{flagIndex++}]";
                string name = Text(flag, id, flagPath);
                if (!flags.Add(name)) throw Failure(id, flagPath, $"Duplicate named flag '{name}'.");
                raw |= name switch
                {
                    "learn-on-hit" => 0x20,
                    "show-name" => 0x40,
                    "cannot-learn-with-jp" => 0x80,
                    _ => throw Failure(id, flagPath, $"Unknown named flag '{name}'."),
                };
            }
            JsonElement ai = node.GetProperty("aiFlagsRaw");
            JsonElement.ArrayEnumerator aiValues = Array(ai, id, path + ".aiFlagsRaw", 4);
            if (ai.GetArrayLength() != 4) throw Failure(id, path + ".aiFlagsRaw", "Exactly four raw AI flag bytes are required.");
            var aiBytes = new List<byte>();
            foreach (JsonElement value in aiValues)
                aiBytes.Add((byte)Integer(value, id, $"{path}.aiFlagsRaw[{aiBytes.Count}]", 0, 255));
            return new AbilityDefinition(new AbilityId(id), cost, rate, (byte)raw, aiBytes);
        }

        private void CountReferences(int count, string record, string path)
        {
            referenceCount += count;
            if (referenceCount > MaxTotalReferences) throw Failure(record, path, $"Package exceeds {MaxTotalReferences} reference/slot entries.");
        }

        private void Fields(JsonElement node, string? record, string path, params string[] fields)
            => validation.Fields(node, record, path, fields);

        private JsonElement.ArrayEnumerator Array(JsonElement node, string? record, string path, int maximum)
            => validation.Array(node, record, path, maximum);

        private string Id(JsonElement node, string? record, string path) => validation.Id(node, record, path);
        private string Text(JsonElement node, string? record, string path) => validation.Text(node, record, path);
        private int Integer(JsonElement node, string? record, string path, int minimum, int maximum)
            => validation.Integer(node, record, path, minimum, maximum);
        private ContentPackageValidationException Failure(string? record, string path, string message) => new(packageId, record, path, message);
        private static string? GuessString(JsonElement node, string property) =>
            PackageJsonValidation.GuessString(node, property);
    }
}
