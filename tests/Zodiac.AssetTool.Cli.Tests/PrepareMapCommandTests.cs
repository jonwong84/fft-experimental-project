using System.Collections.Immutable;
using System.Security.Cryptography;
using Zodiac.Content;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class PrepareMapCommandTests
{
    [Theory]
    [InlineData()]
    [InlineData("wrong-command")]
    [InlineData("prepare-map")]
    [InlineData("prepare-map", "--gns")]
    [InlineData("prepare-map", "--unknown", "x")]
    [InlineData("prepare-map", "--gns", "x", "--gns", "y")]
    public void Invalid_options_report_usage(params string[] args)
    {
        var result = Run(Path.GetTempPath(), args);
        Assert.Equal(2, result.Code); Assert.Contains("Usage:", result.Error);
    }

    [Theory]
    [InlineData(2)] [InlineData(4)] [InlineData(6)] [InlineData(8)]
    public void Every_input_and_output_must_stay_below_scratch(int argument)
    {
        string root = NewRoot(), source = Path.Combine(root, "scratch", "source");
        var args = Args(source, Path.Combine(root, "scratch", "output"));
        args[argument] = Path.Combine(root, "scratch-other", "outside");
        var result = Run(root, args);
        Assert.Equal(1, result.Code); Assert.Contains("below repository scratch", result.Error);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Existing_output_is_preserved_before_inputs_are_read()
    {
        string root = NewRoot(), scratch = Path.Combine(root, "scratch"), destination = Path.Combine(scratch, "output");
        Directory.CreateDirectory(destination);
        string sentinel = Path.Combine(destination, "keep.txt"); File.WriteAllText(sentinel, "keep");
        try
        {
            var result = Run(root, Args(Path.Combine(scratch, "missing"), destination));
            Assert.Equal(1, result.Code); Assert.Contains("already exists", result.Error);
            Assert.Equal("keep", File.ReadAllText(sentinel));
        }
        finally { File.Delete(sentinel); Directory.Delete(destination); Directory.Delete(scratch); Directory.Delete(root); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(568)]
    public void Missing_wrong_length_and_wrong_hash_inputs_leave_no_output(int length)
    {
        string root = NewRoot(), scratch = Path.Combine(root, "scratch"), input = Path.Combine(scratch, "source");
        Directory.CreateDirectory(scratch);
        try
        {
            if (length != 0) File.WriteAllBytes(input, new byte[length]);
            var result = Run(root, Args(input, Path.Combine(scratch, "output")));
            Assert.Equal(1, result.Code);
            if (length == 568) Assert.Contains("SHA-256 mismatch", result.Error);
            Assert.Empty(Directory.GetDirectories(scratch));
        }
        finally { File.Delete(input); Directory.Delete(scratch); Directory.Delete(root); }
    }

    [Fact]
    public void Export_and_prepared_only_load_preserve_native_material_order_and_normalized_two_layer_terrain()
    {
        // Invented records; native parsing and its fixed source stride have independent binary tests.
        ImmutableArray<OriginalMapVector> vertices = [new(112, -12, 84), new(112, -12, 56), new(84, -12, 84), new(84, -12, 56)];
        var normals = Enumerable.Repeat(new OriginalMapVector(0, -4096, 0), 4).ToImmutableArray();
        OriginalMapTile Tile(byte height, byte flags = 0) => new([0xdd, 0x45, height, 0xff, 0x39, 0xff, flags, 0xa5]);
        var decoded = new DecodedBattleMap(2, 1, [
            new("textured-quad", 217, vertices, normals, [0xe358, 0x7808, 0xd058, 14, 0xe36b, 0xd06b], 0x8000, 0x100),
            new("textured-triangle", 0, vertices.Take(3).ToImmutableArray(), normals.Take(3).ToImmutableArray(), [1, 0x7800, 2, 12, 3], 1, 0xfffe),
            new("untextured-quad", 1, vertices, [], [0x1234, 0xabcd], 2, null),
            new("untextured-triangle", 1, vertices.Take(3).ToImmutableArray(), [], [0x5678, 0xffff], 3, null)],
            [Tile(1), Tile(2, 0xab), Tile(8), Tile(9, 1)],
            Enumerable.Range(0, 256).Select(i => (ushort)(i * 257)).ToImmutableArray(),
            new(Enumerable.Range(0, 18).Select(i => (short)(i - 9)).ToImmutableArray(),
                Enumerable.Range(0, 9).Select(i => (byte)i).ToImmutableArray(),
                Enumerable.Repeat((byte)0xa5, 640).ToImmutableArray(),
                Enumerable.Range(0, 256).Select(i => (ushort)(65535 - i)).ToImmutableArray(),
                Enumerable.Repeat((ushort)0x8000, 256).ToImmutableArray()));
        var source = new MapSource("SCUS-94221", new('a', 40), new('b', 64), new('c', 64), new('d', 64));
        var definition = MapContentExporter.Create(decoded, new("sample:map"), new("sample:indices"), source);
        byte[] json = definition.ToUtf8();
        Assert.Equal(json, MapContentExporter.Create(decoded, definition.Id, definition.TextureId, source).ToUtf8());
        var indices = OriginalBattleMap.DecodeTexture(Enumerable.Repeat((byte)0xe1, 131072).ToArray()).ToArray();
        string root = NewRoot(); Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "map.json"), json);
            File.WriteAllBytes(Path.Combine(root, "texture.indices"), indices);
            new PreparedAssetManifest([Entry(definition.Id, "map.json", json), Entry(definition.TextureId, "texture.indices", indices)])
                .SaveToFile(Path.Combine(root, PreparedAssetManifest.FileName));
            var loaded = PreparedBattleMap.Load(LooseAssetStore.Load(root), definition.Id);
            var quad = loaded.Definition.Polygons[0];
            Assert.Equal(new MapVector(112, -12, 84), quad.Vertices[0]);
            Assert.Equal(new MapVector(84, -12, 56), quad.Vertices[3]);
            Assert.All(quad.Normals, n => Assert.Equal(new MapVector(0, -4096, 0), n));
            Assert.Equal(new[] { new MapUv(88, 227), new MapUv(88, 208), new MapUv(107, 227), new MapUv(107, 208) }, quad.Uvs);
            Assert.Equal(2, quad.TexturePage); Assert.Equal(8, quad.PaletteBank);
            Assert.Equal((ushort)0x8000, quad.Flags); Assert.Equal(new MapTerrainReference(1, 0, 0), quad.Terrain);
            Assert.Equal(decoded.Polygons[0].Material.ToArray(), quad.SourceMaterial.ToArray());
            Assert.Null(loaded.Definition.Polygons[1].Terrain);
            Assert.Equal((ushort?)0xfffe, loaded.Definition.Polygons[1].SourceTerrainTag);
            Assert.Empty(loaded.Definition.Polygons[2].Uvs);
            Assert.Null(loaded.Definition.Polygons[2].PaletteBank);
            Assert.Equal((ushort)0xffff, loaded.Definition.Polygons[3].SourceMaterial[1]);
            var map = loaded.Definition.ToBattleMap();
            Assert.True(map.TryGetTile(new(1, 0, 0), out var lower));
            Assert.True(map.TryGetTile(new(1, 0, 1), out var upper));
            Assert.Equal(2, lower.Height); Assert.Equal(9, upper.Height);
            Assert.Equal("base:terrain/ps1/1d", lower.Surface.Value);
            Assert.Equal(31, lower.HalfHeight); Assert.Equal(7, lower.WaterDepth); Assert.Equal(31, lower.CeilingDepth);
            Assert.Equal(new TileSlope(1, 2, 3, 0), lower.Slope);
            Assert.True(lower.IsBlocked); Assert.True(lower.IsUntargetable); Assert.True(lower.CannotStop);
            Assert.True(upper.IsBlocked); Assert.False(upper.CannotStop);
            Assert.Equal(decoded.Tiles[1].Bytes.ToArray(), loaded.Definition.Tiles[1].SourceBytes.ToArray());
            Assert.Equal(decoded.Palettes.ToArray(), loaded.Definition.Palettes.ToArray()); // Includes original STP; no sprite palette fixup.
            Assert.Equal(decoded.Presentation.TextureAnimations.ToArray(), loaded.Definition.Presentation.TextureAnimations.ToArray());
            Assert.Equal(source, loaded.Definition.Source);
            Assert.Equal(json, loaded.Definition.ToUtf8()); Assert.Equal(indices, loaded.TextureIndices.ToArray());
            Assert.Equal(new byte[] { 1, 14 }, loaded.TextureIndices.Take(2));
            Assert.Equal(3, Directory.GetFiles(root).Length); // Prepared-only root, no BIN, mesh, GNS or reference checkout.
        }
        finally
        {
            foreach (string file in new[] { "map.json", "texture.indices", PreparedAssetManifest.FileName }) File.Delete(Path.Combine(root, file));
            Directory.Delete(root);
        }
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "fft-map-cli-" + Guid.NewGuid().ToString("N"));
    private static PreparedAssetEntry Entry(AssetId id, string path, byte[] bytes) =>
        new(id, path, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    private static string[] Args(string input, string output) => ["prepare-map", "--gns", input, "--mesh", input, "--texture", input, "--output", output];
    private static (int Code, string Error) Run(string root, string[] args)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        int code = new PrepareMapCommand(root).Run(args, output, error);
        return (code, error.ToString());
    }
}
