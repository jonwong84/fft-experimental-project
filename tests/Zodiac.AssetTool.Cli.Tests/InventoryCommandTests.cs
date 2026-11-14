using Zodiac.AssetTool;
using Zodiac.AssetTool.Cli;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class InventoryCommandTests
{
    [Fact]
    public void Run_rejects_missing_required_arguments()
    {
        using var workspace = new TemporaryDirectory();
        var command = new InventoryCommand(workspace.Path);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = command.Run(["inventory", "--bin", "game.bin"], output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Both --bin and --cue are required", error.ToString());
    }

    [Fact]
    public void ValidateImageProfile_rejects_wrong_file_size_before_hashing()
    {
        using var workspace = new TemporaryDirectory();
        string binPath = System.IO.Path.Combine(workspace.Path, "short.bin");
        File.WriteAllBytes(binPath, [1, 2, 3]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => InventoryCommand.ValidateImageProfile(binPath));

        Assert.Contains("Unsupported BIN size", exception.Message);
    }

    [Fact]
    public void ValidateCue_rejects_track_layout_outside_supported_profile()
    {
        using var workspace = new TemporaryDirectory();
        string binPath = System.IO.Path.Combine(workspace.Path, "game.bin");
        string cuePath = System.IO.Path.Combine(workspace.Path, "game.cue");
        File.WriteAllText(binPath, string.Empty);
        File.WriteAllText(cuePath, "FILE \"game.bin\" BINARY\nTRACK 01 MODE1/2352\nINDEX 01 00:00:00\n");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => InventoryCommand.ValidateCue(cuePath, binPath));

        Assert.Contains("Unsupported CUE layout", exception.Message);
    }

    [Fact]
    public void ValidateCue_rejects_reference_to_a_different_bin_path()
    {
        using var workspace = new TemporaryDirectory();
        string suppliedBinPath = System.IO.Path.Combine(workspace.Path, "game.bin");
        string otherBinPath = System.IO.Path.Combine(workspace.Path, "other.bin");
        string cuePath = System.IO.Path.Combine(workspace.Path, "game.cue");
        File.WriteAllText(suppliedBinPath, string.Empty);
        File.WriteAllText(otherBinPath, string.Empty);
        File.WriteAllText(cuePath, "FILE \"other.bin\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:00\n");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => InventoryCommand.ValidateCue(cuePath, suppliedBinPath));

        Assert.Contains("does not match the supplied BIN path", exception.Message);
    }

    [Fact]
    public void Compare_reports_missing_extra_and_metadata_mismatch_by_path()
    {
        IsoFileEntry[] managed =
        [
            new("MATCH.BIN;1", "MATCH.BIN", 10, 100),
            new("CHANGED.BIN;1", "CHANGED.BIN", 21, 200),
            new("EXTRA.BIN;1", "EXTRA.BIN", 30, 300),
        ];
        ReferenceInventoryEntry[] reference =
        [
            new("MATCH.BIN", 10, 100),
            new("CHANGED.BIN", 20, 201),
            new("MISSING.BIN", 40, 400),
        ];

        InventoryComparisonResult result = InventoryComparison.Compare(managed, reference);

        Assert.Equal(new[] { "MISSING.BIN" }, result.MissingPaths);
        Assert.Equal(new[] { "EXTRA.BIN" }, result.ExtraPaths);
        InventoryMismatch mismatch = Assert.Single(result.MismatchedEntries);
        Assert.Equal("CHANGED.BIN", mismatch.Path);
        Assert.Equal(21u, mismatch.ManagedLba);
        Assert.Equal(20u, mismatch.ReferenceLba);
        Assert.Equal(200u, mismatch.ManagedBytes);
        Assert.Equal(201u, mismatch.ReferenceBytes);
        Assert.False(result.IsMatch);
    }

    [Fact]
    public void Compare_accepts_equal_path_extent_and_length()
    {
        var entry = new IsoFileEntry("SAME.BIN;1", "SAME.BIN", 10, 100);

        InventoryComparisonResult result = InventoryComparison.Compare(
            [entry],
            [new ReferenceInventoryEntry("SAME.BIN", 10, 100)]);

        Assert.True(result.IsMatch);
        Assert.Empty(result.MissingPaths);
        Assert.Empty(result.ExtraPaths);
        Assert.Empty(result.MismatchedEntries);
        Assert.Empty(result.LengthsNotComparable);
    }

    [Fact]
    public void Compare_classifies_mixed_extracted_size_separately_from_iso_logical_length()
    {
        var entry = new IsoFileEntry("VIDEO.STR;1", "VIDEO.STR", 20, 2048);

        InventoryComparisonResult result = InventoryComparison.Compare(
            [entry],
            [new ReferenceInventoryEntry("VIDEO.STR", 20, 2336, "mixed")]);

        Assert.True(result.IsMatch);
        Assert.Empty(result.MismatchedEntries);
        InventoryLengthNotComparable length = Assert.Single(result.LengthsNotComparable);
        Assert.Equal("VIDEO.STR", length.Path);
        Assert.Equal("mixed", length.ReferenceType);
        Assert.Equal(2048u, length.IsoLogicalBytes);
        Assert.Equal(2336u, length.ExtractedBytes);
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
