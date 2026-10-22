using System.Text.Json.Nodes;
using Fft.Content.ContentPackages;
using Fft.Content.Definitions;
using Xunit;

namespace Fft.Content.Tests.Packages;

public sealed class SwordContentPackageTests
{
    private const string Sample = """
    {"format":"fft.sword-content","schemaVersion":1,"packageId":"sample:package/swords",
     "profileId":"sample:profile/attack","requiredMetadataPackageId":"sample:package/metadata",
     "swords":[{"id":"sample:item/plain-sword","type":3,"range":1,"power":5,"formula":1,
       "targetingFlags":128,"evade":0,"element":0,
       "addedEffect":{"typeRaw":128,"statusesRaw":[0,0,0,0,0]}}]}
    """;

    [Fact]
    public void Authored_sword_and_json_roundtrip_identically_without_mutating_metadata()
    {
        var bytes = new byte[5];
        var effect = new EmptySwordAddedEffect(128, bytes);
        var sword = new SwordDefinition(new WeaponId("sample:item/plain-sword"), SwordWeaponType.Sword,
            1, 5, SwordDamageFormula.WeaponDamage, SwordTargetingFlags.Striking, 0, SwordElement.Neutral, effect);
        var source = new List<SwordDefinition> { sword };
        var package = new SwordContentPackage("sample:package/swords", "sample:profile/attack", "sample:package/metadata", source);
        source.Clear(); bytes[0] = 255;
        ContentPackage metadata = Metadata();
        string previous = metadata.ToJson();
        package.ValidateAgainst(metadata);
        Assert.Equal(previous, metadata.ToJson());
        Assert.Equal(package.ToJson(), SwordContentPackage.ReadJson(Sample).ToJson());
        SwordContentPackage reloaded = SwordContentPackage.ReadJson(package.ToJson());
        Assert.Equal(package.ToJson(), reloaded.ToJson());
        Assert.Equal(new byte[5], Assert.Single(reloaded.Swords).AddedEffect.StatusesRaw);
        Assert.EndsWith("\n", reloaded.ToJson());
        Assert.DoesNotContain("\r", reloaded.ToJson());
        Assert.Empty(ContentPackage.Compose(metadata).Abilities);
    }

    [Theory]
    [InlineData("type", 2)]
    [InlineData("range", 2)]
    [InlineData("power", -1)]
    [InlineData("power", 256)]
    [InlineData("formula", 2)]
    [InlineData("targetingFlags", 132)]
    [InlineData("evade", -1)]
    [InlineData("evade", 256)]
    [InlineData("element", 1)]
    public void Unsupported_fields_have_package_record_and_precise_path(string field, int value)
    {
        JsonObject root = Parse();
        root["swords"]![0]![field] = value;
        var error = Assert.Throws<ContentPackageValidationException>(() => SwordContentPackage.ReadJson(root.ToJsonString()));
        Assert.Equal("sample:package/swords", error.PackageId);
        Assert.Equal("sample:item/plain-sword", error.RecordId);
        Assert.Equal("$.swords[0]." + field, error.FieldPath);
    }

    [Theory]
    [InlineData("mode", "$.swords[0].addedEffect.typeRaw")]
    [InlineData("status", "$.swords[0].addedEffect.statusesRaw[2]")]
    [InlineData("count", "$.swords[0].addedEffect.statusesRaw")]
    [InlineData("unknown", "$.swords[0].addedEffect.extra")]
    [InlineData("missing", "$.swords[0].addedEffect")]
    public void Empty_added_effect_is_explicit_and_strict(string change, string path)
    {
        JsonObject root = Parse();
        JsonObject sword = root["swords"]![0]!.AsObject();
        switch (change)
        {
            case "mode": sword["addedEffect"]!["typeRaw"] = 64; break;
            case "status": sword["addedEffect"]!["statusesRaw"]![2] = 1; break;
            case "count": sword["addedEffect"]!["statusesRaw"] = new JsonArray(0, 0); break;
            case "unknown": sword["addedEffect"]!["extra"] = true; break;
            case "missing": sword.Remove("addedEffect"); break;
        }
        var error = Assert.Throws<ContentPackageValidationException>(() => SwordContentPackage.ReadJson(root.ToJsonString()));
        Assert.Equal(path, error.FieldPath);
    }

