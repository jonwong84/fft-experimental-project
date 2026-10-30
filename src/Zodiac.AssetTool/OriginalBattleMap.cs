using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Zodiac.AssetTool;

public readonly record struct OriginalMapVector(short X, short Y, short Z);
public sealed record OriginalMapPolygon(string Kind, int SourceIndex, ImmutableArray<OriginalMapVector> Vertices,
    ImmutableArray<OriginalMapVector> Normals, ImmutableArray<ushort> Material, ushort Flags, ushort? TerrainTag);
public sealed record OriginalMapTile(ImmutableArray<byte> Bytes);
public sealed record OriginalMapPresentation(ImmutableArray<short> LightMatrices, ImmutableArray<byte> AmbientAndGradient,
    ImmutableArray<byte> TextureAnimations, ImmutableArray<ushort> PaletteAnimationFrames, ImmutableArray<ushort> MarkedTilePalettes);
public sealed record DecodedBattleMap(int Width, int Depth, ImmutableArray<OriginalMapPolygon> Polygons,
    ImmutableArray<OriginalMapTile> Tiles, ImmutableArray<ushort> Palettes, OriginalMapPresentation Presentation);

/// <summary>Bounded primary mesh/terrain decoder. No renderer, C invocation or simulation transition.
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; docs/architecture/zod-029-map003-source-map.md.</summary>
public static class OriginalBattleMap
{
    private static readonly int[] ResourceIds = [0x10, 0x11, 0x19, 0x1a, 0x1b, 0x1c, 0x1f, 0x2c];

