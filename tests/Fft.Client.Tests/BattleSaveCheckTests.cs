using System.Text.Json;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BattleSaveCheckTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "fft-save-check-tests-" + Guid.NewGuid().ToString("N"));
    public BattleSaveCheckTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void Finite_headless_check_writes_thirteen_loadable_checkpoints_and_success_receipt()
    {
        string output = Path.Combine(directory, "check");
        Assert.Equal(0, BattleSaveCheck.Run(output));
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        for (int index = 0; index <= 12; index++)
            Assert.False(TwoUnitBattleSave.Load(profile, Path.Combine(output, $"checkpoint-{index:D2}.json")).WasMigrated);
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "check.json")));
        var root = report.RootElement;
        Assert.Equal(13, root.GetProperty("checkpoints").GetInt32());
        Assert.True(root.GetProperty("continuationStateAndEventsMatched").GetBoolean());
        Assert.Equal("FirstUnitWon", root.GetProperty("outcome").GetString());
        Assert.Equal(2473755763u, root.GetProperty("rngWord").GetUInt32());
        Assert.Equal(22ul, root.GetProperty("rngDrawCount").GetUInt64());
        Assert.Equal(14, Directory.GetFiles(output).Length);
    }

    [Fact]
    public void Existing_output_file_or_directory_is_rejected_without_changes()
    {
        string file = Path.Combine(directory, "keep.txt"); File.WriteAllText(file, "keep");
        Assert.Equal(1, BattleSaveCheck.Run(file));
        Assert.Equal(1, BattleSaveCheck.Run(directory));
        Assert.Equal("keep", File.ReadAllText(file));
        Assert.Equal([file], Directory.GetFiles(directory));
    }

    public void Dispose()
    {
        string resolved = Path.GetFullPath(directory);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !Path.GetFileName(resolved).StartsWith("fft-save-check-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove an unexpected save-check test directory.");
        Directory.Delete(resolved, recursive: true);
    }
}
