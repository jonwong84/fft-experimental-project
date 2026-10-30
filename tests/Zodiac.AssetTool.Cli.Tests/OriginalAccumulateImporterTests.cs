using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Zodiac.AssetTool.Cli;
using Zodiac.Content.ContentPackages;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class OriginalAccumulateImporterTests
{
    private static int Offset(uint address) => checked((int)(address - 0x80010000 + 0x800));

    [Fact]
    public void Explicit_supported_export_preserves_all_records_and_never_certifies_synthetic_edition()
    {
        byte[] image = Image(); byte[] original = image.ToArray();
        var metadata = OriginalMetadataImporter.DecodeSelection(image).Package;
        string metadataJson = metadata.ToJson();
        var result = OriginalAccumulateImporter.DecodeSelection(image, metadata);
        result.Package.ValidateAgainst(metadata);
        Assert.Equal(1, Assert.Single(result.Package.Abilities).Amount);
        Assert.Equal(metadataJson, metadata.ToJson()); Assert.Equal(original, image);
        Assert.Equal(6, result.Records.Length);
        foreach (var record in result.Records)
        {
            Assert.Equal(Offset(record.VirtualAddress), record.FileOffset);
            Assert.Equal(Convert.ToHexStringLower(image.AsSpan(record.FileOffset, record.ByteLength)), record.RawHex);
        }
        string json = OriginalAccumulateImporter.ToJson(result);
        Assert.Equal(json, OriginalAccumulateImporter.ToJson(OriginalAccumulateImporter.DecodeSelection(image, metadata)));
        var parsed = JsonNode.Parse(json)!;
        Assert.Null(parsed["targetEdition"]); Assert.Null(parsed["input"]!["sha256"]);
        Assert.Equal("unverified-layout-decoding", parsed["confidence"]!.GetValue<string>());
        Assert.Equal(OriginalAccumulateImporter.ReferenceRevision, parsed["referenceRevision"]!.GetValue<string>());
        Assert.Throws<InvalidDataException>(() => OriginalAccumulateImporter.ImportVerified(image, metadata));
        Array.Clear(image); Assert.Equal(json, OriginalAccumulateImporter.ToJson(result));
        Assert.Equal(result.Package.ToJson(), ImmediateAbilityContentPackage.ReadJson(result.Package.ToJson()).ToJson());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public void Every_unsupported_secondary_field_is_rejected_without_rewriting_input(int field)
    {
        byte[] image = Image(); var metadata = OriginalMetadataImporter.DecodeSelection(image).Package;
        image[Offset(0x8005fbf0 + 146 * 14) + field] ^= 0xff;
        byte[] before = image.ToArray();
        Assert.Throws<InvalidDataException>(() => OriginalAccumulateImporter.DecodeSelection(image, metadata));
        Assert.Equal(before, image);
    }

    [Theory]
    [InlineData("job")] [InlineData("slot")] [InlineData("menu")] [InlineData("status-type")]
    [InlineData("status-bits")] [InlineData("learning")] [InlineData("header")] [InlineData("length")]
    public void Mismatched_dependency_and_reference_paths_fail_closed(string kind)
    {
        byte[] image = Image(); var metadata = OriginalMetadataImporter.DecodeSelection(image).Package;
        switch (kind)
        {
            case "job": image[Offset(0x800610b8 + 74 * 48)] = 6; break;
            case "slot": image[Offset(0x80064a94 + 5 * 25) + 3] = 147; break;
            case "menu": image[Offset(0x80065cb4 + 5)] = 8; break;
            case "status-type": image[Offset(0x80063fc4)] = 128; break;
            case "status-bits": image[Offset(0x80063fc4) + 1] = 1; break;
            case "learning": image[Offset(0x8005ebf0 + 146 * 8)]++; break;
            case "header": image[0] = 0; break;
            case "length": image = image[..0x900]; break;
        }
        Assert.Throws<InvalidDataException>(() => OriginalAccumulateImporter.DecodeSelection(image, metadata));
    }

    [Theory] [InlineData("missing")] [InlineData("outside")] [InlineData("hash")]
    public void Command_requires_metadata_scratch_confinement_and_edition_before_publishing(string kind)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "fft-accumulate-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(workspace, "scratch"); Directory.CreateDirectory(scratch);
        string image = Path.Combine(scratch, "synthetic.exe");
        string metadata = Path.Combine(scratch, "metadata.json");
        string destination = Path.Combine(scratch, "output");
        try
        {
            File.WriteAllBytes(image, Image());
            File.WriteAllText(metadata, OriginalMetadataImporter.DecodeSelection(Image()).Package.ToJson());
            using var output = new StringWriter(); using var error = new StringWriter();
            string[] args = kind == "missing" ? ["import-accumulate", "--exe", image, "--output", destination] :
                ["import-accumulate", "--exe", kind == "outside" ? Path.Combine(workspace, "outside.exe") : image,
                    "--metadata", metadata, "--output", destination];
            int exit = new ImportMetadataCommand(workspace).Run(args, output, error);
            Assert.Equal(kind == "missing" ? 2 : 1, exit);
            Assert.Empty(output.ToString()); Assert.NotEmpty(error.ToString()); Assert.False(Directory.Exists(destination));
            Assert.Equal(new[] { image, metadata }.Order(), Directory.GetFiles(scratch).Order());
        }
        finally
        {
            File.Delete(image); File.Delete(metadata); Directory.Delete(scratch); Directory.Delete(workspace);
        }
    }

    private static byte[] Image()
    {
        var image = new byte[OriginalMetadataImporter.ExecutableLength];
        "PS-X EXE"u8.CopyTo(image);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18), 0x80010000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x1c), (uint)image.Length - 0x800);
        // Invented learning and job bytes. Only the selected closed effect constraints are source-derived.
        int job = Offset(0x800610b8 + 74 * 48);
        image[job] = 5; image[job + 0x17] = 1; image[job + 0x18] = 2;
        image[Offset(0x80064a94 + 5 * 25) + 3] = 146;
        new byte[] { 123, 0, 37, 65, 1, 2, 3, 4 }.CopyTo(image, Offset(0x8005ebf0 + 146 * 8));
        image[Offset(0x8005fbf0 + 146 * 14) + 3] = 2;
        image[Offset(0x8005fbf0 + 146 * 14) + 4] = 16;
        image[Offset(0x8005fbf0 + 146 * 14) + 8] = 0x36;
        image[Offset(0x8005fbf0 + 146 * 14) + 10] = 1;
        return image;
    }
}