    public static DecodedBattleMap Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 196 or > 1048576) throw new InvalidDataException("Invalid bounded mesh file length.");
        var offsets = new SortedDictionary<int, int>();
        for (int id = 0; id < 49; id++)
        {
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(id * 4)..]);
            if (raw == 0) continue;
            if (!ResourceIds.Contains(id)) throw new NotSupportedException($"Unsupported mesh resource {id:x2}.");
            if (raw < 196 || raw >= bytes.Length || raw % 4 != 0 || !offsets.TryAdd((int)raw, id))
                throw new InvalidDataException("Invalid, duplicate or unaligned mesh chunk offset.");
        }
        if (offsets.Count != ResourceIds.Length) throw new InvalidDataException("Selected mesh requires all eight resources.");
        var chunks = new Dictionary<int, byte[]>();
        var starts = offsets.Keys.ToArray();
        for (int i = 0; i < starts.Length; i++)
        {
            int start = starts[i], end = i + 1 == starts.Length ? bytes.Length : starts[i + 1];
            chunks.Add(offsets[start], bytes[start..end].ToArray());
        }
        ReadOnlySpan<byte> terrain = Chunk(chunks, 0x1a, 4098, 2);
        int width = terrain[0], depth = terrain[1];
        if (width == 0 || depth == 0 || width * depth > 256) throw new InvalidDataException("Invalid source terrain dimensions.");
        var tiles = ImmutableArray.CreateBuilder<OriginalMapTile>(width * depth * 2);
        for (int layer = 0; layer < 2; layer++)
        for (int i = 0; i < width * depth; i++)
            tiles.Add(new([.. terrain.Slice(2 + (layer * 256 + i) * 8, 8)]));

        byte[] geometry = chunks[0x10];
        if (geometry.Length < 8) throw new InvalidDataException("Truncated mesh counts.");
        int[] count = Enumerable.Range(0, 4).Select(i => (int)BinaryPrimitives.ReadUInt16LittleEndian(geometry.AsSpan(i * 2))).ToArray();
        int[] capacities = [512, 768, 64, 256];
        for (int i = 0; i < 4; i++) if (count[i] > capacities[i]) throw new InvalidDataException("Mesh count exceeds source flag-table capacity.");
        int needed = checked(8 + count[0] * 48 + count[1] * 62 + count[2] * 22 + count[3] * 28);
        if (geometry.Length != needed) throw new InvalidDataException("Mesh geometry count/section span mismatch.");
        var positions = new ImmutableArray<OriginalMapVector>[4][];
        var normals = new ImmutableArray<OriginalMapVector>[2][];
        var material = new ImmutableArray<ushort>[4][];
        int cursor = 8;
        for (int kind = 0; kind < 4; kind++)
        {
            positions[kind] = new ImmutableArray<OriginalMapVector>[count[kind]];
            for (int i = 0; i < count[kind]; i++) positions[kind][i] = ReadVectors(geometry, ref cursor, kind % 2 == 0 ? 3 : 4);
        }
        for (int kind = 0; kind < 2; kind++)
        {
            normals[kind] = new ImmutableArray<OriginalMapVector>[count[kind]];
            for (int i = 0; i < count[kind]; i++) normals[kind][i] = ReadVectors(geometry, ref cursor, kind == 0 ? 3 : 4);
        }
        for (int kind = 0; kind < 4; kind++)
        {
            material[kind] = new ImmutableArray<ushort>[count[kind]];
            int words = kind == 0 ? 5 : kind == 1 ? 6 : 2;
            for (int i = 0; i < count[kind]; i++)
            {
                material[kind][i] = Words(geometry.AsSpan(cursor, words * 2)); cursor += words * 2;
                if (kind < 2 && (material[kind][i][3] is < 12 or > 15 || (material[kind][i][1] & 0x3f) > 15))
                    throw new NotSupportedException("Selected texture pages/CLUTs are outside the four-page map profile.");
            }
        }
        var polygons = ImmutableArray.CreateBuilder<OriginalMapPolygon>();
        var flags = Chunk(chunks, 0x2c, 4096);
        int[] flagStarts = [0x380, 0x780, 0xd80, 0xe00];
        string[] kinds = ["textured-triangle", "textured-quad", "untextured-triangle", "untextured-quad"];
        for (int kind = 0; kind < 4; kind++)
        for (int i = 0; i < count[kind]; i++)
        {
            ushort? tag = null;
            if (kind < 2)
            {
                tag = BinaryPrimitives.ReadUInt16LittleEndian(geometry.AsSpan(cursor)); cursor += 2;
                if (tag != 0xfffe && ((tag.Value >> 8) >= width || ((tag.Value >> 1) & 127) >= depth))
                    throw new InvalidDataException("Mesh terrain tag points outside the selected map.");
            }
            polygons.Add(new(kinds[kind], i, positions[kind][i], kind < 2 ? normals[kind][i] : [], material[kind][i],
                BinaryPrimitives.ReadUInt16LittleEndian(flags[(flagStarts[kind] + i * 2)..]), tag));
        }
        if (cursor != geometry.Length) throw new InvalidDataException("Unconsumed geometry bytes.");
        var light = Chunk(chunks, 0x19, 45, 3);
        var lightMatrices = ImmutableArray.CreateBuilder<short>(18);
        for (int i = 0; i < 18; i++) lightMatrices.Add(BinaryPrimitives.ReadInt16LittleEndian(light[(i * 2)..]));
        return new(width, depth, polygons.ToImmutable(), tiles.ToImmutable(), Words(Chunk(chunks, 0x11, 512)),
            new(lightMatrices.ToImmutable(), [.. light.Slice(36, 9)], [.. Chunk(chunks, 0x1b, 640)],
                Words(Chunk(chunks, 0x1c, 512)), Words(Chunk(chunks, 0x1f, 512))));
    }

    public static ImmutableArray<byte> DecodeTexture(ReadOnlySpan<byte> packed)
    {
        if (packed.Length != 131072) throw new InvalidDataException("Selected map texture must have four 256x256 4bpp pages.");
        byte[] output = new byte[1024 * 256];
        for (int page = 0; page < 4; page++)
        for (int y = 0; y < 256; y++)
        for (int x = 0; x < 256; x++)
        {
            byte value = packed[page * 0x8000 + y * 128 + x / 2];
            output[y * 1024 + page * 256 + x] = (byte)((x & 1) == 0 ? value & 15 : value >> 4);
        }
        return [.. output];
    }

    private static ReadOnlySpan<byte> Chunk(Dictionary<int, byte[]> chunks, int id, int length, int padding = 0)
    {
        byte[] data = chunks[id];
        if (data.Length < length || data.Length > length + padding) throw new InvalidDataException($"Invalid chunk {id:x2} span.");
        foreach (byte value in data.AsSpan(length)) if (value != 0) throw new InvalidDataException("Unexpected nonzero alignment padding.");
        return data.AsSpan(0, length);
    }

    private static ImmutableArray<ushort> Words(ReadOnlySpan<byte> bytes)
    {
        var result = ImmutableArray.CreateBuilder<ushort>(bytes.Length / 2);
        for (int at = 0; at < bytes.Length; at += 2) result.Add(BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]));
        return result.ToImmutable();
    }

    private static ImmutableArray<OriginalMapVector> ReadVectors(byte[] data, ref int cursor, int count)
    {
        var result = ImmutableArray.CreateBuilder<OriginalMapVector>(count);
        for (int i = 0; i < count; i++, cursor += 6)
            result.Add(new(BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(cursor)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(cursor + 2)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(cursor + 4))));
        return result.ToImmutable();
    }
}
