using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Zodiac.AssetTool;

public sealed record UnitShapePart(sbyte X, sbyte Y, ushort Attributes)
{
    public int U => (Attributes & 31) * 8;
    public int V => ((Attributes & 0x3ff) >> 5) * 8;
    public int SizeIndex => (Attributes & 0x3c00) >> 10;
    public bool FlipHorizontal => (Attributes & 0x4000) != 0;
    public bool FlipVertical => (Attributes & 0x8000) != 0;
}

public sealed record UnitShapeFrame(byte RotationIndex, byte Flags, ImmutableArray<UnitShapePart> Parts);

/// <summary>Offline unit SHP reader, not WEP/EFF. Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df,
/// sprite_runtime.c::unit_shp_data_unpack. The native format installs 208 of its 256 offsets.</summary>
public sealed class UnitShapeFile
{
    public ushort FirstAttackFrame { get; }
    public ushort FirstSp2Frame { get; }
    public ImmutableArray<UnitShapeFrame> Primary { get; }
    public ImmutableArray<UnitShapeFrame> Submerged { get; }

    private UnitShapeFile(ushort attack, ushort sp2, ImmutableArray<UnitShapeFrame> primary,
        ImmutableArray<UnitShapeFrame> submerged) =>
        (FirstAttackFrame, FirstSp2Frame, Primary, Submerged) = (attack, sp2, primary, submerged);

    public static UnitShapeFile Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8) throw new InvalidDataException("Truncated unit SHP header.");
        int second = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        var (primary, end) = ReadSet(bytes, 8);
        if (second != 8 && (second < end || second > bytes.Length - 0x402))
            throw new InvalidDataException("Invalid or overlapping secondary SHP frame set.");
        var submerged = second == 8 ? primary : ReadSet(bytes, second).Frames;
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]), primary, submerged);
    }

    private static (ImmutableArray<UnitShapeFrame> Frames, int End) ReadSet(ReadOnlySpan<byte> bytes, int start)
    {
        if (start < 0 || start > bytes.Length - 0x402) throw new InvalidDataException("Truncated SHP offset table.");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(start + 0x400)..]);
        int payload = start + 0x402;
        if (count > bytes.Length - payload) throw new InvalidDataException("Truncated SHP frame data.");
        var frames = ImmutableArray.CreateBuilder<UnitShapeFrame>(0xd0);
        for (int i = 0; i < 0xd0; i++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes[(start + i * 4)..]);
            if (offset == -1) offset = 0; // Original fallback points to data start, not a missing frame.
            if (offset < 0 || offset > count - 2) throw new InvalidDataException($"Invalid SHP frame offset {i}.");
            var data = bytes.Slice(payload + offset, count - offset);
            int partCount = (data[0] & 7) + 1;
            if (partCount > (data.Length - 2) / 4) throw new InvalidDataException($"Truncated SHP frame {i}.");
            var parts = ImmutableArray.CreateBuilder<UnitShapePart>(partCount);
            for (int p = 0; p < partCount; p++)
            {
                int at = 2 + p * 4;
                parts.Add(new(unchecked((sbyte)data[at]), unchecked((sbyte)data[at + 1]),
                    BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 2)..])));
            }
            frames.Add(new((byte)(data[0] >> 3), data[1], parts.ToImmutable()));
        }
        return (frames.ToImmutable(), payload + count);
    }
}
