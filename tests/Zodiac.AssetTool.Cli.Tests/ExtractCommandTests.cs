using Zodiac.AssetTool.Cli;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class ExtractCommandTests
{
    [Fact]
    public void Run_requires_image_output_and_at_least_one_path()
    {
        using var workspace = new TemporaryDirectory();
        var command = new ExtractCommand(workspace.Path);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = command.Run(["extract", "--bin", "game.bin", "--cue", "game.cue"], output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("either one or more paths or --all-data are required", error.ToString());
    }

    [Fact]
    public void Run_rejects_duplicate_selected_paths_before_reading_image()
    {
        using var workspace = new TemporaryDirectory();
        var command = new ExtractCommand(workspace.Path);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = command.Run(
            ["extract", "--bin", "game.bin", "--cue", "game.cue", "--output", "scratch/out", "--path", "MENU/A", "--path", "MENU/A"],
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Contains("supplied more than once", error.ToString());
    }

    [Fact]
    public void Run_rejects_combining_all_data_with_selected_paths()
    {
        using var workspace = new TemporaryDirectory();
        var command = new ExtractCommand(workspace.Path);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = command.Run(
            ["extract", "--bin", "game.bin", "--cue", "game.cue", "--output", "scratch/out", "--all-data", "--path", "MENU/A"],
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Use either --all-data or --path options", error.ToString());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
