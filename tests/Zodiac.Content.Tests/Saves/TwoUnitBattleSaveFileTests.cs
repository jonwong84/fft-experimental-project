using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Xunit;

namespace Zodiac.Content.Tests.Saves;

public sealed class TwoUnitBattleSaveFileTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "fft-save-tests-" + Guid.NewGuid().ToString("N"));
    public TwoUnitBattleSaveFileTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void Saving_over_existing_file_loads_new_checkpoint_and_leaves_no_sibling_temporary_file()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        string path = Path.Combine(directory, "battle.json");
        TwoUnitBattleSave.Save(profile, profile.Definition.Start(), path);
        byte[] original = File.ReadAllBytes(path);
        var active = TwoUnitBattleSaveTests.At(profile, 5);
        TwoUnitBattleSave.Save(profile, active, path);
        Assert.NotEqual(original, File.ReadAllBytes(path));
        Assert.Equal(TwoUnitBattleSaveTests.Snapshot(active), TwoUnitBattleSaveTests.Snapshot(TwoUnitBattleSave.Load(profile, path).State));
        byte[] saved = File.ReadAllBytes(path);
        TwoUnitBattleSave.Save(profile, active, path);
        Assert.Equal(saved, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(directory));
    }

    [Fact]
    public void Failed_validation_preserves_last_valid_save_bytes()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        string path = Path.Combine(directory, "battle.json");
        TwoUnitBattleSave.Save(profile, profile.Definition.Start(), path);
        byte[] original = File.ReadAllBytes(path);
        var foreign = SyntheticTwoUnitBattle.CreateSaveProfile();
        Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.Save(profile, foreign.Definition.Start(), path));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(directory));
    }

    [Fact]
    public void Failed_Windows_replace_preserves_old_file_and_cleans_flushed_sibling()
    {
        if (!OperatingSystem.IsWindows()) return; // Unix rename semantics do not honor Windows delete-sharing locks.
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        string path = Path.Combine(directory, "battle.json");
        TwoUnitBattleSave.Save(profile, profile.Definition.Start(), path);
        byte[] original = File.ReadAllBytes(path);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            AssertFileFailure(() => TwoUnitBattleSave.Save(profile, TwoUnitBattleSaveTests.At(profile, 5), path));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(directory));
        Assert.Equal(TwoUnitBattleSaveTests.Snapshot(profile.Definition.Start()), TwoUnitBattleSaveTests.Snapshot(TwoUnitBattleSave.Load(profile, path).State));
    }

    [Fact]
    public void Failed_promotion_to_directory_preserves_its_contents_and_leaves_no_temporary_save()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        string target = Path.Combine(directory, "target");
        Directory.CreateDirectory(target);
        string marker = Path.Combine(target, "keep.txt"); File.WriteAllText(marker, "existing");
        AssertFileFailure(() => TwoUnitBattleSave.Save(profile, profile.Definition.Start(), target));
        Assert.Equal("existing", File.ReadAllText(marker));
        Assert.Empty(Directory.GetFiles(directory));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("truncated")]
    [InlineData("budget")]
    public void Bounded_file_reader_rejects_invalid_bytes(string problem)
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        string path = Path.Combine(directory, "bad.json");
        File.WriteAllBytes(path, problem switch
        {
            "utf8" => [0xff, 0xfe, 0x00],
            "budget" => new byte[TwoUnitBattleSave.MaxJsonBytes + 1],
            _ => "{\"schemaVersion\":2"u8.ToArray(),
        });
        Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.Load(profile, path));
    }

    [Fact]
    public void Loading_legacy_file_does_not_rewrite_it_and_explicit_save_upgrades_a_separate_destination()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        string source = Path.Combine(directory, "v1.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves", "two-unit-active-v1.json"), source);
        byte[] original = File.ReadAllBytes(source);
        var loaded = TwoUnitBattleSave.Load(profile, source);
        Assert.True(loaded.WasMigrated);
        Assert.Equal(original, File.ReadAllBytes(source));
        string upgraded = Path.Combine(directory, "v2.json");
        TwoUnitBattleSave.Save(profile, loaded.State, upgraded);
        var current = TwoUnitBattleSave.Load(profile, upgraded);
        Assert.False(current.WasMigrated);
        Assert.Equal(TwoUnitBattleSaveTests.Snapshot(loaded.State), TwoUnitBattleSaveTests.Snapshot(current.State));
        Assert.Equal(original, File.ReadAllBytes(source));
    }

    private static void AssertFileFailure(Action action)
    {
        var error = Record.Exception(action);
        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString() ?? "Expected a file access failure.");
    }

    public void Dispose()
    {
        string resolved = Path.GetFullPath(directory);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !Path.GetFileName(resolved).StartsWith("fft-save-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove an unexpected save-test directory.");
        Directory.Delete(resolved, recursive: true);
    }
}
