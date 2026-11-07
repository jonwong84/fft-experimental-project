using Zodiac.AssetTool.Cli;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class ImportMetadataCommandTests
{
    [Theory]
    [InlineData("import-metadata")]
    [InlineData("import-metadata", "--exe")]
    [InlineData("import-metadata", "--unknown", "anything")]
    [InlineData("import-metadata", "--exe", "one", "--output", "out", "--metadata", "selected")]
    [InlineData("import-sword", "--exe", "one", "--output", "out", "--metadata")]
    [InlineData("import-sword", "--exe", "one", "--output", "out", "--metadata", "a", "--metadata", "b")]
    [InlineData("import-metadata", "--exe", "one", "--exe", "two", "--output", "out")]
    public void Invalid_arguments_report_usage(params string[] args)
    {
        var (code, output, error) = Run(Path.GetTempPath(), args);
        Assert.Equal(2, code);
        Assert.Empty(output);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public void Original_content_cannot_be_read_or_written_outside_scratch()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        foreach ((string input, string destination) in new[]
        {
            (Path.Combine(root, "outside.exe"), Path.Combine(root, "scratch", "output")),
            (Path.Combine(root, "scratch", "source.exe"), Path.Combine(root, "scratch", "..", "outside")),
        })
        {
            var (code, output, error) = Run(root, ["import-metadata", "--exe", input, "--output", destination]);
            Assert.Equal(1, code);
            Assert.Empty(output);
            Assert.Contains("scratch", error);
            Assert.False(Directory.Exists(root));
        }
    }

    [Fact]
    public void Selected_sword_metadata_dependency_must_also_stay_in_scratch()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var (code, output, error) = Run(root, ["import-sword", "--exe", Path.Combine(root, "scratch", "source.exe"),
            "--output", Path.Combine(root, "scratch", "output"), "--metadata", Path.Combine(root, "outside.json")]);
        Assert.Equal(1, code);
        Assert.Empty(output);
        Assert.Contains("scratch", error);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Missing_wrong_size_and_wrong_hash_inputs_leave_no_output()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(root, "scratch");
        Directory.CreateDirectory(scratch);
        string source = Path.Combine(scratch, "source.exe");
        string destination = Path.Combine(scratch, "output");
        try
        {
            for (int stage = 0; stage < 3; stage++)
            {
                if (stage == 1) File.WriteAllBytes(source, [0]);
                if (stage == 2) File.WriteAllBytes(source, new byte[OriginalMetadataImporter.ExecutableLength]);
                var (code, output, error) = Run(root, ["import-metadata", "--exe", source, "--output", destination]);
                Assert.Equal(1, code);
                Assert.Empty(output);
                Assert.Contains("Metadata import failed", error);
                if (stage == 2) Assert.Contains("SHA-256 mismatch", error);
                Assert.False(Directory.Exists(destination));
                Assert.Empty(Directory.GetDirectories(scratch));
            }
        }
        finally
        {
            File.Delete(source);
            Directory.Delete(scratch);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void Existing_output_is_preserved_before_attempting_import()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(root, "scratch");
        string destination = Path.Combine(scratch, "output");
        Directory.CreateDirectory(destination);
        string sentinel = Path.Combine(destination, "keep.txt");
        File.WriteAllText(sentinel, "keep");
        try
        {
            var (code, _, error) = Run(root, ["import-metadata", "--exe", Path.Combine(scratch, "missing.exe"), "--output", destination]);
            Assert.Equal(1, code);
            Assert.Contains("already exists", error);
            Assert.Equal("keep", File.ReadAllText(sentinel));
        }
        finally
        {
            File.Delete(sentinel);
            Directory.Delete(destination);
            Directory.Delete(scratch);
            Directory.Delete(root);
        }
    }

    private static (int Code, string Output, string Error) Run(string root, string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        int code = new ImportMetadataCommand(root).Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
