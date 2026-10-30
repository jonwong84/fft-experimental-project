using System.Buffers.Binary;
using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class PrepareSpritePoseCommandTests
{
    [Theory]
    [InlineData("prepare-sprite-pose")]
    [InlineData("prepare-sprite-pose", "--spr")]
    [InlineData("prepare-sprite-pose", "--unknown", "x")]
    [InlineData("prepare-sprite-pose", "--spr", "x", "--spr", "y")]
    public void Missing_unknown_and_duplicate_options_report_usage(params string[] args)
    {
        var (code, error) = Run(Path.GetTempPath(), args);
        Assert.Equal(2, code); Assert.Contains("Usage:", error);
    }

    [Fact]
    public void Preparation_confines_inputs_and_output_to_scratch_and_preserves_existing_output()
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-pose-cli-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(root, "scratch"), destination = Path.Combine(scratch, "output");
        var input = Path.Combine(scratch, "source");
        Assert.Equal(1, Run(root, Args(input, Path.Combine(root, "outside"))).Code);
        Assert.Equal(1, Run(root, Args(Path.Combine(root, "outside"), destination)).Code);
        Assert.False(Directory.Exists(root));
        Directory.CreateDirectory(destination);
        string sentinel = Path.Combine(destination, "keep.txt"); File.WriteAllText(sentinel, "keep");
        try
        {
            var result = Run(root, Args(input, destination));
            Assert.Equal(1, result.Code); Assert.Contains("already exists", result.Error);
            Assert.Equal("keep", File.ReadAllText(sentinel));
        }
        finally { File.Delete(sentinel); Directory.Delete(destination); Directory.Delete(scratch); Directory.Delete(root); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(49572)]
    public void Missing_wrong_length_and_wrong_hash_inputs_create_no_prepared_output(int length)
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-pose-cli-" + Guid.NewGuid().ToString("N"));
        string scratch = Path.Combine(root, "scratch"), input = Path.Combine(scratch, "source");
        Directory.CreateDirectory(scratch);
        try
        {
            if (length != 0) File.WriteAllBytes(input, new byte[length]);
            var (code, error) = Run(root, Args(input, Path.Combine(scratch, "output")));
            Assert.Equal(1, code);
            if (length == 49572) Assert.Contains("SHA-256 mismatch", error);
            Assert.Empty(Directory.GetDirectories(scratch));
        }
        finally { File.Delete(input); Directory.Delete(scratch); Directory.Delete(root); }
    }

    [Fact]
    public void Synthetic_decode_export_and_runtime_load_preserve_overlapping_piece_order_without_original_files()
    {
        // Two opaque pieces at the same anchor: native submission 0=red, 1=green.
        var spr = new byte[UnitSpriteSheet.FixedLength];
        BinaryPrimitives.WriteUInt16LittleEndian(spr.AsSpan(2), 31);
        BinaryPrimitives.WriteUInt16LittleEndian(spr.AsSpan(4), 31 << 5);
        spr[0x200] = 1; spr[0x204] = 2;
        var shp = new byte[0x414];
        BinaryPrimitives.WriteInt32LittleEndian(shp, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(shp.AsSpan(4), 61);
        BinaryPrimitives.WriteUInt16LittleEndian(shp.AsSpan(6), 255);
        BinaryPrimitives.WriteUInt16LittleEndian(shp.AsSpan(0x408), 10);
        shp[0x40a] = 1; // two parts, source u=0 then u=8
        BinaryPrimitives.WriteUInt16LittleEndian(shp.AsSpan(0x412), 1);
        var seq = new byte[0x408];
        BinaryPrimitives.WriteUInt16LittleEndian(seq, 120);
        BinaryPrimitives.WriteUInt16LittleEndian(seq.AsSpan(2), 255);
        BinaryPrimitives.WriteUInt16LittleEndian(seq.AsSpan(0x404), 2);
        seq[0x407] = 6;
        var decoded = OrdinaryUnitPoseDecoder.Decode(UnitSpriteSheet.Parse(spr), UnitShapeFile.Parse(shp), UnitSequenceFile.Parse(seq), 6, 0);
        var definition = SpritePoseContentExporter.Create(decoded, new("sample:pose"), new("sample:atlas"),
            new("SCUS-94221", new('a', 40), new('b', 64), new('c', 64), new('d', 64)));
        Assert.Equal(definition.ToUtf8(), SpritePoseContentExporter.Create(decoded, definition.Id, definition.TextureId, definition.Source).ToUtf8());
        string root = Path.Combine(Path.GetTempPath(), "fft-pose-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var json = definition.ToUtf8(); var rgba = decoded.Rgba.ToArray();
            File.WriteAllBytes(Path.Combine(root, "pose.json"), json);
            File.WriteAllBytes(Path.Combine(root, "atlas.rgba"), rgba);
            new PreparedAssetManifest([Entry(definition.Id, "pose.json", json), Entry(definition.TextureId, "atlas.rgba", rgba)])
                .SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
            var loaded = PreparedSpritePose.Load(LooseAssetStore.Load(root), definition.Id);
            Assert.Equal(new[] { 1, 0 }, loaded.Definition.PartsBackToFront.Select(p => p.SourceIndex));
            byte[] top = [];
            foreach (var part in loaded.Definition.PartsBackToFront)
                top = loaded.Rgba.Skip((part.V * 256 + part.U) * 4).Take(4).ToArray();
            Assert.Equal(new byte[] { 255, 0, 0, 255 }, top); // head insertion paints native part 0 last
            Assert.Equal(3, Directory.GetFiles(root).Length);
        }
        finally
        {
            foreach (string file in new[] { "pose.json", "atlas.rgba", PreparedAssetManifest.FileName }) File.Delete(Path.Combine(root, file));
            Directory.Delete(root);
        }
    }

    private static PreparedAssetEntry Entry(AssetId id, string path, byte[] bytes) =>
        new(id, path, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    private static string[] Args(string input, string output) =>
        ["prepare-sprite-pose", "--spr", input, "--shp", input, "--seq", input, "--output", output];
    private static (int Code, string Error) Run(string root, string[] args)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        int code = new PrepareSpritePoseCommand(root).Run(args, output, error);
        return (code, error.ToString());
    }
}
