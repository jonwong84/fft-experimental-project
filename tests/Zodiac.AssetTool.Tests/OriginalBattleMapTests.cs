using System.Buffers.Binary;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class OriginalBattleMapTests
{
    [Fact]
    public void Mesh_sections_preserve_four_polygon_kinds_signed_vectors_materials_and_tags()
    {
        byte[] bytes = Mesh();
        var map = OriginalBattleMap.Parse(bytes);
        Assert.Equal(new[] { "textured-triangle", "textured-quad", "untextured-triangle", "untextured-quad" }, map.Polygons.Select(p => p.Kind));
        Assert.Equal(new OriginalMapVector(-300, -12, 28), map.Polygons[0].Vertices[0]);
        Assert.Equal(new OriginalMapVector(0, -4096, 0), map.Polygons[1].Normals[3]);
        Assert.Equal(new ushort[] { 0x11, 0x7808, 0x22, 14, 0x33, 0x44 }, map.Polygons[1].Material);
        Assert.Equal((ushort?)0xfffe, map.Polygons[0].TerrainTag);
        Assert.Equal((ushort?)0x0102, map.Polygons[1].TerrainTag);
        Assert.Equal(0x8004, map.Polygons[1].Flags);
        Assert.Null(map.Polygons[2].TerrainTag); Assert.Empty(map.Polygons[2].Normals);
        Assert.Equal(new ushort[] { 0x1234, 0xabcd }, map.Polygons[2].Material);
    }

    [Fact]
    public void Terrain_becomes_dense_without_reading_native_layer_padding_or_mutating_source_data()
    {
        byte[] bytes = Mesh();
        var map = OriginalBattleMap.Parse(bytes);
        int terrain = Offset(bytes, 0x1a);
        bytes[terrain + 4] = 99;
        Assert.Equal(2, map.Width); Assert.Equal(2, map.Depth); Assert.Equal(8, map.Tiles.Length);
        Assert.Equal(new byte[] { 0x9d, 0x55, 1, 0x71, 0x99, 0xa7, 0xc3, 0x71 }, map.Tiles[0].Bytes);
        Assert.Equal(4, map.Tiles[3].Bytes[2]);
        Assert.Equal(8, map.Tiles[4].Bytes[2]); // source record256, not source record4
        Assert.Equal(11, map.Tiles[7].Bytes[2]);
        Assert.Equal(18, map.Presentation.LightMatrices.Length);
        Assert.Equal(640, map.Presentation.TextureAnimations.Length);
        Assert.Equal(256, map.Palettes.Length);
    }

    [Theory]
    [InlineData("header")] [InlineData("offset-range")] [InlineData("offset-negative")]
    [InlineData("duplicate-offset")] [InlineData("alignment")] [InlineData("missing-chunk")]
    [InlineData("count-budget")] [InlineData("geometry-span")] [InlineData("truncated-flags")]
    [InlineData("dimensions")] [InlineData("zero-width")] [InlineData("terrain-tag")]
    [InlineData("overlapping-chunk")]
    public void Invalid_mesh_offsets_counts_and_records_fail_before_becoming_prepared_data(string kind)
    {
        byte[] bytes = Mesh();
        switch (kind)
        {
            case "header": bytes = bytes[..195]; break;
            case "offset-range": W32(bytes, 0x10 * 4, bytes.Length); break;
            case "offset-negative": W32(bytes, 0x10 * 4, -1); break;
            case "duplicate-offset": W32(bytes, 0x11 * 4, Offset(bytes, 0x10)); break;
            case "alignment": W32(bytes, 0x11 * 4, Offset(bytes, 0x11) + 1); break;
            case "missing-chunk": W32(bytes, 0x11 * 4, 0); break;
            case "count-budget": W16(bytes, 196, 513); break;
            case "geometry-span": W16(bytes, 196, 2); break;
            case "truncated-flags": bytes = bytes[..^1]; break;
            case "dimensions": bytes[Offset(bytes, 0x1a)] = 255; break;
            case "zero-width": bytes[Offset(bytes, 0x1a)] = 0; break;
            case "terrain-tag": W16(bytes, 196 + 166, 0x0200); break;
            case "overlapping-chunk": W32(bytes, 0x19 * 4, Offset(bytes, 0x11) + 4); break;
        }
        Assert.Throws<InvalidDataException>(() => OriginalBattleMap.Parse(bytes));
    }

    [Theory]
    [InlineData("resource")] [InlineData("page")] [InlineData("palette")]
    public void Untraced_resource_pages_and_palette_ranges_are_explicitly_unsupported(string kind)
    {
        byte[] bytes = Mesh();
        if (kind == "resource") W32(bytes, 0x24 * 4, 200);
        if (kind == "page") W16(bytes, 196 + 134 + 6, 28);
        if (kind == "palette") W16(bytes, 196 + 134 + 2, 0x7810);
        Assert.Throws<NotSupportedException>(() => OriginalBattleMap.Parse(bytes));
    }

    [Fact]
    public void Texture_quarters_are_reassembled_by_page_and_row_with_low_nibble_first()
    {
        var packed = new byte[131072];
        for (int page = 0; page < 4; page++) packed[page * 0x8000] = (byte)(0x21 + page * 0x22);
        packed[128] = 0xba; packed[^1] = 0x23;
        var result = OriginalBattleMap.DecodeTexture(packed);
        Assert.Equal(new byte[] { 1, 2 }, result.Take(2));
        Assert.Equal(new byte[] { 3, 4 }, result.Skip(256).Take(2));
        Assert.Equal(new byte[] { 5, 6 }, result.Skip(512).Take(2));
        Assert.Equal(new byte[] { 7, 8 }, result.Skip(768).Take(2));
        Assert.Equal(new byte[] { 10, 11 }, result.Skip(1024).Take(2));
        Assert.Equal(new byte[] { 3, 2 }, result.TakeLast(2));
        Assert.Throws<InvalidDataException>(() => OriginalBattleMap.DecodeTexture(packed.AsSpan(1)));
    }

    [Fact]
    public void Gns_selects_default_files_and_handles_separator_and_final_command_at_eof()
    {
        var selection = MapGnsSelection.ReadDayDry(Gns());
        Assert.Equal(new MapFileSelection(12658, 131072, 12722, 43008), selection);
        var terminated = Gns().Concat(new byte[20]).ToArray();
        Assert.Equal(selection, MapGnsSelection.ReadDayDry(terminated));
    }

    [Theory]
    [InlineData("short-row")] [InlineData("no-separator")] [InlineData("stride")]
    [InlineData("partial-command")] [InlineData("extent")]
    [InlineData("marker")] [InlineData("tail")]
    public void Gns_malformed_rows_commands_and_extents_are_rejected(string kind)
    {
        byte[] bytes = Gns();
        switch (kind)
        {
            case "short-row": bytes = bytes[..19]; break;
            case "no-separator": bytes = bytes[..80]; break;
            case "stride": bytes[108] = 0; break;
            case "partial-command": bytes = bytes[..^1]; break;
            case "extent": W32(bytes, 12, 1); break;
            case "marker": bytes[19] = 0; break;
            case "tail": bytes = bytes.Concat(new byte[21]).ToArray(); bytes[^1] = 1; break;
        }
        Assert.Throws<InvalidDataException>(() => MapGnsSelection.ReadDayDry(bytes));
    }

    [Fact]
    public void Gns_rejects_ambiguous_default_selection_and_scene_changes_outside_the_profile()
    {
        var bytes = Gns(); W16(bytes, 42, 0);
        Assert.Throws<NotSupportedException>(() => MapGnsSelection.ReadDayDry(bytes));
        bytes = Gns(); bytes[105] = 0x84;
        Assert.Throws<NotSupportedException>(() => MapGnsSelection.ReadDayDry(bytes));
    }

    private static int Offset(byte[] bytes, int id) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(id * 4));
    private static void W16(byte[] bytes, int at, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), unchecked((ushort)value));
    private static void W32(byte[] bytes, int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at), value);

    private static byte[] Mesh()
    {
        int[] ids = [0x10, 0x11, 0x19, 0x1a, 0x1b, 0x1c, 0x1f, 0x2c];
        int[] lengths = [168, 512, 48, 4100, 640, 512, 512, 4096];
        var bytes = new byte[196 + lengths.Sum()]; int at = 196;
        for (int i = 0; i < ids.Length; i++) { W32(bytes, ids[i] * 4, at); at += lengths[i]; }
        for (int i = 0; i < 4; i++) W16(bytes, 196 + i * 2, 1);
        at = 204;
        foreach (int vertices in new[] { 3, 4, 3, 4 })
        for (int v = 0; v < vertices; v++) { W16(bytes, at, -300 + v); W16(bytes, at + 2, -12); W16(bytes, at + 4, 28); at += 6; }
        for (int v = 0; v < 7; v++) { W16(bytes, at, 0); W16(bytes, at + 2, -4096); W16(bytes, at + 4, 0); at += 6; }
        foreach (int word in new[] { 0x11, 0x7808, 0x22, 14, 0x33, 0x11, 0x7808, 0x22, 14, 0x33, 0x44, 0x1234, 0xabcd, 0x5678, 0x9abc, 0xfffe, 0x0102 })
        { W16(bytes, at, word); at += 2; }
        Assert.Equal(364, at);
        int terrain = Offset(bytes, 0x1a); bytes[terrain] = 2; bytes[terrain + 1] = 2;
        foreach (int layer in new[] { 0, 1 })
        for (int i = 0; i < 4; i++)
        {
            byte[] tile = [0x9d, 0x55, (byte)(1 + layer * 7 + i), 0x71, 0x99, 0xa7, 0xc3, 0x71];
            tile.CopyTo(bytes, terrain + 2 + (layer * 256 + i) * 8);
        }
        int flags = Offset(bytes, 0x2c);
        foreach (int offset in new[] { 0x380, 0x780, 0xd80, 0xe00 }) W16(bytes, flags + offset, 0x8004);
        return bytes;
    }

    private static byte[] Gns()
    {
        var bytes = new byte[168];
        for (int row = 0; row < 4; row++)
        {
            int at = row * 20; W16(bytes, at, 0x22); W16(bytes, at + 2, row < 2 ? 0 : 1);
            bytes[at + 4] = 1; bytes[at + 5] = new byte[] { 0x17, 0x2e, 0x30, 0x31 }[row];
            W16(bytes, at + 6, 0x3333); W32(bytes, at + 8, row == 0 ? 12658 : 12722);
            W32(bytes, at + 12, row == 0 ? 131072 : 43008);
            new byte[] { 0x55, 0x66, 0x77, 0x88 }.CopyTo(bytes, at + 16);
        }
        bytes[85] = 0x80; bytes[88] = 255; // separator payload is deliberately not a valid command stride
        bytes[105] = 0x85; bytes[108] = 20;
        bytes[125] = 0x88; bytes[128] = 20;
        bytes[145] = 0x8a; bytes[148] = 28;
        return bytes;
    }
}
