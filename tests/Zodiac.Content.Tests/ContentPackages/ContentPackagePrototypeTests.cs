using System.Text.Json.Nodes;
using Zodiac.Content.Definitions;
using Xunit;

namespace Zodiac.Content.Tests.Packages;

public sealed class ContentPackagePrototypeTests
{
    // Entirely invented metadata. "baseline" describes composition role, not verified original content.
    private const string Synthetic = """
    {
      "format": "fft.content-metadata",
      "schemaVersion": 1,
      "packageId": "sample.base:package/recruits",
      "profileId": "sample:profile/metadata",
      "kind": "baseline",
      "requiredBaselinePackageId": null,
      "jobs": [{
        "id": "sample.base:job/recruit",
        "primarySkillsetId": "sample.base:skillset/practice",
        "baseMove": 3, "baseJump": 2, "steppingStone": true,
        "innateAbilityIds": ["sample.base:ability/focus", "sample.base:ability/focus"]
      }],
      "skillsets": [{
        "id": "sample.base:skillset/practice",
        "slots": [{"index": 9, "abilityId": "sample.base:ability/focus"}, {"index": 0, "abilityId": null}]
      }],
      "abilities": [{
        "id": "sample.base:ability/focus", "jpCost": 65535, "learnOnHitRateRaw": 255,
        "serializedClassRaw": 15,
        "flags": ["show-name", "learn-on-hit", "cannot-learn-with-jp"],
        "unknownTypeBitsRaw": 16, "aiFlagsRaw": [255, 128, 1, 66]
      }]
    }
    """;

    [Fact]
    public void Synthetic_metadata_package_round_trips_through_typed_definitions()
    {
        ContentPackagePrototype original = ContentPackagePrototype.Read(Synthetic);
        string output = original.Write();
        ContentPackagePrototype roundTripped = ContentPackagePrototype.Read(output);
        DefinitionCatalog catalog = ContentPackagePrototype.Compose(roundTripped);
        JobDefinition job = Assert.Single(catalog.Jobs);
        AbilityDefinition ability = catalog.GetAbility(job.InnateAbilityIds[0]);
        Assert.Equal(3, job.BaseMove);
        Assert.Equal(2, job.BaseJump);
        Assert.True(job.SteppingStone);
        Assert.Equal(2, job.InnateAbilityIds.Length);
        Assert.Equal(new[] { 9, 0 }, catalog.GetSkillset(job.PrimarySkillsetId).Slots.Select(slot => slot.Slot));
        Assert.Null(catalog.GetSkillset(job.PrimarySkillsetId).Slots[1].AbilityId);
        Assert.Equal(255, ability.TypeFlagsRaw);
        Assert.Equal(65535, ability.JpCost);
        Assert.Equal(255, ability.LearnOnHitRateRaw);
        Assert.False(ability.CanLearnWithJp);
        Assert.Equal(new byte[] { 255, 128, 1, 66 }, ability.AiFlagsRaw);
        Assert.Equal(output, roundTripped.Write());
        Assert.EndsWith("\n", output);
        Assert.DoesNotContain("\r", output);
    }

