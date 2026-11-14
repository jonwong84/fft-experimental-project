using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Zodiac.Content.Definitions;
using Zodiac.Framework.Formulas;
using Zodiac.Framework;

namespace Zodiac.Content.ContentPackages;

/// <summary>Strict closed targeted-effect companion; never enables effects from learning metadata alone.</summary>
public sealed class TargetedAbilityContentPackage
{
    public const string Format = "fft.targeted-ability-content";
    public const int SchemaVersion = 1;
    public const int CustomSchemaVersion = 2;
    public string PackageId { get; }
    public string ProfileId { get; }
    public string RequiredMetadataPackageId { get; }
    public ImmutableArray<TargetedAbilityDefinition> Abilities { get; }
    public int Version => Abilities.Any(a => a.IsCustom) ? CustomSchemaVersion : SchemaVersion;

    public TargetedAbilityContentPackage(string packageId, string profileId, string requiredMetadataPackageId,
        IEnumerable<TargetedAbilityDefinition> abilities)
    {
        var v = new PackageJsonValidation(packageId);
        PackageId = v.Id(packageId, null, "$.packageId");
        ProfileId = v.Id(profileId, null, "$.profileId");
        RequiredMetadataPackageId = v.Id(requiredMetadataPackageId, null, "$.requiredMetadataPackageId");
        if (PackageId == RequiredMetadataPackageId) throw v.Failure(null, "$.packageId", "Companion identity must differ from its metadata dependency.");
        if (abilities is null) throw v.Failure(null, "$.abilities", "An ability collection is required.");
        var selected = ImmutableArray.CreateBuilder<TargetedAbilityDefinition>();
        var ids = new HashSet<AbilityId>();
        string owner = PackageId[..PackageId.IndexOf(':')];
        foreach (var ability in abilities)
        {
            string path = $"$.abilities[{selected.Count}]";
            if (selected.Count == ContentPackage.MaxDefinitionsPerKind) throw v.Failure(null, "$.abilities", "Too many abilities.");
            if (ability is null) throw v.Failure(null, path, "An ability cannot be null.");
            if (ability.Formula.Parameters.Z != 0)
                throw v.Failure(ability.Id.Value, path + ".formula", "The targeted ability content schemas cannot preserve a nonzero Z operand.");
            string id = v.Id(ability.Id.Value, ability.Id.Value, path + ".id");
            if (id[..id.IndexOf(':')] != owner) throw v.Failure(id, path + ".id", "Ability must belong to the companion namespace.");
            if (!ids.Add(ability.Id)) throw v.Failure(id, path + ".id", "Duplicate ability.");
            selected.Add(ability);
        }
        Abilities = selected.ToImmutable();
    }

    public void ValidateAgainst(ContentPackage metadata)
    {
        var v = new PackageJsonValidation(PackageId);
        if (metadata is null || metadata.PackageId != RequiredMetadataPackageId)
            throw v.Failure(null, "$.requiredMetadataPackageId", "Select the declared metadata explicitly.");
        if (metadata.ProfileId != ProfileId) throw v.Failure(null, "$.profileId", "Metadata and effect profiles differ.");
        var declared = metadata.Abilities.ToDictionary(ability => ability.Id);
        foreach (var ability in Abilities)
            if (!declared.TryGetValue(ability.Id, out var learning) || learning.SerializedClassRaw != 1)
                throw v.Failure(ability.Id.Value, "$.abilities", "An effect requires matching ordinary-action learning metadata.");
    }

    public static TargetedAbilityContentPackage ReadJson(string json)
    {
        if (json is null || json.Length > ContentPackage.MaxJsonBytes || Encoding.UTF8.GetByteCount(json) > ContentPackage.MaxJsonBytes)
            throw new ContentPackageValidationException(null, null, "$", "Missing or oversized JSON.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = ContentPackage.MaxJsonDepth });
            var root = document.RootElement;
            var v = new PackageJsonValidation(PackageJsonValidation.GuessString(root, "packageId"));
            if (root.ValueKind != JsonValueKind.Object) throw v.Failure(null, "$", "Expected an object.");
            if (!root.TryGetProperty("schemaVersion", out var versionNode)) throw v.Failure(null, "$.schemaVersion", "Required field is missing.");
            int version = v.Integer(versionNode, null, "$.schemaVersion", 1, 2);
            if (version == 1) v.Fields(root, null, "$", "format", "schemaVersion", "packageId", "profileId", "requiredMetadataPackageId", "abilities");
            else
            {
                v.Fields(root, null, "$", "format", "schemaVersion", "ruleset", "packageId", "profileId", "requiredMetadataPackageId", "abilities");
                if (v.Text(root.GetProperty("ruleset"), null, "$.ruleset") != "custom") throw v.Failure(null, "$.ruleset", "Formula edits require an explicit custom ruleset.");
            }
            if (v.Text(root.GetProperty("format"), null, "$.format") != Format) throw v.Failure(null, "$.format", "Unsupported format.");
            var abilities = new List<TargetedAbilityDefinition>();
            foreach (var node in v.Array(root.GetProperty("abilities"), null, "$.abilities", ContentPackage.MaxDefinitionsPerKind))
            {
                string path = $"$.abilities[{abilities.Count}]";
                string? id = PackageJsonValidation.GuessString(node, "id");
                if (version == 2) { abilities.Add(ReadCustom(node, v, path, id)); continue; }
                v.Fields(node, id, path, "id", "effect", "target", "range", "maximumDamageMultiplier", "chargeTicks", "mpCost");
                id = v.Id(node.GetProperty("id"), id, path + ".id");
                var effect = v.Text(node.GetProperty("effect"), id, path + ".effect") switch
                {
                    "throw-stone-flat" => TargetedAbilityEffect.ThrowStoneFlat,
                    "dash-flat" => TargetedAbilityEffect.DashFlat,
                    _ => throw v.Failure(id, path + ".effect", "Unsupported effect."),
                };
                if (v.Text(node.GetProperty("target"), id, path + ".target") != "occupied-unit") throw v.Failure(id, path + ".target", "Only the immediate occupied-unit subset is supported.");
                int expectedRange = effect == TargetedAbilityEffect.DashFlat ? 1 : 4;
                int expectedMaximum = effect == TargetedAbilityEffect.DashFlat ? 4 : 2;
                int range = v.Integer(node.GetProperty("range"), id, path + ".range", expectedRange, expectedRange);
                int maximum = v.Integer(node.GetProperty("maximumDamageMultiplier"), id, path + ".maximumDamageMultiplier", expectedMaximum, expectedMaximum);
                int charge = v.Integer(node.GetProperty("chargeTicks"), id, path + ".chargeTicks", 0, 0);
                int mp = v.Integer(node.GetProperty("mpCost"), id, path + ".mpCost", 0, 0);
                abilities.Add(new(new(id), effect, range, maximum, charge, mp));
            }
            if (version == 2 && abilities.Count == 0) throw v.Failure(null, "$.abilities", "A custom formula companion requires an ability.");
            return new(v.Id(root.GetProperty("packageId"), null, "$.packageId"), v.Id(root.GetProperty("profileId"), null, "$.profileId"),
                v.Id(root.GetProperty("requiredMetadataPackageId"), null, "$.requiredMetadataPackageId"), abilities);
        }
        catch (JsonException exception)
        { throw new ContentPackageValidationException(null, null, exception.Path ?? "$", "Malformed JSON.", exception); }
    }

