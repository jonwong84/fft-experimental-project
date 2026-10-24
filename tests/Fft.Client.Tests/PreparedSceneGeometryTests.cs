using System.Collections.Immutable;
using Fft.Client.Presentation;
using Fft.Content;
using Fft.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Fft.Client.Tests;

public sealed class PreparedSceneGeometryTests
{
    [Fact]
    public void Camera_handedness_preserves_the_native_yaw_horizontal_axes_after_source_Y_reflection()
    {
        // Source-derived RotMatrix row 0 at yaw 0xe00, roll 0: (cos(yaw), 0, sin(yaw)).
        // Reference 11464943bbdbd2a0af022d90cb7f10455cf911df; authored pitch/projection differ.
        var center = PreparedSceneGeometry.Project(PreparedSceneGeometry.Target);
        var x = PreparedSceneGeometry.Project(PreparedSceneGeometry.Target + Vector3.UnitX);
        var z = PreparedSceneGeometry.Project(PreparedSceneGeometry.Target + Vector3.UnitZ);
        Near(480, center.X); Near(320, center.Y);
        Near(MathF.Sqrt(0.5f) * 960 / 630, x.X - center.X);
        Near(-MathF.Sqrt(0.5f) * 960 / 630, z.X - center.X);
    }

    [Fact]
    public void Strip_quad_covers_the_rectangle_with_consistent_winding_and_page_texel_centers()
    {
        var map = Map(); var pose = Pose();
        var scene = PreparedSceneGeometry.Create(map, pose);
        var draw = Assert.Single(scene.MapDraws);
        Assert.Equal(2, scene.HiddenPolygons);
        Assert.Equal("textured-quad", draw.Kind); Assert.Equal(2, draw.PaletteBank);
        var v = draw.Vertices;
        Assert.Equal(6, v.Length);
        Assert.Equal(new Vector3(-14, 12, 0), v[0].Position);
        Assert.Equal(new Vector3(14, 12, 0), v[1].Position);
        Assert.Equal(new Vector3(-14, 12, 28), v[2].Position);
        Assert.Equal(v[2], v[3]); Assert.Equal(v[1], v[4]);
        Assert.Equal(new Vector3(14, 12, 28), v[5].Position);
        var firstNormal = Vector3.Cross(v[1].Position - v[0].Position, v[2].Position - v[0].Position);
        var secondNormal = Vector3.Cross(v[4].Position - v[3].Position, v[5].Position - v[3].Position);
        Assert.True(Vector3.Dot(firstNormal, secondNormal) > 0);
        Near(28 * 28, (firstNormal.Length() + secondNormal.Length()) / 2);
        Assert.Equal(new Vector2(512.5f / 1024, 0.5f / 256), v[0].TextureCoordinate);
        Assert.Equal(new Vector2(767.5f / 1024, 0.5f / 256), v[1].TextureCoordinate);
        Assert.Equal(new Vector2(512.5f / 1024, 255.5f / 256), v[2].TextureCoordinate);
        Assert.Equal(new Vector2(767.5f / 1024, 255.5f / 256), v[5].TextureCoordinate);
        Assert.All(v, vertex => Assert.Equal(Color.White, vertex.Color));
    }

