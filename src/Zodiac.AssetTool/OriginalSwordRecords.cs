using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Zodiac.AssetTool;

// Import records preserve raw widths; they do not certify a runtime-supported combat configuration.
public sealed record OriginalItemRecord(byte Palette, byte SpriteId, byte RequiredLevel, byte TypeFlags,
    byte SecondaryDataId, byte Type, byte Unused06, byte Attributes, ushort Price, byte ShopAvailability, byte Padding0b);
public sealed record OriginalWeaponRecord(byte Range, byte Flags, byte Formula, byte Unused03,
    byte Power, byte Evade, byte Element, byte ProcId);
public sealed record OriginalProcRecord(byte Type, ImmutableArray<byte> Statuses);

/// <summary>Pinned item.h/status.h layouts; decomp 611b504d25d4a9b03fab8023d7dbea75fe381d5d.</summary>
public static class OriginalSwordRecords
{
    public static OriginalItemRecord DecodeItem(ReadOnlySpan<byte> bytes)
    {
        Length(bytes, 12, "item");
        return new(bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7],
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]), bytes[10], bytes[11]);
    }
    public static OriginalWeaponRecord DecodeWeapon(ReadOnlySpan<byte> bytes)
    {
        Length(bytes, 8, "weapon");
        return new(bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7]);
    }
    public static OriginalProcRecord DecodeProc(ReadOnlySpan<byte> bytes)
    {
        Length(bytes, 6, "status infliction");
        return new(bytes[0], ImmutableArray.CreateRange(bytes[1..].ToArray()));
    }
    private static void Length(ReadOnlySpan<byte> bytes, int length, string kind)
    {
        if (bytes.Length != length) throw new InvalidDataException($"Original {kind} record requires exactly {length} bytes; received {bytes.Length}.");
    }
}