    [Fact]
    public void Canonical_output_sorts_definition_ids_and_named_flags_but_keeps_slot_and_innate_order()
    {
        JsonObject first = Parse();
        JsonObject secondJob = (JsonObject)first["jobs"]![0]!.DeepClone();
        secondJob["id"] = "sample.base:job/another";
        first["jobs"]!.AsArray().Add(secondJob);
        JsonObject second = (JsonObject)first.DeepClone();
        JsonNode moved = second["jobs"]![0]!;
        second["jobs"]!.AsArray().RemoveAt(0);
        second["jobs"]!.AsArray().Add(moved);
        second["abilities"]![0]!["flags"] = new JsonArray("cannot-learn-with-jp", "learn-on-hit", "show-name");
        string a = ContentPackagePrototype.Read(first.ToJsonString()).Write();
        string b = ContentPackagePrototype.Read(second.ToJsonString()).Write();
        Assert.Equal(a, b);
        Assert.True(a.IndexOf("sample.base:job/another", StringComparison.Ordinal) < a.IndexOf("sample.base:job/recruit", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_raw_type_byte_survives_symbolic_projection_and_round_trip()
    {
        for (int raw = 0; raw <= 255; raw++)
        {
            JsonObject root = Parse();
            JsonNode ability = root["abilities"]![0]!;
            ability["serializedClassRaw"] = raw & 15;
            ability["unknownTypeBitsRaw"] = raw & 16;
            var flags = new JsonArray();
            if ((raw & 0x20) != 0) flags.Add("learn-on-hit");
            if ((raw & 0x40) != 0) flags.Add("show-name");
            if ((raw & 0x80) != 0) flags.Add("cannot-learn-with-jp");
            ability["flags"] = flags;
            ContentPackagePrototype loaded = ContentPackagePrototype.Read(root.ToJsonString());
            Assert.Equal((byte)raw, ContentPackagePrototype.Read(loaded.Write()).Abilities[0].TypeFlagsRaw);
        }
    }

    [Theory]
    [InlineData("{", "Invalid content package")]
    [InlineData("[]", "must be an object")]
    [InlineData("null", "must be an object")]
    public void Malformed_or_wrong_root_data_is_rejected(string json, string expected)
    {
        Assert.Contains(expected, Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Read(json)).Message);
    }

    [Theory]
    [InlineData("schemaVersion", "2", "Unsupported")]
    [InlineData("schemaVersion", "1.5", "integer")]
    [InlineData("schemaVersion", "\"1\"", "integer")]
    [InlineData("format", "\"other\"", "Unsupported")]
    [InlineData("kind", "\"override\"", "Unknown package kind")]
    [InlineData("jobs", "null", "must be an array")]
    [InlineData("packageId", "\"Invalid\"", "Invalid content package")]
    public void Invalid_top_level_values_are_rejected(string field, string jsonValue, string expected)
    {
        AssertInvalid(root => root[field] = JsonNode.Parse(jsonValue), expected);
    }

    [Fact]
    public void Missing_unknown_duplicate_and_case_changed_fields_are_rejected()
    {
        AssertInvalid(root => root.Remove("jobs"), "Missing field '$.jobs'");
        AssertInvalid(root => root["unexpected"] = true, "Unknown field '$.unexpected'");
        AssertInvalid(root => root["abilities"]![0]!["unexpected"] = 1, "Unknown field 'ability.unexpected'");
        AssertInvalid(root => root["skillsets"]![0]!["slots"]![0]!["Index"] = 4, "Unknown field 'slot.Index'");
        string duplicate = Synthetic.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"schemaVersion\": 1,", StringComparison.Ordinal);
        Assert.Contains("Duplicate field '$.schemaVersion'", Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Read(duplicate)).Message);
    }

    [Fact]
    public void Unknown_and_duplicate_named_flags_are_errors_not_opaque_bits()
    {
        AssertInvalid(root => root["abilities"]![0]!["flags"] = new JsonArray("future-effect"), "Unknown named flag 'future-effect'");
        AssertInvalid(root => root["abilities"]![0]!["flags"] = new JsonArray("show-name", "show-name"), "Duplicate named flag 'show-name'");
        AssertInvalid(root => root["abilities"]![0]!["unknownTypeBitsRaw"] = 32, "unknownTypeBitsRaw must be 0 or 16");
    }

    [Fact]
    public void Invalid_byte_widths_and_structural_invariants_are_rejected()
    {
        AssertInvalid(root => root["abilities"]![0]!["aiFlagsRaw"]![0] = 256, "byte must be 0..255");
        AssertInvalid(root => root["abilities"]![0]!["serializedClassRaw"] = 16, "serializedClassRaw must be 0..15");
        AssertInvalid(root => root["jobs"]![0]!["baseJump"] = 128, "baseJump");
        AssertInvalid(root => root["jobs"]![0]!["baseMove"] = "3", "integer");
        AssertInvalid(root => root["jobs"]![0]!["steppingStone"] = 1, "boolean");
        AssertInvalid(root => root["abilities"]![0]!["jpCost"] = -1, "jpCost");
        AssertInvalid(root => root["skillsets"]![0]!["slots"]![1]!["index"] = 9, "duplicate slot 9");
    }

    [Fact]
    public void Duplicate_ids_and_unresolved_references_are_rejected_before_publishing_baseline()
    {
        foreach (string collection in new[] { "jobs", "skillsets", "abilities" })
            AssertInvalid(root => root[collection]!.AsArray().Add(root[collection]![0]!.DeepClone()), "Duplicate");
        AssertInvalid(root => root["jobs"]![0]!["primarySkillsetId"] = "sample.base:skillset/missing", "unresolved primary skillset");
        AssertInvalid(root => root["jobs"]![0]!["innateAbilityIds"]![0] = "sample.base:ability/missing", "innate ability 0");
        AssertInvalid(root => root["skillsets"]![0]!["slots"]![0]!["abilityId"] = "sample.base:ability/missing", "slot 9 references unresolved ability");
    }