    [Fact]
    public void Palette_lookup_is_per_bank_and_cutout_depends_on_the_full_word_not_index_or_STP()
    {
        var map = Map();
        var first = PreparedSceneGeometry.PaletteTexture(map, 0);
        var second = PreparedSceneGeometry.PaletteTexture(map, 1);
        Assert.Equal(new Color(255, 0, 0, 255), first[0]); // Index zero, nonzero color.
        Assert.Equal(Color.Transparent, first[1]); // Nonzero index, word zero.
        Assert.Equal(new Color(0, 0, 0, 255), first[2]); // STP black remains opaque.
        Assert.Equal(new Color(0, 255, 0, 255), second[0]);
        Assert.Equal(new Color(0, 0, 255, 255), second[1]);
        Assert.Equal(Color.White, second[2]);
        Assert.Equal(Color.Transparent, PreparedSceneGeometry.PaletteColor(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PreparedSceneGeometry.PaletteTexture(map, 16));
        Assert.Throws<InvalidDataException>(() => PreparedSceneGeometry.PaletteTexture(map with { TextureIndices = [0] }, 0));
        var invalid = map.TextureIndices.ToArray(); invalid[0] = 16;
        Assert.Throws<InvalidDataException>(() => PreparedSceneGeometry.PaletteTexture(map with { TextureIndices = invalid.ToImmutableArray() }, 0));
    }

    [Fact]
    public void Every_five_bit_channel_expands_by_bit_replication_without_STP_affecting_color()
    {
        for (int value = 0; value < 32; value++)
        {
            int expected = (value << 3) | (value >> 2);
            Assert.Equal(new Color(expected, 0, 0, 255), PreparedSceneGeometry.PaletteColor((ushort)(0x8000 | value)));
            Assert.Equal(new Color(0, expected, 0, 255), PreparedSceneGeometry.PaletteColor((ushort)(0x8000 | (value << 5))));
            Assert.Equal(new Color(0, 0, expected, 255), PreparedSceneGeometry.PaletteColor((ushort)(0x8000 | (value << 10))));
        }
        Assert.Equal(PreparedSceneGeometry.PaletteColor(0x1234), PreparedSceneGeometry.PaletteColor(0x9234));
    }

    [Theory]
    [InlineData(0x000, 0x0200)]
    [InlineData(0x001, 0x0004)]
    [InlineData(0x1ff, 0x0004)]
    [InlineData(0x200, 0x0404)]
    [InlineData(0x201, 0x0008)]
    [InlineData(0x5ff, 0x0010)]
    [InlineData(0x600, 0x0810)]
    [InlineData(0x601, 0x0020)]
    [InlineData(0xa00, 0x1040)]
    [InlineData(0xe00, 0x2100)]
    [InlineData(0xe01, 0x0200)]
    [InlineData(0xfff, 0x0200)]
    public void Pinned_visibility_masks_cover_wrap_bands_and_resting_diagonals(int yaw, int mask)
    {
        Assert.True(PreparedSceneGeometry.Visible(0x8001, yaw)); // Lighting is independent.
        for (int bit = 0; bit < 16; bit++)
            Assert.Equal((mask & (1 << bit)) == 0, PreparedSceneGeometry.Visible((ushort)(1 << bit), yaw));
    }

    [Fact]
    public void Visibility_rejects_non_native_yaw_domain()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PreparedSceneGeometry.Visible(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PreparedSceneGeometry.Visible(0, 4096));
    }

    [Fact]
    public void Anchor_uses_normalized_Core_height_and_native_layer_without_source_byte_reinterpretation()
    {
        var map = Map().Definition;
        var terrain = map.ToBattleMap();
        Assert.Equal(new Vector3(126, 84, 42), PreparedSceneGeometry.FlatAnchor(terrain, new(4, 1, 0)));
        Assert.Equal(new Vector3(126, 108, 42), PreparedSceneGeometry.FlatAnchor(terrain, new(4, 1, 1)));
        Assert.Equal(new Vector3(98, 84, 70), PreparedSceneGeometry.Create(new(map, new byte[PreparedBattleMap.TextureByteLength].ToImmutableArray()), Pose()).UnitAnchor);
        Assert.Throws<InvalidDataException>(() => PreparedSceneGeometry.FlatAnchor(terrain, new(5, 1, 0)));
    }

