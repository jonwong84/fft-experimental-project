using System.Text.Json.Nodes;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Xunit;

namespace Zodiac.Content.Tests.Packages;

public sealed class ContentPackageTests
{
    // Invented metadata only; baseline is a composition role, not original-game evidence.
    private const string Synthetic = """
    {
      "format": "fft.content-metadata", "schemaVersion": 1,
      "packageId": "sample.base:package/recruits", "profileId": "sample:profile/metadata",
      "kind": "baseline", "requiredBaselinePackageId": null,
      "jobs": [{"id": "sample.base:job/recruit", "primarySkillsetId": "sample.base:skillset/practice",
        "baseMove": 3, "baseJump": 2, "steppingStone": true,
        "innateAbilityIds": ["sample.base:ability/focus", "sample.base:ability/focus"]}],
      "skillsets": [{"id": "sample.base:skillset/practice",
        "slots": [{"index": 9, "abilityId": "sample.base:ability/focus"}, {"index": 0, "abilityId": null}]}],
      "abilities": [{"id": "sample.base:ability/focus", "jpCost": 65535, "learnOnHitRateRaw": 255,
        "serializedClassRaw": 15, "flags": ["show-name", "learn-on-hit", "cannot-learn-with-jp"],
        "unknownTypeBitsRaw": 16, "aiFlagsRaw": [255, 128, 1, 66]}]
    }
    """;

    [Fact]
    public void Csharp_and_file_authoring_produce_equivalent_runtime_definitions_and_identical_exports()
    {
        ContentPackage authored = Authored();
        ContentPackage edited = ContentPackage.ReadJson(Synthetic);
        Assert.Equal(authored.ToJson(), edited.ToJson());
        string directory = TempDirectory();
        try
        {
            string path = Path.Combine(directory, "metadata.json");
            authored.Save(path);
            byte[] first = File.ReadAllBytes(path);
            authored.Save(path);
            Assert.Equal(first, File.ReadAllBytes(path));
            ContentPackage runtime = ContentPackage.Load(path);
            Assert.Equal(authored.ToJson(), runtime.ToJson());
            DefinitionCatalog catalog = ContentPackage.Compose(runtime);
            JobDefinition job = Assert.Single(catalog.Jobs);
            Assert.Equal(3, job.JobStats.BaseMove);
            Assert.Equal(2, job.JobStats.BaseJump);
            Assert.True(job.SteppingStone);
            Assert.Equal(2, job.InnateAbilityIds.Length);
            Assert.Equal(new[] { 9, 0 }, Assert.Single(catalog.Skillsets).Slots.Select(slot => slot.Slot));
            Assert.Null(catalog.Skillsets[0].Slots[1].AbilityId);
            AbilityMetadataDefinition ability = Assert.Single(catalog.Abilities);
            Assert.Equal(65535, ability.JpCost);
            Assert.Equal(15, ability.SerializedClassRaw);
            Assert.True(ability.LearnOnHit);
            Assert.True(ability.ShowName);
            Assert.False(ability.CanLearnWithJp);
            Assert.True(ability.UnknownTypeBit4);
            Assert.Equal(255, ability.LearnOnHitRateRaw);
            Assert.Equal(new byte[] { 255, 128, 1, 66 }, ability.AiFlagsRaw);
            Assert.EndsWith("\n", runtime.ToJson());
            Assert.DoesNotContain("\r", runtime.ToJson());
        }
        finally { DeleteTempDirectory(directory); }
    }

    [Fact]
    public void Canonical_exports_sort_definitions_and_flags_but_preserve_semantic_sequence_order()
    {
        JsonObject first = Parse();
        JsonNode extra = first["jobs"]![0]!.DeepClone();
        extra["id"] = "sample.base:job/another";
        first["jobs"]!.AsArray().Add(extra);
        JsonObject second = first.DeepClone().AsObject();
        JsonNode moved = second["jobs"]![0]!;
        second["jobs"]!.AsArray().RemoveAt(0);
        second["jobs"]!.AsArray().Add(moved);
        second["abilities"]![0]!["flags"] = new JsonArray("cannot-learn-with-jp", "learn-on-hit", "show-name");
        string output = ContentPackage.ReadJson(first.ToJsonString()).ToJson();
        Assert.Equal(output, ContentPackage.ReadJson(second.ToJsonString()).ToJson());
        Assert.Equal(output, ContentPackage.ReadJson(output).ToJson());
        Assert.True(output.IndexOf("sample.base:job/another", StringComparison.Ordinal) <
            output.IndexOf("sample.base:job/recruit", StringComparison.Ordinal));
        Assert.Equal(new[] { 9, 0 }, ContentPackage.ReadJson(output).Skillsets[0].Slots.Select(slot => slot.Slot));
    }