    [Fact]
    public void Direct_definitions_reject_unsupported_configuration_instead_of_guessing_defaults()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmptySwordAddedEffect(0, new byte[5]));
        Assert.Throws<ArgumentException>(() => new EmptySwordAddedEffect(128, new byte[4]));
        Assert.Throws<ArgumentException>(() => new EmptySwordAddedEffect(128, new byte[] { 0, 0, 1, 0, 0 }));
        Assert.Throws<ArgumentException>(() => new SwordDefinition(default, SwordWeaponType.Sword, 1, 5,
            SwordDamageFormula.WeaponDamage, SwordTargetingFlags.Striking, 0, SwordElement.Neutral, new(128, new byte[5])));
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new SwordDefinition(new("sample:item/sword"),
            SwordWeaponType.Sword, 1, 5, (SwordDamageFormula)2, SwordTargetingFlags.Striking, 0,
            SwordElement.Neutral, new(128, new byte[5])));
        Assert.Equal("formula", error.ParamName);
    }

    [Fact]
    public void Byte_boundaries_roundtrip_and_order_is_canonical()
    {
        JsonObject root = Parse();
        JsonObject first = root["swords"]![0]!.AsObject();
        first["power"] = 0; first["evade"] = 255;
        JsonObject second = first.DeepClone().AsObject();
        second["id"] = "sample:item/another"; second["power"] = 255; second["evade"] = 0;
        root["swords"]!.AsArray().Add(second);
        SwordContentPackage loaded = SwordContentPackage.ReadJson(root.ToJsonString());
        string export = loaded.ToJson();
        Assert.Equal(export, SwordContentPackage.ReadJson(export).ToJson());
        Assert.Equal(255, SwordContentPackage.ReadJson(export).Swords[0].Power);
        Assert.Equal(0, SwordContentPackage.ReadJson(export).Swords[1].Power);
        Assert.Equal(255, SwordContentPackage.ReadJson(export).Swords[1].Evade);
    }

    [Fact]
    public void IDs_profile_dependency_and_selected_package_are_validated()
    {
        SwordContentPackage package = SwordContentPackage.ReadJson(Sample);
        Assert.Equal("$.profileId", Assert.Throws<ContentPackageValidationException>(() =>
            package.ValidateAgainst(Metadata(profile: "sample:profile/other"))).FieldPath);
        Assert.Equal("$.requiredMetadataPackageId", Assert.Throws<ContentPackageValidationException>(() =>
            package.ValidateAgainst(Metadata(id: "sample:package/other"))).FieldPath);
        JsonObject duplicate = Parse();
        duplicate["swords"]!.AsArray().Add(duplicate["swords"]![0]!.DeepClone());
        Assert.Equal("$.swords[1].id", Assert.Throws<ContentPackageValidationException>(() =>
            SwordContentPackage.ReadJson(duplicate.ToJsonString())).FieldPath);
        JsonObject foreign = Parse();
        foreign["swords"]![0]!["id"] = "other:item/sword";
        Assert.Equal("$.swords[0].id", Assert.Throws<ContentPackageValidationException>(() =>
            SwordContentPackage.ReadJson(foreign.ToJsonString())).FieldPath);
    }

    [Fact]
    public void Formats_versions_duplicate_keys_and_numeric_types_cannot_be_silently_reinterpreted()
    {
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(Sample));
        Assert.Throws<ContentPackageValidationException>(() => SwordContentPackage.ReadJson(Metadata().ToJson()));
        Assert.Throws<ContentPackageValidationException>(() => SwordContentPackage.ReadJson("null"));
        Assert.Throws<ContentPackageValidationException>(() => SwordContentPackage.ReadJson(Sample.Replace("\"power\":5", "\"power\":5,\"power\":6", StringComparison.Ordinal)));
        foreach (JsonNode? value in new JsonNode?[] { JsonValue.Create(1.5), JsonValue.Create("5"), null })
        {
            JsonObject root = Parse(); root["swords"]![0]!["power"] = value;
            Assert.Equal("$.swords[0].power", Assert.Throws<ContentPackageValidationException>(() =>
                SwordContentPackage.ReadJson(root.ToJsonString())).FieldPath);
        }
        JsonObject version = Parse(); version["schemaVersion"] = 2;
        Assert.Equal("$.schemaVersion", Assert.Throws<ContentPackageValidationException>(() =>
            SwordContentPackage.ReadJson(version.ToJsonString())).FieldPath);
        Assert.Throws<ContentPackageValidationException>(() => SwordContentPackage.ReadJson(new string('x', ContentPackage.MaxJsonBytes + 1)));
    }

    [Fact]
    public void Files_roundtrip_with_stable_utf8_and_invalid_utf8_fails()
    {
        string path = Path.Combine(Path.GetTempPath(), "fft-sword-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            SwordContentPackage package = SwordContentPackage.ReadJson(Sample);
            package.Save(path);
            byte[] first = File.ReadAllBytes(path);
            package.Save(path);
            Assert.Equal(first, File.ReadAllBytes(path));
            Assert.Equal(package.ToJson(), SwordContentPackage.Load(path).ToJson());
            File.WriteAllBytes(path, new byte[] { 0xff });
            Assert.Throws<ContentPackageValidationException>(() => SwordContentPackage.Load(path));
        }
        finally { File.Delete(path); }
    }

    private static JsonObject Parse() => JsonNode.Parse(Sample)!.AsObject();
    private static ContentPackage Metadata(string id = "sample:package/metadata", string profile = "sample:profile/attack") =>
        new(id, profile, ContentPackageKind.Baseline, null, [], [], []);
}