    [Theory]
    [InlineData("slope")] [InlineData("half-height")] [InlineData("water")]
    [InlineData("blocked")] [InlineData("untargetable")] [InlineData("cannot-stop")]
    public void Anchor_rejects_untraced_placement_fields(string field)
    {
        var map = Map().Definition;
        var tiles = map.Tiles.ToArray();
        var tile = tiles[9]; // Layer 0, x4,y1 in width5.
        tiles[9] = field switch
        {
            "slope" => tile with { SlopeEast = 1 },
            "half-height" => tile with { HalfHeight = 1 },
            "water" => tile with { WaterDepth = 1 },
            "blocked" => tile with { MovementFlags = 1 },
            "untargetable" => tile with { MovementFlags = 2 },
            _ => tile with { MovementFlags = 4 },
        };
        var changed = new PreparedBattleMap(map.Id, map.TextureId, map.Width, map.Depth, map.Polygons,
            tiles, map.Palettes, map.Presentation, map.Source);
        Assert.Throws<NotSupportedException>(() => PreparedSceneGeometry.FlatAnchor(changed.ToBattleMap(), new(4, 1, 0)));
    }

    [Fact]
    public void Sprite_parts_preserve_prepared_order_signed_origin_offsets_independent_flips_and_constant_depth()
    {
        var scene = PreparedSceneGeometry.Create(Map(), Pose());
        Assert.Equal(new[] { 3, 2, 1, 0 }, scene.PoseDraws.Select(draw => draw.SourceIndex));
        var anchor = PreparedSceneGeometry.Project(scene.UnitAnchor);
        foreach (var draw in scene.PoseDraws)
        {
            var topLeft = PreparedSceneGeometry.Project(draw.Vertices[0].Position);
            var bottomRight = PreparedSceneGeometry.Project(draw.Vertices[5].Position);
            Near(-16 * 960f / 630, topLeft.X - anchor.X);
            Near(-35 * 640f / 420, topLeft.Y - anchor.Y);
            Near(8 * 960f / 630, bottomRight.X - topLeft.X);
            Near(8 * 640f / 420, bottomRight.Y - topLeft.Y);
            Assert.All(draw.Vertices, vertex => Near(anchor.Z, PreparedSceneGeometry.Project(vertex.Position).Z));
        }
        var ordinary = scene.PoseDraws[0].Vertices;
        var horizontal = scene.PoseDraws[1].Vertices;
        var vertical = scene.PoseDraws[2].Vertices;
        var both = scene.PoseDraws[3].Vertices;
        Assert.Equal(new Vector2(8 / 256f, 16 / 256f), ordinary[0].TextureCoordinate);
        Assert.Equal(new Vector2(16 / 256f, 16 / 256f), horizontal[0].TextureCoordinate);
        Assert.Equal(new Vector2(8 / 256f, 24 / 256f), vertical[0].TextureCoordinate);
        Assert.Equal(new Vector2(16 / 256f, 24 / 256f), both[0].TextureCoordinate);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(ordinary[i].Position, horizontal[i].Position);
            Assert.Equal(ordinary[i].Position, vertical[i].Position);
            Assert.Equal(ordinary[i].Position, both[i].Position);
        }
    }

    [Fact]
    public void Repeated_geometry_and_palette_generation_is_deterministic_and_preserves_prepared_inputs()
    {
        var map = Map(); var pose = Pose();
        byte[] mapBytes = map.Definition.ToUtf8(), poseBytes = pose.Definition.ToUtf8();
        var indices = map.TextureIndices.ToArray(); var rgba = pose.Rgba.ToArray();
        var first = PreparedSceneGeometry.Create(map, pose);
        var second = PreparedSceneGeometry.Create(map, pose);
        Assert.Equal(first.UnitAnchor, second.UnitAnchor); Assert.Equal(first.HiddenPolygons, second.HiddenPolygons);
        Assert.Equal(first.MapDraws.SelectMany(d => d.Vertices), second.MapDraws.SelectMany(d => d.Vertices));
        Assert.Equal(first.PoseDraws.SelectMany(d => d.Vertices), second.PoseDraws.SelectMany(d => d.Vertices));
        var colors = PreparedSceneGeometry.PaletteTexture(map, 0); colors[0] = Color.Purple;
        Assert.NotEqual(colors[0], PreparedSceneGeometry.PaletteTexture(map, 0)[0]);
        Assert.Equal(mapBytes, map.Definition.ToUtf8()); Assert.Equal(poseBytes, pose.Definition.ToUtf8());
        Assert.Equal(indices, map.TextureIndices.ToArray()); Assert.Equal(rgba, pose.Rgba.ToArray());
    }

    [Fact]
    public void Mixed_primitive_kinds_preserve_input_order_banks_and_native_untextured_initialization()
    {
        var basis = Map(); var definition = basis.Definition; var quad = definition.Polygons[0];
        var triangle = new MapPolygon("textured-triangle", 7, quad.Vertices.Take(3).ToImmutableArray(),
            quad.Normals.Take(3).ToImmutableArray(), quad.Uvs.Take(3).ToImmutableArray(), 1, 9, 0, null,
            [0, 9, 0, 13, 0], 0xfffe);
        var untexturedTriangle = new MapPolygon("untextured-triangle", 3, triangle.Vertices, [], [],
            null, null, 0, null, [0xffff, 0xffff], null);
        var untexturedQuad = new MapPolygon("untextured-quad", 4, quad.Vertices, [], [],
            null, null, 0, null, [0xffff, 0xffff], null);
        var mixed = new PreparedBattleMap(definition.Id, definition.TextureId, definition.Width, definition.Depth,
            [untexturedTriangle, quad, untexturedQuad, triangle], definition.Tiles, definition.Palettes,
            definition.Presentation, definition.Source);
        var scene = PreparedSceneGeometry.Create(new(mixed, basis.TextureIndices), Pose());
        Assert.Equal(new[] { "untextured-triangle", "textured-quad", "untextured-quad", "textured-triangle" }, scene.MapDraws.Select(draw => draw.Kind));
        Assert.Equal(new[] { 3, 6, 6, 3 }, scene.MapDraws.Select(draw => draw.Vertices.Length));
        Assert.Equal(new[] { -1, 2, -1, 9 }, scene.MapDraws.Select(draw => draw.PaletteBank));
        foreach (int index in new[] { 0, 2 })
            Assert.All(scene.MapDraws[index].Vertices, vertex =>
            {
                Assert.Equal(new Color(0, 0, 1), vertex.Color);
                Assert.Equal(Vector2.Zero, vertex.TextureCoordinate);
            });
        var strip = scene.MapDraws[2].Vertices;
        Assert.Equal(strip[2], strip[3]); Assert.Equal(strip[1], strip[4]);
        Assert.Equal(PreparedSceneGeometry.World(quad.Vertices[3]), strip[5].Position);
        var textured = scene.MapDraws[3].Vertices;
        Assert.All(textured, vertex => Assert.Equal(Color.White, vertex.Color));
        Assert.Equal(new Vector2(256.5f / 1024, 0.5f / 256), textured[0].TextureCoordinate);
        Assert.Equal(PreparedSceneGeometry.World(triangle.Vertices[2]), textured[2].Position);
    }

    [Fact]
    public void Source_height_one_checkpoint_anchor_projects_to_its_native_strip_quad_center()
    {
        // Source-derived numeric relation at decomp 11464943bbdbd2a0af022d90cb7f10455cf911df:
        // terrain_projection.c flat tile center=(28*x+14,-12*height,28*y+14).
        // Authored camera projection is tested here, not original screen-coordinate equivalence.
        var basis = Map(); var definition = basis.Definition;
        var tiles = definition.Tiles.ToArray(); tiles[13] = tiles[13] with { Height = 1 };
        var quad = definition.Polygons[0] with
        {
            Vertices = [new(112, -12, 84), new(112, -12, 56), new(84, -12, 84), new(84, -12, 56)],
        };
        var checkpoint = new PreparedBattleMap(definition.Id, definition.TextureId, definition.Width, definition.Depth,
            [quad], tiles, definition.Palettes, definition.Presentation, definition.Source);
        var scene = PreparedSceneGeometry.Create(new(checkpoint, basis.TextureIndices), Pose());
        Assert.Equal(new Vector3(98, 12, 70), scene.UnitAnchor);
        var vertices = Assert.Single(scene.MapDraws).Vertices;
        var corners = new[] { vertices[0].Position, vertices[1].Position, vertices[2].Position, vertices[5].Position };
        Assert.Equal(new[] { new Vector3(112, 12, 84), new Vector3(112, 12, 56), new Vector3(84, 12, 84), new Vector3(84, 12, 56) }, corners);
        Assert.Equal(scene.UnitAnchor, corners.Aggregate(Vector3.Zero, (sum, corner) => sum + corner) / 4);
        var projectedAnchor = PreparedSceneGeometry.Project(scene.UnitAnchor);
        var projectedCenter = corners.Select(PreparedSceneGeometry.Project).Aggregate(Vector3.Zero, (sum, corner) => sum + corner) / 4;
        Near(projectedAnchor.X, projectedCenter.X); Near(projectedAnchor.Y, projectedCenter.Y); Near(projectedAnchor.Z, projectedCenter.Z);
    }

    private static void Near(float expected, float actual) => Assert.InRange(actual, expected - 0.0002f, expected + 0.0002f);

    private static LoadedBattleMap Map()
    {
        ImmutableArray<MapVector> vertices = [new(-14, -12, 0), new(14, -12, 0), new(-14, -12, 28), new(14, -12, 28)];
        var quad = new MapPolygon("textured-quad", 0, vertices,
            Enumerable.Repeat(new MapVector(0, -4096, 0), 4).ToImmutableArray(),
            [new(0, 0), new(255, 0), new(0, 255), new(255, 255)], 2, 2, 0x8000, null, [0, 0, 0, 14, 0, 0], 0xfffe);
        var hiddenBand = quad with { SourceIndex = 1, Flags = 0x100 };
        var hiddenRest = quad with { SourceIndex = 2, Flags = 0x2000 };
        var palettes = new ushort[256];
        palettes[0] = 0x001f; palettes[1] = 0; palettes[2] = 0x8000;
        palettes[16] = 0x03e0; palettes[17] = 0x7c00; palettes[18] = 0xffff;
        var tiles = Enumerable.Range(0, 30).Select(i => new MapTile("base:terrain/ps1/1d", 29,
            i >= 15 ? 9 : i is 9 or 13 ? 7 : 1, 0, 0, 0, 0, 0, 0, 0, 0,
            Enumerable.Repeat((byte)255, 8).ToImmutableArray())).ToArray();
        var definition = new PreparedBattleMap(new("sample:map/scene"), new("sample:texture/map"), 5, 3,
            [quad, hiddenBand, hiddenRest], tiles, palettes,
            new(new short[18].ToImmutableArray(), new byte[9].ToImmutableArray(), new byte[640].ToImmutableArray(),
                new ushort[256].ToImmutableArray(), new ushort[256].ToImmutableArray()),
            new("SCUS-94221", new string('a', 40), new string('b', 64), new string('c', 64), new string('d', 64)));
        var indices = new byte[PreparedBattleMap.TextureByteLength]; indices[1] = 1; indices[2] = 2;
        return new(definition, indices.ToImmutableArray());
    }

    private static LoadedSpritePose Pose()
    {
        var parts = Enumerable.Range(0, 4).Select(i => new SpritePosePart(3 - i, -16, -35, 8, 16, 8, 8,
            (i & 1) != 0, (i & 2) != 0));
        var definition = new PreparedSpritePose(new("sample:pose/scene"), new("sample:texture/pose"), 6, 9, 6, 0,
            new ushort[16], parts, new("SCUS-94221", new string('a', 40), new string('b', 64), new string('c', 64), new string('d', 64)));
        return new(definition, new byte[256 * 256 * 4].ToImmutableArray());
    }
}