    [Fact]
    public void Addon_is_explicit_and_can_reference_baseline_without_mutating_it()
    {
        ContentPackagePrototype baseline = ContentPackagePrototype.Read(Synthetic);
        string before = baseline.Write();
        ContentPackagePrototype addon = ContentPackagePrototype.Read(Addon().ToJsonString());
        DefinitionCatalog baseOnly = ContentPackagePrototype.Compose(baseline);
        DefinitionCatalog composed = ContentPackagePrototype.Compose(baseline, addon);
        Assert.Single(baseOnly.Jobs);
        Assert.Equal(2, composed.Jobs.Length);
        Assert.Single(composed.Abilities);
        Assert.Equal(new SkillsetId("sample.base:skillset/practice"),
            composed.GetJob(new JobId("sample.extra:job/trainee")).PrimarySkillsetId);
        Assert.Equal(before, baseline.Write());
    }

    [Fact]
    public void Addon_cannot_override_baseline_or_ignore_profile_and_dependency()
    {
        ContentPackagePrototype baseline = ContentPackagePrototype.Read(Synthetic);
        JsonObject addon = Addon();
        addon["jobs"]![0]!["id"] = "sample.base:job/recruit";
        Assert.Contains("outside its namespace", Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Read(addon.ToJsonString())).Message);
        foreach (string field in new[] { "profileId", "requiredBaselinePackageId" })
        {
            JsonObject incompatible = Addon();
            incompatible[field] = "other:package/wrong";
            Assert.Contains("incompatible", Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Compose(
                baseline, ContentPackagePrototype.Read(incompatible.ToJsonString()))).Message);
        }
        ContentPackagePrototype valid = ContentPackagePrototype.Read(Addon().ToJsonString());
        Assert.Contains("Duplicate package ID", Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Compose(baseline, valid, valid)).Message);
    }

    [Fact]
    public void Unresolved_addon_reference_fails_composition_and_colliding_addons_do_not_last_write_win()
    {
        ContentPackagePrototype baseline = ContentPackagePrototype.Read(Synthetic);
        JsonObject missing = Addon();
        missing["jobs"]![0]!["primarySkillsetId"] = "sample.base:skillset/missing";
        Assert.Contains("unresolved primary skillset", Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Compose(
            baseline, ContentPackagePrototype.Read(missing.ToJsonString()))).Message);
        JsonObject colliding = Addon();
        colliding["packageId"] = "sample.extra:package/second";
        Assert.Contains("duplicate job ID", Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Compose(
            baseline, ContentPackagePrototype.Read(Addon().ToJsonString()), ContentPackagePrototype.Read(colliding.ToJsonString()))).Message);
    }

    [Fact]
    public void Selected_addon_order_does_not_change_catalog_order()
    {
        ContentPackagePrototype baseline = ContentPackagePrototype.Read(Synthetic);
        ContentPackagePrototype first = ContentPackagePrototype.Read(Addon().ToJsonString());
        JsonObject third = Addon();
        third["packageId"] = "sample.third:package/recruits";
        third["jobs"]![0]!["id"] = "sample.third:job/third";
        ContentPackagePrototype second = ContentPackagePrototype.Read(third.ToJsonString());
        Assert.Equal(ContentPackagePrototype.Compose(baseline, first, second).Jobs.Select(job => job.Id),
            ContentPackagePrototype.Compose(baseline, second, first).Jobs.Select(job => job.Id));
    }

    [Fact]
    public void Kind_and_dependency_roles_are_explicit()
    {
        AssertInvalid(root => root["requiredBaselinePackageId"] = "sample:package/another", "Baseline requires null dependency");
        AssertInvalid(root => root["kind"] = "addon", "addon requires a baseline package ID");
        ContentPackagePrototype addon = ContentPackagePrototype.Read(Addon().ToJsonString());
        Assert.Contains("explicit baseline", Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Compose(addon)).Message);
    }

    private static JsonObject Parse() => JsonNode.Parse(Synthetic)!.AsObject();
    private static void AssertInvalid(Action<JsonObject> change, string expected)
    {
        JsonObject root = Parse();
        change(root);
        Assert.Contains(expected, Assert.Throws<InvalidDataException>(() => ContentPackagePrototype.Read(root.ToJsonString())).Message);
    }

    private static JsonObject Addon()
    {
        JsonObject root = Parse();
        root["kind"] = "addon";
        root["packageId"] = "sample.extra:package/trainees";
        root["requiredBaselinePackageId"] = "sample.base:package/recruits";
        root["jobs"]![0]!["id"] = "sample.extra:job/trainee";
        root["abilities"] = new JsonArray();
        root["skillsets"] = new JsonArray();
        return root;
    }
}
