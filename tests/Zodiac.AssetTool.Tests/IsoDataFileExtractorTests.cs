using Zodiac.AssetTool;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class IsoDataFileExtractorTests
{
    [Fact]
    public void ExtractForm1DataFile_copies_multiple_sectors_and_truncates_final_sector()
    {
        using var imageStream = CreateImage(sectorCount: 2);
        var image = new Mode2RawImageReader(imageStream);
        var entry = new IsoFileEntry("SAMPLE.BIN;1", "DATA/SAMPLE.BIN", 0, 2050);
        string outputRoot = CreateTemporaryDirectory();

        try
        {
            string outputPath = IsoDataFileExtractor.ExtractForm1DataFile(image, entry, outputRoot);
            byte[] actual = File.ReadAllBytes(outputPath);

            Assert.Equal(2050, actual.Length);
            Assert.All(actual.AsSpan(0, 2048).ToArray(), value => Assert.Equal(0x11, value));
            Assert.Equal(new byte[] { 0x22, 0x22 }, actual[^2..]);
            Assert.Equal(Path.Combine(outputRoot, "DATA", "SAMPLE.BIN"), outputPath);
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.bin")]
    [InlineData("DATA/../outside.bin")]
    [InlineData("DATA/CON.txt")]
    [InlineData("DATA/name.")]
    [InlineData("DATA/name:stream")]
    [InlineData("DATA\\name.bin")]
    public void ExtractForm1DataFile_rejects_unsafe_output_paths(string relativePath)
    {
        using var imageStream = CreateImage(sectorCount: 1);
        var image = new Mode2RawImageReader(imageStream);
        var entry = new IsoFileEntry("UNSAFE.BIN;1", relativePath, 0, 1);
        string outputRoot = CreateTemporaryDirectory();

        try
        {
            Assert.Throws<InvalidDataException>(
                () => IsoDataFileExtractor.ExtractForm1DataFile(image, entry, outputRoot));
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public void ExtractForm1DataFile_does_not_replace_existing_file()
    {
        using var imageStream = CreateImage(sectorCount: 1);
        var image = new Mode2RawImageReader(imageStream);
        var entry = new IsoFileEntry("SAMPLE.BIN;1", "SAMPLE.BIN", 0, 1);
        string outputRoot = CreateTemporaryDirectory();
        string existingPath = Path.Combine(outputRoot, "SAMPLE.BIN");
        File.WriteAllBytes(existingPath, [0x7f]);

        try
        {
            Assert.Throws<IOException>(
                () => IsoDataFileExtractor.ExtractForm1DataFile(image, entry, outputRoot));
            Assert.Equal(new byte[] { 0x7f }, File.ReadAllBytes(existingPath));
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public void ValidateOutputPaths_rejects_case_insensitive_windows_collisions()
    {
        IsoFileEntry[] entries =
        [
            new("A.BIN;1", "DATA/A.BIN", 0, 1),
            new("a.bin;1", "data/a.bin", 1, 1),
        ];

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => IsoDataFileExtractor.ValidateOutputPaths(entries));

        Assert.Contains("collide on Windows", exception.Message);
    }

    [Fact]
    public void ValidateOutputPaths_rejects_file_directory_conflicts()
    {
        IsoFileEntry[] entries =
        [
            new("A;1", "DATA/A", 0, 1),
            new("B;1", "DATA/A/B", 1, 1),
        ];

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => IsoDataFileExtractor.ValidateOutputPaths(entries));

        Assert.Contains("conflicts with child path", exception.Message);
    }

    private static MemoryStream CreateImage(int sectorCount)
    {
        byte[] bytes = new byte[sectorCount * Mode2RawImageReader.RawSectorSize];
        byte[] sync = [0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00];
        for (int i = 0; i < sectorCount; i++)
        {
            int sectorOffset = i * Mode2RawImageReader.RawSectorSize;
            sync.CopyTo(bytes, sectorOffset);
            bytes[sectorOffset + 15] = 2;
            bytes[sectorOffset + 16] = 1;
            bytes[sectorOffset + 20] = 1;
            Array.Fill(bytes, (byte)(i == 0 ? 0x11 : 0x22),
                sectorOffset + Mode2RawImageReader.UserDataOffset,
                Mode2RawImageReader.UserDataSize);
        }

        return new MemoryStream(bytes);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fft-asset-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