    public string ToJson()
    {
        object document = Version == 2 ? new
        {
            format = Format, schemaVersion = CustomSchemaVersion, ruleset = "custom", packageId = PackageId,
            profileId = ProfileId, requiredMetadataPackageId = RequiredMetadataPackageId,
            abilities = Abilities.OrderBy(a => a.Id.Value, StringComparer.Ordinal).Select(a => new
            {
                id = a.Id.Value, delivery = a.Delivery == PhysicalAbilityDelivery.AdjacentUnit ? "adjacent-unit" : "direct-projectile",
                target = "occupied-unit", range = a.Range, evadeable = a.Definition.Evadeable,
                formula = (int)a.Formula.Id, x = a.Formula.Parameters.X, y = a.Formula.Parameters.Y, chargeTicks = a.ChargeTicks, mpCost = a.MpCost
            })
        } : new
        {
            format = Format, schemaVersion = SchemaVersion, packageId = PackageId, profileId = ProfileId, requiredMetadataPackageId = RequiredMetadataPackageId,
            abilities = Abilities.OrderBy(ability => ability.Id.Value, StringComparer.Ordinal).Select(ability => new
            { id = ability.Id.Value, effect = ability.Effect == TargetedAbilityEffect.DashFlat ? "dash-flat" : "throw-stone-flat", target = "occupied-unit", range = ability.Range,
              maximumDamageMultiplier = ability.MaximumDamageMultiplier, chargeTicks = ability.ChargeTicks, mpCost = ability.MpCost }),
        };
        string json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n";
        if (Encoding.UTF8.GetByteCount(json) > ContentPackage.MaxJsonBytes) throw new ContentPackageValidationException(PackageId, null, "$", "Serialized package is oversized.");
        return json;
    }

    private static TargetedAbilityDefinition ReadCustom(JsonElement node, PackageJsonValidation v, string path, string? id)
    {
        v.Fields(node, id, path, "id", "delivery", "target", "range", "evadeable", "formula", "x", "y", "chargeTicks", "mpCost");
        id = v.Id(node.GetProperty("id"), id, path + ".id");
        var delivery = v.Text(node.GetProperty("delivery"), id, path + ".delivery") switch
        {
            "adjacent-unit" => PhysicalAbilityDelivery.AdjacentUnit,
            "direct-projectile" => PhysicalAbilityDelivery.DirectProjectile,
            _ => throw v.Failure(id, path + ".delivery", "Unsupported delivery profile.")
        };
        if (v.Text(node.GetProperty("target"), id, path + ".target") != "occupied-unit") throw v.Failure(id, path + ".target", "Select occupied-unit targeting.");
        int range = delivery == PhysicalAbilityDelivery.AdjacentUnit ? 1 : 4;
        _ = v.Integer(node.GetProperty("range"), id, path + ".range", range, range);
        var evade = node.GetProperty("evadeable");
        if (evade.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || evade.GetBoolean() != (delivery == PhysicalAbilityDelivery.DirectProjectile))
            throw v.Failure(id, path + ".evadeable", "The selected delivery retains its admitted execution flags.");
        _ = v.Integer(node.GetProperty("chargeTicks"), id, path + ".chargeTicks", 0, 0);
        _ = v.Integer(node.GetProperty("mpCost"), id, path + ".mpCost", 0, 0);
        int formula = v.Integer(node.GetProperty("formula"), id, path + ".formula", 1, int.MaxValue);
        int x = v.Integer(node.GetProperty("x"), id, path + ".x", 0, 255), y = v.Integer(node.GetProperty("y"), id, path + ".y", 0, 255);
        try { return new(new(id), delivery, new AbilityFormula((AbilityFormulaId)formula, new(x, y))); }
        catch (Exception e) when (e is NotSupportedException or ArgumentException)
        { throw v.Failure(id, path + ".formula", e.Message); }
    }
}
