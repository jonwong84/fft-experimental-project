using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedBattleMapTests
{
    [Fact]
    public void Strict_round_trip_preserves_order_signed_coordinates_raw_flags_and_frozen_metadata()
    {
        var original = Sample();
        byte[] bytes = original.ToUtf8();
        var parsed = PreparedBattleMap.Parse(bytes);
        Assert.Equal(bytes, parsed.ToUtf8());
        Assert.Equal(original.Id, parsed.Id);
        Assert.Equal(new[] { "textured-quad", "untextured-triangle", "textured-triangle", "untextured-quad" }, parsed.Polygons.Select(p => p.Kind));
        Assert.Equal(new MapVector(short.MinValue, -123, short.MaxValue), parsed.Polygons[0].Vertices[0]);
        Assert.Equal(ushort.MaxValue, parsed.Polygons[0].Flags);
        Assert.Equal(new ushort[] { 65535, 4, 3, 2, 1, 0 }, parsed.Polygons[0].SourceMaterial);
        Assert.Equal((ushort?)0xfffe, parsed.Polygons[2].SourceTerrainTag);
        Assert.Null(parsed.Polygons[2].Terrain);
        Assert.Equal(original.Presentation.LightMatrices.ToArray(), parsed.Presentation.LightMatrices.ToArray());
        Assert.Equal(original.Presentation.TextureAnimations.ToArray(), parsed.Presentation.TextureAnimations.ToArray());
        Assert.Equal(original.Palettes.ToArray(), parsed.Palettes.ToArray());
    }

    [Fact]
    public void Terrain_projection_preserves_two_dense_layers_and_normalized_values_without_decoding_raw_bytes()
    {
        var map = Sample().ToBattleMap();
        Assert.Equal(2, map.LayerCount);
        Assert.True(map.TryGetTile(new(1, 0, 0), out var lower));
        Assert.True(map.TryGetTile(new(1, 0, 1), out var upper));
        Assert.Equal(2, lower.Height); Assert.Equal(4, upper.Height);
        Assert.Equal(new TileSlope(1, 2, 3, 0), lower.Slope);
        Assert.Equal(31, lower.HalfHeight); Assert.Equal(7, lower.WaterDepth); Assert.Equal(31, lower.CeilingDepth);
        Assert.True(lower.IsBlocked); Assert.True(lower.IsUntargetable); Assert.True(lower.CannotStop);
        Assert.Equal("base:terrain/ps1/1d", lower.Surface.Value);
        // Every opaque record is all 0xff; normalized values above remain independent.
        Assert.Equal(0, upper.HalfHeight);
    }

    [Theory]
    [InlineData("version")] [InlineData("profile")] [InlineData("missing")]
    [InlineData("unknown")] [InlineData("nested-unknown")] [InlineData("null")]
    [InlineData("numeric-string")] [InlineData("dimension")]
    [InlineData("surface")] [InlineData("height")] [InlineData("slope")]
    [InlineData("flags")] [InlineData("source-bytes")] [InlineData("palette-count")]
    [InlineData("presentation-count")] [InlineData("hash")]
    [InlineData("page")] [InlineData("bank")] [InlineData("vertices")]
    [InlineData("normals")] [InlineData("uvs")] [InlineData("material")]
    [InlineData("terrain")] [InlineData("source-index")] [InlineData("duplicate-index")]
    [InlineData("untextured-metadata")] [InlineData("kind")]
    public void Malformed_or_unsupported_prepared_data_is_rejected(string change)
    {
        var root = JsonNode.Parse(Sample().ToUtf8())!;
        var polygon = root["polygons"]![0]!;
        var tile = root["tiles"]![0]!;
        switch (change)
        {
            case "version": root["version"] = 2; break;
            case "profile": root["profile"] = "night-rain"; break;
            case "missing": root.AsObject().Remove("textureId"); break;
            case "unknown": root["extra"] = 1; break;
            case "nested-unknown": polygon["vertices"]![0]!["extra"] = 1; break;
            case "null": root["source"] = null; break;
            case "numeric-string": tile["height"] = "1"; break;
            case "dimension": root["width"] = int.MaxValue; break;
            case "surface": tile["surfaceId"] = "base:terrain/ps1/00"; break;
            case "height": tile["height"] = 256; break;
            case "slope": tile["slopeNorth"] = 4; break;
            case "flags": tile["movementFlags"] = 8; break;
            case "source-bytes": tile["sourceBytes"] = new JsonArray(1); break;
            case "palette-count": root["palettes"]!.AsArray().RemoveAt(0); break;
            case "presentation-count": root["presentation"]!["lightMatrices"]!.AsArray().RemoveAt(0); break;
            case "hash": root["source"]!["meshSha256"] = "unverified"; break;
            case "page": polygon["texturePage"] = 4; break;
            case "bank": polygon["paletteBank"] = 16; break;
            case "vertices": polygon["vertices"]!.AsArray().RemoveAt(0); break;
            case "normals": polygon["normals"]!.AsArray().RemoveAt(0); break;
            case "uvs": polygon["uvs"]!.AsArray().RemoveAt(0); break;
            case "material": polygon["sourceMaterial"]!.AsArray().RemoveAt(0); break;
            case "terrain": polygon["terrain"]!["layer"] = 2; break;
            case "source-index": polygon["sourceIndex"] = 768; break;
            case "duplicate-index": root["polygons"]!.AsArray().Add(polygon.DeepClone()); break;
            case "untextured-metadata": root["polygons"]![1]!["texturePage"] = 0; break;
            case "kind": polygon["kind"] = "invented"; break;
        }
        Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Parse(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    [Fact]
    public void Duplicate_fields_missing_nullable_fields_numeric_overflow_and_budget_are_rejected()
    {
        string json = Encoding.UTF8.GetString(Sample().ToUtf8());
        Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Parse(Encoding.UTF8.GetBytes(json.Replace("\"version\":1", "\"version\":1,\"version\":1"))));
        var root = JsonNode.Parse(json)!;
        root["polygons"]![1]!.AsObject().Remove("terrain");
        Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Parse(Encoding.UTF8.GetBytes(root.ToJsonString())));
        root = JsonNode.Parse(json)!;
        root["polygons"]![0]!["vertices"]![0]!["x"] = 32768;
        Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Parse(Encoding.UTF8.GetBytes(root.ToJsonString())));
        Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Parse(new byte[PreparedBattleMap.MaxMetadataBytes + 1]));
    }

    [Fact]
    public void Constructor_snapshots_caller_lists_and_rejects_invalid_polygon_and_layer_counts()
    {
        var sample = Sample();
        var polygons = sample.Polygons.ToList(); var tiles = sample.Tiles.ToList(); var palettes = sample.Palettes.ToList();
        var copy = new PreparedBattleMap(sample.Id, sample.TextureId, 2, 1, polygons, tiles, palettes, sample.Presentation, sample.Source);
        polygons.Clear(); tiles.Clear(); palettes.Clear();
        Assert.Equal(sample.ToUtf8(), copy.ToUtf8());
        Assert.Throws<InvalidDataException>(() => new PreparedBattleMap(sample.Id, sample.TextureId, 2, 1,
            Enumerable.Repeat(sample.Polygons[0], 1601), sample.Tiles, sample.Palettes, sample.Presentation, sample.Source));
        Assert.Throws<InvalidDataException>(() => new PreparedBattleMap(sample.Id, sample.TextureId, 2, 1,
            sample.Polygons, sample.Tiles.Take(2), sample.Palettes, sample.Presentation, sample.Source));
    }

    [Fact]
    public void Prepared_only_load_verifies_manifest_hashes_texture_indices_size_and_requested_identity()
    {
        string directory = Path.Combine(Path.GetTempPath(), "fft-prepared-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var map = Sample(); byte[] metadata = map.ToUtf8();
            byte[] texture = Enumerable.Range(0, PreparedBattleMap.TextureByteLength).Select(i => (byte)(i % 16)).ToArray();
            LooseAssetStore Store(byte[] mapBytes, byte[] textureBytes)
            {
                File.WriteAllBytes(Path.Combine(directory, "map.json"), mapBytes);
                File.WriteAllBytes(Path.Combine(directory, "texture.indices"), textureBytes);
                return new(directory, new PreparedAssetManifest([
                    new(map.Id, "map.json", mapBytes.Length, Convert.ToHexStringLower(SHA256.HashData(mapBytes))),
                    new(map.TextureId, "texture.indices", textureBytes.Length, Convert.ToHexStringLower(SHA256.HashData(textureBytes)))]));
            }
            var store = Store(metadata, texture);
            var loaded = PreparedBattleMap.Load(store, map.Id);
            Assert.Equal(metadata, loaded.Definition.ToUtf8()); Assert.Equal(texture, loaded.TextureIndices.ToArray());
            texture[0] = 15; Assert.Equal(0, loaded.TextureIndices[0]);
            File.AppendAllText(Path.Combine(directory, "map.json"), " ");
            Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Load(store, map.Id));
            store = Store(metadata, texture);
            // Same-size tampering must also fail the manifest hash check.
            texture[0] ^= 1; File.WriteAllBytes(Path.Combine(directory, "texture.indices"), texture);
            Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Load(store, map.Id));
            texture[0] = 16;
            Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Load(Store(metadata, texture), map.Id));
            Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Load(Store(metadata, new byte[1]), map.Id));
            Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Load(Store(metadata, new byte[PreparedBattleMap.TextureByteLength + 1]), map.Id));
            Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Load(Store(new byte[PreparedBattleMap.MaxMetadataBytes + 1], new byte[PreparedBattleMap.TextureByteLength]), map.Id));
            var root = JsonNode.Parse(metadata)!; root["id"] = "sample:map/other";
            Assert.Throws<InvalidDataException>(() => PreparedBattleMap.Load(Store(Encoding.UTF8.GetBytes(root.ToJsonString()), new byte[PreparedBattleMap.TextureByteLength]), map.Id));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static PreparedBattleMap Sample()
    {
        ImmutableArray<MapVector> vertices = [new(short.MinValue, -123, short.MaxValue), new(0, 0, 0), new(28, -12, 0), new(28, -12, 28)];
        ImmutableArray<MapVector> normals = [new(0, -4096, 0), new(0, -4096, 0), new(0, -4096, 0), new(0, -4096, 0)];
        ImmutableArray<MapUv> uvs = [new(0, 0), new(255, 0), new(0, 255), new(255, 255)];
        MapPolygon[] polygons = [
            new("textured-quad", 3, vertices, normals, uvs, 3, 15, 65535, new(1, 0, 1), [65535, 4, 3, 2, 1, 0], 0x100),
            new("untextured-triangle", 0, vertices.Take(3).ToImmutableArray(), [], [], null, null, 1, null, [65535, 0], null),
            new("textured-triangle", 1, vertices.Take(3).ToImmutableArray(), normals.Take(3).ToImmutableArray(), uvs.Take(3).ToImmutableArray(), 0, 0, 0, null, [1, 2, 3, 4, 5], 0xfffe),
            new("untextured-quad", 0, vertices, [], [], null, null, 0, null, [0, 1], null)];
        MapTile Tile(int height, bool varied = false) => new("base:terrain/ps1/1d", 29, height,
            varied ? 31 : 0, varied ? 7 : 0, varied ? 1 : 0, varied ? 2 : 0, varied ? 3 : 0, 0,
            varied ? 31 : 0, varied ? 7 : 0, Enumerable.Repeat((byte)255, 8).ToImmutableArray());
        return new(new("sample:map/synthetic"), new("sample:texture/synthetic"), 2, 1, polygons,
            [Tile(1), Tile(2, true), Tile(3), Tile(4)], Enumerable.Range(0, 256).Select(i => (ushort)(i * 257)),
            new(Enumerable.Range(0, 18).Select(i => (short)(i - 9)).ToImmutableArray(),
                Enumerable.Range(0, 9).Select(i => (byte)i).ToImmutableArray(),
                Enumerable.Repeat((byte)255, 640).ToImmutableArray(),
                Enumerable.Range(0, 256).Select(i => (ushort)i).ToImmutableArray(),
                Enumerable.Repeat(ushort.MaxValue, 256).ToImmutableArray()),
            new("SCUS-94221", new string('a', 40), new string('b', 64), new string('c', 64), new string('d', 64)));
    }
}