    [Theory]
    [InlineData(1, false, false, true, false)]
    [InlineData(1, true, false, true, false)]
    [InlineData(1, false, true, true, false)]
    [InlineData(15, true, true, false, true)]
    [InlineData(0, false, false, false, true)]
    public void Authored_boolean_flags_and_opaque_class_survive_export_and_load(
        int serializedClass, bool learnOnHit, bool showName, bool canLearn, bool unknown)
    {
        ContentPackage source = Authored();
        var ability = new AbilityMetadataDefinition(source.Abilities[0].Id, 7, 9, serializedClass, [1, 2, 3, 4],
            learnOnHit: learnOnHit, showName: showName, canLearnWithJp: canLearn, unknownTypeBit4: unknown);
        var package = new ContentPackage(source.PackageId, source.ProfileId, source.Kind, null,
            source.Jobs, source.Skillsets, [ability]);
        var loaded = Assert.Single(ContentPackage.ReadJson(package.ToJson()).Abilities);
        Assert.Equal((serializedClass, learnOnHit, showName, canLearn, unknown),
            (loaded.SerializedClassRaw, loaded.LearnOnHit, loaded.ShowName, loaded.CanLearnWithJp, loaded.UnknownTypeBit4));
        Assert.Equal(package.ToJson(), ContentPackage.ReadJson(package.ToJson()).ToJson());
    }

    [Fact]
    public void Package_snapshots_caller_collections()
    {
        ContentPackage source = Authored();
        var jobs = source.Jobs.ToList();
        var skills = source.Skillsets.ToList();
        var abilities = source.Abilities.ToList();
        var package = new ContentPackage(source.PackageId, source.ProfileId, source.Kind, null, jobs, skills, abilities);
        string original = package.ToJson();
        jobs.Clear(); skills.Clear(); abilities.Clear();
        Assert.Equal(original, package.ToJson());
        Assert.Single(ContentPackage.Compose(package).Jobs);
    }

