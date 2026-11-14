using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedSpritePoseTests
{
    [Fact]
    public void Prepared_contract_roundtrips_deterministically_and_owns_its_collections()
    {
        var palette = new ushort[16]; palette[1] = 31;
        SpritePosePart[] parts = [new(0, -16, -35, 0, 0, 8, 8, true, false)];
        var pose = Pose(palette: palette, parts: parts, wait: 0);
        palette[1] = 999; parts[0] = parts[0] with { X = 10 };
        Assert.Equal(31, pose.RawPalette[1]); Assert.Equal(-16, pose.PartsBackToFront[0].X);
        var loaded = PreparedSpritePose.Parse(pose.ToUtf8());
        Assert.Equal(0, loaded.Wait);
        Assert.True(loaded.PartsBackToFront[0].FlipHorizontal);
        Assert.Equal(pose.ToUtf8(), loaded.ToUtf8());
    }

    [Theory]
    [InlineData("missing")] [InlineData("unknown")] [InlineData("version")]
    [InlineData("format")] [InlineData("dimensions")] [InlineData("pixel-format")]
    [InlineData("fractional")] [InlineData("string-number")] [InlineData("frame")]
    [InlineData("palette-range")] [InlineData("palette-count")] [InlineData("piece-field")]
    [InlineData("order")] [InlineData("rect-overflow")] [InlineData("flip-type")]
    [InlineData("source-hash")] [InlineData("source-revision")] [InlineData("edition")]
    public void Invalid_metadata_is_rejected_before_runtime_use(string kind)
    {
        var json = JsonNode.Parse(Pose().ToUtf8())!;
        switch (kind)
        {
            case "missing": json.AsObject().Remove("wait"); break;
            case "unknown": json["extra"] = 1; break;
            case "version": json["schemaVersion"] = 2; break;
            case "format": json["format"] = "other"; break;
            case "dimensions": json["width"] = 1920; break;
            case "pixel-format": json["pixelFormat"] = "bgra8"; break;
            case "fractional": json["wait"] = 1.5; break;
            case "string-number": json["wait"] = "6"; break;
            case "frame": json["frameId"] = 208; break;
            case "palette-range": json["paletteIndex"] = 6; break;
            case "palette-count": json["rawPalette"]!.AsArray().RemoveAt(15); break;
            case "piece-field": json["partsBackToFront"]![0]!["extra"] = 1; break;
            case "order": json["partsBackToFront"]![0]!["sourceIndex"] = 1; break;
            case "rect-overflow": json["partsBackToFront"]![0]!["u"] = int.MaxValue; break;
            case "flip-type": json["partsBackToFront"]![0]!["flipVertical"] = 0; break;
            case "source-hash": json["source"]!["sprSha256"] = "invalid"; break;
            case "source-revision": json["source"]!["referenceRevision"] = "main"; break;
            case "edition": json["source"]!["edition"] = "other"; break;
        }
        Assert.Throws<InvalidDataException>(() => PreparedSpritePose.Parse(Encoding.UTF8.GetBytes(json.ToJsonString())));
    }

    [Theory]
    [InlineData("\"wait\":", "\"wait\": 1, \"wait\":")]
    [InlineData("\"x\":", "\"x\": 1, \"x\":")]
    [InlineData("\"edition\":", "\"edition\": \"SCUS-94221\", \"edition\":")]
    public void Duplicate_json_keys_are_not_silently_overwritten(string find, string replacement)
    {
        string json = Encoding.UTF8.GetString(Pose().ToUtf8()).Replace(find, replacement, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => PreparedSpritePose.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Constructor_rejects_bad_ids_rectangles_offsets_and_unbounded_metadata()
    {
        Assert.Throws<ArgumentException>(() => new PreparedSpritePose(new("sample:pose"), new("sample:pose"), 6, 9, 6, 0,
            new ushort[16], [Part()], Source()));
        Assert.Throws<ArgumentException>(() => Pose(parts: [Part() with { X = -129 }]));
        Assert.Throws<ArgumentException>(() => Pose(parts: [Part() with { Width = 48, Height = 48 }]));
        Assert.Throws<ArgumentException>(() => Pose(parts: [Part() with { U = 249 }]));
        Assert.Throws<InvalidDataException>(() => PreparedSpritePose.Parse(new byte[32769]));
    }

    [Fact]
    public void Runtime_loads_only_prepared_files_and_verifies_the_atlas_hash_and_format()
    {
        WithStore((root, pose) =>
        {
            var loaded = PreparedSpritePose.Load(LooseAssetStore.Load(root), pose.Id);
            Assert.Equal(pose.ToUtf8(), loaded.Definition.ToUtf8());
            Assert.Equal(256 * 256 * 4, loaded.Rgba.Length);
            File.WriteAllBytes(Path.Combine(root, "atlas.rgba"), new byte[256 * 256 * 4 - 1]);
            Assert.Throws<InvalidDataException>(() => PreparedSpritePose.Load(LooseAssetStore.Load(root), pose.Id));
        });
    }

    [Theory]
    [InlineData("alpha")] [InlineData("short-atlas")] [InlineData("id-mismatch")] [InlineData("hash")]
    public void Manifest_or_pose_mismatches_cannot_reach_rendering(string kind)
    {
        WithStore((root, pose) =>
        {
            byte[] rgba = new byte[kind == "short-atlas" ? 4 : 256 * 256 * 4];
            if (kind == "alpha") rgba[3] = 128;
            byte[] metadata = pose.ToUtf8();
            if (kind == "id-mismatch") metadata = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(metadata)
                .Replace("sample:pose", "sample:other-pose", StringComparison.Ordinal));
            File.WriteAllBytes(Path.Combine(root, "pose.json"), metadata);
            File.WriteAllBytes(Path.Combine(root, "atlas.rgba"), rgba);
            File.Delete(Path.Combine(root, PreparedAssetManifest.FileName));
            Manifest(pose, metadata, rgba).SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
            if (kind == "hash") { rgba[0] = 99; File.WriteAllBytes(Path.Combine(root, "atlas.rgba"), rgba); }
            Assert.Throws<InvalidDataException>(() => PreparedSpritePose.Load(LooseAssetStore.Load(root), pose.Id));
        });
    }

    private static PreparedSpritePose Pose(ushort[]? palette = null, SpritePosePart[]? parts = null, int wait = 6) =>
        new(new("sample:pose"), new("sample:atlas"), 6, 9, wait, 0, palette ?? new ushort[16], parts ?? [Part()], Source());
    private static SpritePosePart Part() => new(0, 0, 0, 0, 0, 8, 8, false, false);
    private static SpritePoseSource Source() => new("SCUS-94221", new('a', 40), new('b', 64), new('c', 64), new('d', 64));

    private static PreparedAssetManifest Manifest(PreparedSpritePose pose, byte[] json, byte[] rgba) => new(
        [Entry(pose.Id, "pose.json", json), Entry(pose.TextureId, "atlas.rgba", rgba)]);
    private static PreparedAssetEntry Entry(AssetId id, string path, byte[] bytes) =>
        new(id, path, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private static void WithStore(Action<string, PreparedSpritePose> action)
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-pose-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var pose = Pose(); var json = pose.ToUtf8(); var rgba = new byte[256 * 256 * 4];
            File.WriteAllBytes(Path.Combine(root, "pose.json"), json);
            File.WriteAllBytes(Path.Combine(root, "atlas.rgba"), rgba);
            Manifest(pose, json, rgba).SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
            action(root, pose);
        }
        finally
        {
            foreach (string file in new[] { "pose.json", "atlas.rgba", PreparedAssetManifest.FileName }) File.Delete(Path.Combine(root, file));
            Directory.Delete(root);
        }
    }
}
