using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Zodiac.Content.Definitions;

namespace Zodiac.Content.ContentPackages;

/// <summary>Explicit executable companion to learning metadata. No unknown effect is defaulted or executed.</summary>
public sealed class ImmediateAbilityContentPackage
{
    public const string Format = "fft.immediate-ability-content";
    public const int SchemaVersion = 1;
    public string PackageId { get; }
    public string ProfileId { get; }
    public string RequiredMetadataPackageId { get; }
    public ImmutableArray<ImmediateAbilityDefinition> Abilities { get; }

    public ImmediateAbilityContentPackage(string packageId, string profileId, string requiredMetadataPackageId,
        IEnumerable<ImmediateAbilityDefinition> abilities)
    {
        var v = new PackageJsonValidation(packageId);
        PackageId = v.Id(packageId, null, "$.packageId");
        ProfileId = v.Id(profileId, null, "$.profileId");
        RequiredMetadataPackageId = v.Id(requiredMetadataPackageId, null, "$.requiredMetadataPackageId");
        if (PackageId == RequiredMetadataPackageId) throw v.Failure(null, "$.packageId", "The companion requires its own package identity.");
        if (abilities is null) throw v.Failure(null, "$.abilities", "An ability collection is required.");
        var selected = ImmutableArray.CreateBuilder<ImmediateAbilityDefinition>();
        var ids = new HashSet<AbilityId>();
        string owner = PackageId[..PackageId.IndexOf(':')];
        foreach (var ability in abilities)
        {
            string path = $"$.abilities[{selected.Count}]";
            if (selected.Count == ContentPackage.MaxDefinitionsPerKind) throw v.Failure(null, "$.abilities", "Too many abilities.");
            if (ability is null) throw v.Failure(null, path, "An ability cannot be null.");
            string id = v.Id(ability.Id.Value, ability.Id.Value, path + ".id");
            if (id[..id.IndexOf(':')] != owner) throw v.Failure(id, path + ".id", "Ability must belong to the companion's namespace.");
            if (!ids.Add(ability.Id)) throw v.Failure(id, path + ".id", "Duplicate ability.");
            selected.Add(ability);
        }
        Abilities = selected.ToImmutable();
    }

    public void ValidateAgainst(ContentPackage metadata)
    {
        var v = new PackageJsonValidation(PackageId);
        if (metadata is null || metadata.PackageId != RequiredMetadataPackageId)
            throw v.Failure(null, "$.requiredMetadataPackageId", "Select the declared metadata dependency explicitly.");
        if (metadata.ProfileId != ProfileId) throw v.Failure(null, "$.profileId", "Metadata and effect profiles differ.");
        var declared = metadata.Abilities.ToDictionary(ability => ability.Id);
        foreach (var ability in Abilities)
            if (!declared.TryGetValue(ability.Id, out var learning) || learning.SerializedClassRaw != 1)
                throw v.Failure(ability.Id.Value, "$.abilities", "An effect requires matching ordinary-action learning metadata.");
    }

    public static ImmediateAbilityContentPackage ReadJson(string json)
    {
        if (json is null || json.Length > ContentPackage.MaxJsonBytes || Encoding.UTF8.GetByteCount(json) > ContentPackage.MaxJsonBytes)
            throw new ContentPackageValidationException(null, null, "$", "Missing or oversized JSON.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = ContentPackage.MaxJsonDepth });
            var root = document.RootElement;
            var v = new PackageJsonValidation(PackageJsonValidation.GuessString(root, "packageId"));
            v.Fields(root, null, "$", "format", "schemaVersion", "packageId", "profileId", "requiredMetadataPackageId", "abilities");
            if (v.Text(root.GetProperty("format"), null, "$.format") != Format) throw v.Failure(null, "$.format", "Unsupported format.");
            _ = v.Integer(root.GetProperty("schemaVersion"), null, "$.schemaVersion", 1, 1);
            var abilities = new List<ImmediateAbilityDefinition>();
            foreach (var node in v.Array(root.GetProperty("abilities"), null, "$.abilities", ContentPackage.MaxDefinitionsPerKind))
            {
                string path = $"$.abilities[{abilities.Count}]";
                string? id = PackageJsonValidation.GuessString(node, "id");
                v.Fields(node, id, path, "id", "effect", "target", "amount", "chargeTicks", "mpCost");
                id = v.Id(node.GetProperty("id"), id, path + ".id");
                if (v.Text(node.GetProperty("effect"), id, path + ".effect") != "self-pa-increase")
                    throw v.Failure(id, path + ".effect", "Unsupported effect.");
                if (v.Text(node.GetProperty("target"), id, path + ".target") != "self")
                    throw v.Failure(id, path + ".target", "Only self targeting is supported.");
                int amount = v.Integer(node.GetProperty("amount"), id, path + ".amount", 1, 1);
                int charge = v.Integer(node.GetProperty("chargeTicks"), id, path + ".chargeTicks", 0, 0);
                int mp = v.Integer(node.GetProperty("mpCost"), id, path + ".mpCost", 0, 0);
                abilities.Add(new(new(id), ImmediateAbilityEffect.SelfPhysicalAttackIncrease, amount, charge, mp));
            }
            return new(v.Id(root.GetProperty("packageId"), null, "$.packageId"),
                v.Id(root.GetProperty("profileId"), null, "$.profileId"),
                v.Id(root.GetProperty("requiredMetadataPackageId"), null, "$.requiredMetadataPackageId"), abilities);
        }
        catch (JsonException exception)
        { throw new ContentPackageValidationException(null, null, exception.Path ?? "$", "Malformed JSON.", exception); }
    }

    public string ToJson()
    {
        string json = JsonSerializer.Serialize(new
        {
            format = Format, schemaVersion = SchemaVersion, packageId = PackageId, profileId = ProfileId,
            requiredMetadataPackageId = RequiredMetadataPackageId,
            abilities = Abilities.OrderBy(ability => ability.Id.Value, StringComparer.Ordinal).Select(ability => new
            { id = ability.Id.Value, effect = "self-pa-increase", target = "self", amount = ability.Amount, chargeTicks = ability.ChargeTicks, mpCost = ability.MpCost }),
        }, new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n";
        if (Encoding.UTF8.GetByteCount(json) > ContentPackage.MaxJsonBytes)
            throw new ContentPackageValidationException(PackageId, null, "$", "Serialized package is oversized.");
        return json;
    }
}