    [Theory]
    [InlineData("job-ref", "sample.base:job/recruit", "$.jobs[0].primarySkillsetId")]
    [InlineData("innate-ref", "sample.base:job/recruit", "$.jobs[0].innateAbilityIds[0]")]
    [InlineData("slot-ref", "sample.base:skillset/practice", "$.skillsets[0].slots[0].abilityId")]
    [InlineData("unknown-flag", "sample.base:ability/focus", "$.abilities[0].flags[0]")]
    [InlineData("duplicate-flag", "sample.base:ability/focus", "$.abilities[0].flags[1]")]
    [InlineData("unknown-field", "sample.base:ability/focus", "$.abilities[0].unexpected")]
    [InlineData("nested-field", "sample.base:skillset/practice", "$.skillsets[0].slots[0].unexpected")]
    [InlineData("byte-width", "sample.base:ability/focus", "$.abilities[0].aiFlagsRaw[0]")]
    public void Diagnostics_identify_package_record_and_precise_nested_field(string invalid, string record, string path)
    {
        JsonObject root = Parse();
        switch (invalid)
        {
            case "job-ref": root["jobs"]![0]!["primarySkillsetId"] = "sample.base:skillset/missing"; break;
            case "innate-ref": root["jobs"]![0]!["innateAbilityIds"]![0] = "sample.base:ability/missing"; break;
            case "slot-ref": root["skillsets"]![0]!["slots"]![0]!["abilityId"] = "sample.base:ability/missing"; break;
            case "unknown-flag": root["abilities"]![0]!["flags"] = new JsonArray("future-effect"); break;
            case "duplicate-flag": root["abilities"]![0]!["flags"] = new JsonArray("show-name", "show-name"); break;
            case "unknown-field": root["abilities"]![0]!["unexpected"] = true; break;
            case "nested-field": root["skillsets"]![0]!["slots"]![0]!["unexpected"] = true; break;
            case "byte-width": root["abilities"]![0]!["aiFlagsRaw"]![0] = 256; break;
        }
        var error = Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(root.ToJsonString()));
        Assert.Equal("sample.base:package/recruits", error.PackageId);
        Assert.Equal(record, error.RecordId);
        Assert.Equal(path, error.FieldPath);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Malformed_and_nonobject_json_fail_validation(string json) =>
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(json));

    [Fact]
    public void Utf8_size_budget_is_enforced_for_strings_and_files()
    {
        // Multibyte input exceeds the byte limit while remaining below it in character count.
        string oversized = new('\u00e9', ContentPackage.MaxJsonBytes / 2 + 1);
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(oversized));
        string directory = TempDirectory();
        try
        {
            string path = Path.Combine(directory, "oversized.json");
            File.WriteAllText(path, oversized, new System.Text.UTF8Encoding(false));
            Assert.True(new FileInfo(path).Length > ContentPackage.MaxJsonBytes);
            Assert.Throws<ContentPackageValidationException>(() => ContentPackage.Load(path));
        }
        finally { DeleteTempDirectory(directory); }
    }

    [Fact]
    public void Extended_job_values_round_trip_and_explicit_load_limits_keep_field_diagnostics()
    {
        var root = JsonNode.Parse(Authored().ToJson())!;
        root["jobs"]![0]!["baseMove"] = 512; root["jobs"]![0]!["baseJump"] = 256;
        var package = ContentPackage.ReadJson(root.ToJsonString());
        Assert.Equal(512, package.Jobs[0].JobStats.BaseMove);
        Assert.Equal(256, package.Jobs[0].JobStats.BaseJump);
        string json = package.ToJson();
        Assert.Equal(json, ContentPackage.ReadJson(json, new(512, 256)).ToJson());
        Assert.Equal("$.jobs[0].baseMove", Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(json, new(511, 256))).FieldPath);
        Assert.Equal("$.jobs[0].baseJump", Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(json, new(512, 255))).FieldPath);
        string directory = TempDirectory();
        try
        {
            string path = Path.Combine(directory, "extended.json"); package.Save(path);
            Assert.Equal(json, ContentPackage.Load(path, new(512, 256)).ToJson());
            Assert.Equal("$.jobs[0].baseJump", Assert.Throws<ContentPackageValidationException>(() => ContentPackage.Load(path, new(512, 255))).FieldPath);
        }
        finally { DeleteTempDirectory(directory); }
    }

    [Fact]
    public void Authored_package_id_budget_has_a_structured_field_diagnostic()
    {
        ContentPackage source = Authored();
        string oversizedId = "sample.base:" + new string('a', ContentPackage.MaxIdLength);
        var error = Assert.Throws<ContentPackageValidationException>(() => new ContentPackage(oversizedId,
            source.ProfileId, source.Kind, null, source.Jobs, source.Skillsets, source.Abilities));
        Assert.Equal("$.packageId", error.FieldPath);
    }

    [Fact]
    public void Unknown_versions_fields_duplicate_keys_and_duplicate_ids_are_rejected()
    {
        Invalid(root => root["schemaVersion"] = 2);
        Invalid(root => root["schemaVersion"] = "1");
        Invalid(root => root["schemaVersion"] = 1.5);
        Invalid(root => root["unexpected"] = true);
        Invalid(root => root.Remove("jobs"));
        Invalid(root => root["jobs"]!.AsArray().Add(root["jobs"]![0]!.DeepClone()));
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(
            Synthetic.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"schemaVersion\": 1,", StringComparison.Ordinal)));
    }

    [Fact]
    public void Failed_authored_validation_and_export_io_leave_existing_package_bytes_unchanged()
    {
        string directory = TempDirectory();
        try
        {
            string path = Path.Combine(directory, "metadata.json");
            Authored().Save(path);
            byte[] original = File.ReadAllBytes(path);
            Assert.Throws<ContentPackageValidationException>(() =>
                new ContentPackage("sample.base:package/recruits", "sample:profile/metadata", ContentPackageKind.Baseline,
                    null, Authored().Jobs, Array.Empty<SkillsetDefinition>(), Authored().Abilities).Save(path));
            Assert.Equal(original, File.ReadAllBytes(path));
            // An existing file cannot serve as an output directory; this fails on Windows and Linux.
            Assert.ThrowsAny<IOException>(() => Authored().Save(Path.Combine(path, "child.json")));
            Assert.Equal(original, File.ReadAllBytes(path));
            if (OperatingSystem.IsWindows())
            {
                // Windows denies replacement without delete sharing; exercise failure after temp-file writing.
                using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                Exception? error = Record.Exception(() => Authored().Save(path));
                Assert.True(error is IOException or UnauthorizedAccessException,
                    $"Expected a file replacement failure, got {error?.GetType().Name ?? "no exception"}.");
                Assert.Equal(original, File.ReadAllBytes(path));
            }
            Assert.Equal(Authored().ToJson(), ContentPackage.Load(path).ToJson());
            Assert.Equal(new[] { path }, Directory.GetFiles(directory));
        }
        finally { DeleteTempDirectory(directory); }
    }

    [Fact]
    public void Explicit_addons_do_not_change_baseline_and_invalid_compositions_fail()
    {
        ContentPackage baseline = Authored();
        string before = baseline.ToJson();
        ContentPackage addon = ContentPackage.ReadJson(Addon().ToJsonString());
        Assert.Single(ContentPackage.Compose(baseline).Jobs);
        Assert.Equal(2, ContentPackage.Compose(baseline, addon).Jobs.Length);
        Assert.Equal(before, baseline.ToJson());
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.Compose(baseline, addon, addon));
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.Compose(addon));
        foreach (string field in new[] { "profileId", "requiredBaselinePackageId" })
        {
            JsonObject wrong = Addon();
            wrong[field] = "other:package/wrong";
            Assert.Throws<ContentPackageValidationException>(() =>
                ContentPackage.Compose(baseline, ContentPackage.ReadJson(wrong.ToJsonString())));
        }
        JsonObject unresolved = Addon();
        unresolved["jobs"]![0]!["primarySkillsetId"] = "sample.base:skillset/missing";
        Assert.Throws<ContentPackageValidationException>(() =>
            ContentPackage.Compose(baseline, ContentPackage.ReadJson(unresolved.ToJsonString())));
        JsonObject overrideAttempt = Addon();
        overrideAttempt["jobs"]![0]!["id"] = "sample.base:job/recruit";
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(overrideAttempt.ToJsonString()));
        Assert.Equal(before, baseline.ToJson());
    }

    [Fact]
    public void Addon_collisions_are_errors_and_selection_order_is_not_precedence()
    {
        ContentPackage baseline = Authored();
        ContentPackage first = ContentPackage.ReadJson(Addon().ToJsonString());
        JsonObject collision = Addon();
        collision["packageId"] = "sample.extra:package/second";
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.Compose(baseline, first,
            ContentPackage.ReadJson(collision.ToJsonString())));
        JsonObject independent = Addon();
        independent["packageId"] = "sample.third:package/recruits";
        independent["jobs"]![0]!["id"] = "sample.third:job/third";
        ContentPackage second = ContentPackage.ReadJson(independent.ToJsonString());
        Assert.Equal(ContentPackage.Compose(baseline, first, second).Jobs.Select(job => job.Id),
            ContentPackage.Compose(baseline, second, first).Jobs.Select(job => job.Id));
    }

    private static ContentPackage Authored() => new("sample.base:package/recruits", "sample:profile/metadata",
        ContentPackageKind.Baseline, null,
        new[] { new JobDefinition(new("sample.base:job/recruit"), new("sample.base:skillset/practice"), new(3, 2), true,
            new AbilityId[] { new("sample.base:ability/focus"), new("sample.base:ability/focus") }) },
        new[] { new SkillsetDefinition(new("sample.base:skillset/practice"),
            new[] { new SkillsetSlot(9, new AbilityId("sample.base:ability/focus")), new SkillsetSlot(0, null) }) },
        new[] { new AbilityMetadataDefinition(new("sample.base:ability/focus"), 65535, 255, 15, new byte[] { 255, 128, 1, 66 },
            learnOnHit: true, showName: true, canLearnWithJp: false, unknownTypeBit4: true) });

    private static JsonObject Parse() => JsonNode.Parse(Synthetic)!.AsObject();
    private static JsonObject Addon()
    {
        JsonObject root = Parse();
        root["kind"] = "addon";
        root["packageId"] = "sample.extra:package/trainees";
        root["requiredBaselinePackageId"] = "sample.base:package/recruits";
        root["jobs"]![0]!["id"] = "sample.extra:job/trainee";
        root["skillsets"] = new JsonArray();
        root["abilities"] = new JsonArray();
        return root;
    }
    private static void Invalid(Action<JsonObject> change)
    {
        JsonObject root = Parse();
        change(root);
        Assert.Throws<ContentPackageValidationException>(() => ContentPackage.ReadJson(root.ToJsonString()));
    }
    private static string TempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "fft-content-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static void DeleteTempDirectory(string path)
    {
        string resolved = Path.GetFullPath(path);
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(tempRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || !Path.GetFileName(resolved).StartsWith("fft-content-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to remove unexpected test directory '{resolved}'.");
        Directory.Delete(resolved, true);
    }
}
