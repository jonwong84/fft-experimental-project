using System.Text.Json.Nodes;
using Zodiac.Content.Authoring;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class JobEditorDocumentTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "fft-editor-" + Guid.NewGuid().ToString("N"));
    private static readonly JobId Job = new("sample:job/recruit");
    private static readonly AbilityId A = new("sample:ability/focus");
    private static readonly AbilityId B = new("sample:ability/innate");
    private const string Addon = "authored:package/recruit";
    public JobEditorDocumentTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    [Fact]
    public void Copy_remaps_complete_graph_preserving_flags_opaque_bytes_empty_repeated_slots_and_innates()
    {
        var source = Source(); var document = new JobEditorDocument(source);
        document.CreateCopy(Job, Addon);
        ContentPackage copy = document.Validate();
        Assert.Equal(ContentPackageKind.Addon, copy.Kind);
        Assert.Equal(source.ProfileId, copy.ProfileId); Assert.Equal(source.PackageId, copy.RequiredBaselinePackageId);
        Assert.Equal("authored:job/recruit", Assert.Single(copy.Jobs).Id.Value);
        Assert.Equal("authored:skillset/practice", copy.Jobs[0].PrimarySkillsetId.Value);
        Assert.Equal(new[] { "authored:ability/innate", "authored:ability/focus", "authored:ability/innate" }, copy.Jobs[0].InnateAbilityIds.Select(id => id.Value));
        Assert.Equal(new int[] { 3, 1, 9, 8 }, copy.Skillsets[0].Slots.Select(slot => slot.Slot));
        Assert.Equal(new string?[] { "authored:ability/focus", null, "authored:ability/focus", "authored:ability/innate" }, copy.Skillsets[0].Slots.Select(slot => slot.AbilityId?.Value));
        Assert.Equal(2, copy.Abilities.Length); // An unrelated source ability is not copied.
        AbilityDefinition focus = copy.Abilities.Single(ability => ability.Id.Value == "authored:ability/focus");
        Assert.Equal(0xf7, focus.TypeFlagsRaw); Assert.Equal(177, focus.LearnOnHitRateRaw);
        Assert.Equal(new byte[] { 0, 17, 128, 255 }, focus.AiFlagsRaw);
        Assert.True(document.IsDirty); Assert.Null(document.ExportPath);
        Assert.Equal("sample:job/recruit", source.Jobs[0].Id.Value);
    }

    [Fact]
    public void Search_is_case_insensitive_ordered_and_does_not_touch_a_draft()
    {
        var document = new JobEditorDocument(Source()); document.CreateCopy(Job, Addon);
        Assert.Single(document.Search("  RECRUIT ")); Assert.Empty(document.Search("none"));
        Assert.Single(document.Search("")); Assert.True(document.IsDirty);
        Assert.Equal("authored:job/recruit", document.JobId);
    }

    [Theory]
    [InlineData("invalid", "2", ".baseMove")]
    [InlineData("-1", "2", ".baseMove")]
    [InlineData("256", "2", ".baseMove")]
    [InlineData("1.5", "2", ".baseMove")]
    [InlineData("1e2", "2", ".baseMove")]
    [InlineData("", "2", ".baseMove")]
    [InlineData("2147483648", "2", ".baseMove")]
    [InlineData("2", "-1", ".baseJump")]
    [InlineData("2", "128", ".baseJump")]
    [InlineData("2", "no", ".baseJump")]
    public void Invalid_numeric_draft_is_preserved_with_shared_field_diagnostic(string move, string jump, string field)
    {
        var document = NewDraft(); document.EditMovement(move, jump);
        ContentPackageValidationException error = Assert.Throws<ContentPackageValidationException>(() => document.Validate());
        Assert.EndsWith(field, error.FieldPath); Assert.Equal("authored:job/recruit", error.RecordId);
        Assert.Equal(move, document.MoveText); Assert.Equal(jump, document.JumpText);
        Assert.True(document.IsDirty); Assert.Equal(1, document.Source.Jobs[0].BaseMove);
    }

    [Theory]
    [InlineData("oops")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("2.5")]
    public void Jp_failure_uses_shared_ability_identity_and_path(string text)
    {
        var document = NewDraft(); document.EditJp("authored:ability/focus", text);
        var error = Assert.Throws<ContentPackageValidationException>(() => document.Validate());
        Assert.EndsWith(".jpCost", error.FieldPath); Assert.Equal("authored:ability/focus", error.RecordId);
        Assert.Equal(text, document.Abilities.Single(value => value.Id == error.RecordId).JpCost);
    }

    [Theory]
    [InlineData("0", "0", "0")]
    [InlineData("255", "127", "65535")]
    public void Supported_metadata_boundaries_round_trip(string move, string jump, string jp)
    {
        var document = NewDraft(); document.EditMovement(move, jump); document.EditJp("authored:ability/focus", jp);
        string json = document.Validate().ToJson(); Assert.Equal(json, ContentPackage.ReadJson(json).ToJson());
    }

    [Fact]
    public void Edited_export_reopen_and_movement_proof_have_exact_source_and_authored_results()
    {
        var document = NewDraft(); string original = document.Source.ToJson();
        document.EditMovement("3", "7"); document.EditJp("authored:ability/focus", "123");
        EditorMovementPreview first = document.Preview();
        Assert.Equal(4, first.SourceReachable.Length); Assert.Equal(24, first.AuthoredReachable.Length);
        Assert.DoesNotContain(new TilePosition(7, 4, 0), first.SourceReachable);
        Assert.Contains(new TilePosition(7, 4, 0), first.AuthoredReachable);
        Assert.Equal(7, first.AuthoredJump);
        string path = Path.Combine(directory, "addon.json"); document.Export(path);
        Assert.False(document.IsDirty); Assert.Equal(path, document.ExportPath);
        var reopened = new JobEditorDocument(Source()); reopened.OpenCopy(Job, path);
        EditorMovementPreview second = reopened.Preview();
        Assert.False(reopened.IsDirty); Assert.Equal(first.SourceReachable.ToArray(), second.SourceReachable.ToArray());
        Assert.Equal(first.AuthoredReachable.ToArray(), second.AuthoredReachable.ToArray());
        Assert.Equal(original, document.Source.ToJson());
        Assert.Equal(123, reopened.Validate().Abilities.Single(ability => ability.Id.Value == "authored:ability/focus").JpCost);
        Assert.Equal(File.ReadAllText(path), reopened.Validate().ToJson());
        string repeat = Path.Combine(directory, "repeat.json"); reopened.Export(repeat);
        Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(repeat));
    }

    [Fact]
    public void Dirty_marker_tracks_edits_reversion_and_failed_validation_without_overwriting_saved_bytes()
    {
        var document = NewDraft(); string path = Path.Combine(directory, "addon.json"); document.Export(path);
        byte[] before = File.ReadAllBytes(path);
        document.EditMovement("2", "3"); Assert.True(document.IsDirty);
        document.EditMovement("1", "3"); Assert.False(document.IsDirty);
        document.EditMovement("bad", "3");
        Assert.Throws<ContentPackageValidationException>(() => document.Export(path));
        Assert.True(document.IsDirty); Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Busy_export_and_missing_directory_preserve_prior_output_current_draft_and_export_identity()
    {
        var document = NewDraft(); string path = Path.Combine(directory, "addon.json"); document.Export(path);
        byte[] before = File.ReadAllBytes(path); document.EditMovement("2", "3");
        // Atomic replacement of a read-open file is supported on Unix. A missing directory is portable.
        if (OperatingSystem.IsWindows())
        {
            using var busy = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            Exception? failure = Record.Exception(() => document.Export(path));
            Assert.True(failure is IOException or UnauthorizedAccessException);
        }
        Assert.Throws<DirectoryNotFoundException>(() => document.Export(Path.Combine(directory, "missing", "addon.json")));
        Assert.True(document.IsDirty); Assert.Equal(path, document.ExportPath);
        Assert.Equal(before, File.ReadAllBytes(path)); Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public void Source_file_and_directory_are_protected_even_when_source_is_loaded_from_disk()
    {
        string sourceDir = Path.Combine(directory, "source"); Directory.CreateDirectory(sourceDir);
        string source = Path.Combine(sourceDir, "baseline.json"); Source().Save(source);
        byte[] before = File.ReadAllBytes(source);
        var document = JobEditorDocument.OpenSource(source); document.CreateCopy(Job, Addon);
        Assert.Throws<IOException>(() => document.Export(source));
        Assert.Throws<IOException>(() => document.Export(Path.Combine(sourceDir, "new.json")));
        Directory.CreateDirectory(Path.Combine(sourceDir, "child"));
        Assert.Throws<IOException>(() => document.Export(Path.Combine(sourceDir, "child", "new.json")));
        document.Export(Path.Combine(directory, "allowed.json"));
        Assert.Equal(before, File.ReadAllBytes(source)); Assert.Equal(Path.GetFullPath(source), document.SourcePath);
        Assert.Equal(64, document.SourceSha256.Length);
    }

    [Fact]
    public void Source_and_export_reject_reparse_ancestors_without_writing_the_target()
    {
        string target = Path.Combine(directory, "target"), link = Path.Combine(directory, "link");
        Directory.CreateDirectory(target); Source().Save(Path.Combine(target, "source.json"));
        if (OperatingSystem.IsWindows())
        {
            // Directory junction creation requires no symlink privilege. Only test-owned fresh paths are used.
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/d /c mklink /J \"{link}\" \"{target}\"", UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var process = System.Diagnostics.Process.Start(start)!;
            process.WaitForExit(); Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, target);
        try
        {
            Assert.Throws<IOException>(() => JobEditorDocument.OpenSource(Path.Combine(link, "source.json")));
            var document = NewDraft();
            Assert.Throws<IOException>(() => document.Export(Path.Combine(link, "addon.json")));
            Assert.False(File.Exists(Path.Combine(target, "addon.json"))); Assert.True(document.IsDirty);
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void Reopening_unsupported_shape_or_corrupt_file_preserves_the_current_draft()
    {
        var document = NewDraft(); document.EditMovement("2", "3"); string before = document.Validate().ToJson();
        string path = Path.Combine(directory, "other.json");
        JsonObject changed = JsonNode.Parse(before)!.AsObject();
        changed["abilities"]![0]!["learnOnHitRateRaw"] = 99; File.WriteAllText(path, changed.ToJsonString());
        Assert.Throws<ContentPackageValidationException>(() => document.OpenCopy(Job, path));
        File.WriteAllText(path, "{"); Assert.Throws<ContentPackageValidationException>(() => document.OpenCopy(Job, path));
        Assert.Equal(before, document.Validate().ToJson()); Assert.True(document.IsDirty); Assert.Null(document.ExportPath);
    }

    [Theory]
    [InlineData("sample:package/collision")]
    [InlineData("INVALID")]
    public void Invalid_copy_identity_does_not_destroy_existing_draft(string id)
    {
        var document = NewDraft(); string before = document.Validate().ToJson();
        Assert.Throws<ContentPackageValidationException>(() => document.CreateCopy(Job, id));
        Assert.Equal(before, document.Validate().ToJson()); Assert.True(document.IsDirty);
    }

    [Fact]
    public void No_draft_operations_fail_clearly_and_source_requires_a_baseline()
    {
        var document = new JobEditorDocument(Source()); Assert.False(document.HasDraft); Assert.False(document.IsDirty);
        Assert.Throws<InvalidOperationException>(() => document.Validate());
        Assert.Throws<InvalidOperationException>(() => document.EditMovement("1", "2"));
        var draft = NewDraft(); Assert.Throws<ArgumentException>(() => new JobEditorDocument(draft.Validate()));
    }

    [Fact]
    public void Stepping_stone_metadata_exports_but_movement_preview_rejects_without_fabricating_support()
    {
        var document = new JobEditorDocument(Source(stepping: true)); document.CreateCopy(Job, Addon);
        Assert.True(document.Validate().Jobs[0].SteppingStone);
        Assert.Throws<NotSupportedException>(() => document.Preview());
    }

    [Fact]
    public void Empty_skillset_and_no_ability_closure_are_supported()
    {
        var source = new ContentPackage("sample:package/empty", "sample:profile/demo", ContentPackageKind.Baseline, null,
            [new(Job, new("sample:skillset/empty"), 0, 0, false, [])],
            [new(new("sample:skillset/empty"), [new(0, null)])], []);
        var document = new JobEditorDocument(source); document.CreateCopy(Job, Addon);
        Assert.Empty(document.Abilities); Assert.Empty(document.Preview().AuthoredReachable);
    }

    private static JobEditorDocument NewDraft()
    {
        var document = new JobEditorDocument(Source()); document.CreateCopy(Job, Addon); return document;
    }
    private static ContentPackage Source(bool stepping = false) => new("sample:package/recruits", "sample:profile/demo", ContentPackageKind.Baseline, null,
        [new(Job, new("sample:skillset/practice"), 1, 3, stepping, [B, A, B])],
        [new(new("sample:skillset/practice"), [new(3, A), new(1, null), new(9, A), new(8, B)])],
        [new(A, 50, 177, 0xf7, [0, 17, 128, 255]), new(B, 0, 0, 0x10, [255, 0, 0, 1]),
            new(new("sample:ability/unrelated"), 1, 0, 0, [0, 0, 0, 0])]);
}
