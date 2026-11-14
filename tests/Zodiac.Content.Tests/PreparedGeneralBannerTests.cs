using System.Text;
using System.Text.Json.Nodes;
using Fft.Tests.Support;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedGeneralBannerTests
{
    [Theory]
    [InlineData("unknown-version")][InlineData("unknown-field")][InlineData("duplicate-piece-index")]
    [InlineData("crop-past-row-199")][InlineData("nonpositive-dimension")][InlineData("piece-height-not-31")]
    [InlineData("screen-coordinate-overflow")][InlineData("unsupported-material")][InlineData("other-condition")]
    [InlineData("other-image")][InlineData("original-parity-required")][InlineData("missing-field")]
    [InlineData("duplicate-reference")][InlineData("nested-unknown-field")][InlineData("null-color")]
    [InlineData("wrong-index-format")][InlineData("wrong-palette-format")][InlineData("wrong-bank")]
    [InlineData("wrong-count")][InlineData("horizontal-gradient")][InlineData("pretend-retail")]
    [InlineData("missing-piece-field")][InlineData("wrong-triplet")][InlineData("null-pieces")][InlineData("null-source")]
    [InlineData("channel-overflow")][InlineData("unknown-source-kind")][InlineData("negative-uv")]
    public void ClosedProfileRejectsMalformedMetadata(string variant)
    {
        var node = JsonNode.Parse(AuthoredGeneralBanner.Definition().ToUtf8())!;
        var piece = node["pieces"]![0]!;
        switch (variant)
        {
            case "unknown-version": node["schemaVersion"] = 2; break;
            case "unknown-field": node["extra"] = true; break;
            case "duplicate-piece-index": node["pieces"]![1]!["sourceIndex"] = 0; break;
            case "crop-past-row-199": piece["v"] = 170; break;
            case "nonpositive-dimension": piece["width"] = 0; break;
            case "piece-height-not-31": piece["height"] = 30; break;
            case "screen-coordinate-overflow": piece["x"] = int.MaxValue; break;
            case "unsupported-material": piece["material"] = "average"; break;
            case "other-condition": node["condition"] = 0; break;
            case "other-image": node["image"] = 1; break;
            case "original-parity-required": node["parity"] = 0; break;
            case "missing-field": node.AsObject().Remove("condition"); break;
            case "duplicate-reference": node["paletteId"] = node["indicesId"]!.GetValue<string>(); break;
            case "nested-unknown-field": piece["unused"] = 0; break;
            case "null-color": piece["colors"]![0] = null; break;
            case "wrong-index-format": node["indexFormat"] = "packed"; break;
            case "wrong-palette-format": node["paletteFormat"] = "rgba"; break;
            case "wrong-bank": node["paletteBank"] = 0; break;
            case "wrong-count": node["pieces"]!.AsArray().RemoveAt(5); break;
            case "horizontal-gradient": piece["colors"]![1]!["r"] = 1; break;
            case "pretend-retail": node["source"]!["bonusSha256"] = new string('a', 64); break;
            case "missing-piece-field": piece.AsObject().Remove("triplet"); break;
            case "wrong-triplet": piece["triplet"] = 1; break;
            case "null-pieces": node["pieces"] = null; break;
            case "null-source": node["source"] = null; break;
            case "channel-overflow": piece["colors"]![0]!["r"] = 256; break;
            case "unknown-source-kind": node["source"]!["kind"] = "retail-ish"; break;
            case "negative-uv": piece["u"] = -1; break;
        }
        Assert.Throws<InvalidDataException>(() => PreparedGeneralBanner.Parse(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }

    [Theory]
    [InlineData("missing-asset")][InlineData("hash-mismatch")][InlineData("bad-byte-length")][InlineData("index-16")]
    [InlineData("bad-palette-length")][InlineData("wrong-id")]
    public void LoadRejectsBeforePublishing(string variant)
    {
        string root = Path.Combine(Path.GetTempPath(), "banner-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] indices = AuthoredGeneralBanner.Loaded().Indices.ToArray();
            if (variant == "index-16") indices[42] = 16;
            AuthoredGeneralBanner.Write(root, indices: variant == "bad-byte-length" ? [1] : indices,
                palette: variant == "bad-palette-length" ? [0] : null);
            if (variant == "hash-mismatch") File.WriteAllBytes(Path.Combine(root, "indices.bin"), new byte[51200]);
            if (variant == "missing-asset") File.Delete(Path.Combine(root, "palette.bin"));
            var store = LooseAssetStore.Load(root);
            Assert.ThrowsAny<Exception>(() => PreparedGeneralBanner.Load(store, new(variant == "wrong-id" ? "sample:missing" : "sample:banner/test")));
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void PreparedOnlyRoundTripIsImmutableAndCarriesNoOriginalParserOrHoldArtwork()
    {
        string root = Path.Combine(Path.GetTempPath(), "banner-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            AuthoredGeneralBanner.Write(root);
            var loaded = PreparedGeneralBanner.Load(LooseAssetStore.Load(root), new("sample:banner/test"));
            Assert.Equal(AuthoredGeneralBanner.Definition().ToUtf8(), loaded.Definition.ToUtf8());
            Assert.Equal("authored-test", loaded.Definition.Source.Kind);
            Assert.Null(loaded.Definition.Source.BonusSha256);
            Assert.Equal(60, loaded.Definition.ToLogicalDefinition(60).HoldFrames);
            byte[] copy = loaded.Indices.ToArray(); copy[0] = 15; Assert.Equal(1, loaded.Indices[0]);
            Assert.Equal(4, Directory.GetFiles(root).Length); // metadata, manifest and two prepared payloads only
        }
        finally { Cleanup(root); }
    }

    [Theory]
    [InlineData("root")][InlineData("piece")][InlineData("source")]
    public void RejectsDuplicateFields(string where)
    {
        string json = Encoding.UTF8.GetString(AuthoredGeneralBanner.Definition().ToUtf8());
        json = where switch
        {
            "root" => json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1"),
            "piece" => json.Replace("\"triplet\": 0", "\"triplet\": 0, \"triplet\": 0"),
            _ => json.Replace("\"kind\": \"authored-test\"", "\"kind\": \"authored-test\", \"kind\": \"authored-test\""),
        };
        Assert.Throws<InvalidDataException>(() => PreparedGeneralBanner.Parse(Encoding.UTF8.GetBytes(json)));
    }

    private static void Cleanup(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (string name in new[] { "banner.json", "indices.bin", "palette.bin", PreparedAssetManifest.FileName }) File.Delete(Path.Combine(root, name));
        Directory.Delete(root);
    }
}
