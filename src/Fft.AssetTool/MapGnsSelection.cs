using System.Buffers.Binary;

namespace Fft.AssetTool;

public sealed record MapFileSelection(uint TextureLba, uint TextureReadLength, uint MeshLba, uint MeshReadLength);

/// <summary>Offline validation of the selected day/dry GNS path, not a general GNS interpreter.
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, battle/map/loading.c and gns.c.</summary>
public static class MapGnsSelection
{
    public static MapFileSelection ReadDayDry(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 4096) throw new InvalidDataException("GNS exceeds the selected input budget.");
        uint texture = 0, textureLength = 0, mesh = 0, meshLength = 0;
        int at = 0;
        bool separator = false;
        while (at <= bytes.Length - 20)
        {
            byte type = bytes[at + 5];
            if (type == 0x80) { separator = true; at += 20; break; }
            ushort variable = BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
            short state = BinaryPrimitives.ReadInt16LittleEndian(bytes[(at + 2)..]);
            if (variable != 0x22 || bytes[at + 4] != 1 || type is not (0x17 or 0x2e or 0x30 or 0x31) ||
                BinaryPrimitives.ReadUInt16LittleEndian(bytes[(at + 6)..]) != 0x3333 ||
                !bytes.Slice(at + 16, 4).SequenceEqual(new byte[] { 0x55, 0x66, 0x77, 0x88 }))
                throw new InvalidDataException("Unsupported GNS file row in the day/dry profile.");
            uint lba = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 8)..]);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 12)..]);
            if (lba == 0 || length == 0 || length % 2048 != 0) throw new InvalidDataException("Invalid GNS file extent.");
            if (state == 0)
            {
                if (type == 0x17 && texture == 0) { texture = lba; textureLength = length; }
                else if (type == 0x2e && mesh == 0) { mesh = lba; meshLength = length; }
                else throw new NotSupportedException("Ambiguous or unsupported selected GNS resource.");
            }
            at += 20;
        }
        if (!separator || texture == 0 || mesh == 0) throw new InvalidDataException("GNS requires a separator and selected texture/mesh.");
        bool indoor = false, clearSnow = false;
        while (at < bytes.Length)
        {
            if (at > bytes.Length - 20) throw new InvalidDataException("Truncated GNS command prefix.");
            int type = bytes[at + 5];
            if (type == 0)
            {
                foreach (byte value in bytes[at..]) if (value != 0) throw new InvalidDataException("Nonzero GNS command tail.");
                at = bytes.Length;
                break;
            }
            int stride = bytes[at + 8];
            int expected = type switch { 0x85 or 0x88 => 20, 0x8a => 28, _ => throw new NotSupportedException("Unsupported GNS scene command.") };
            if (stride != expected || stride > bytes.Length - at) throw new InvalidDataException("Invalid GNS command stride.");
            indoor |= type == 0x85; clearSnow |= type == 0x88;
            at += stride;
        }
        if (!indoor || !clearSnow) throw new NotSupportedException("Selected GNS must establish indoor/no-snow presentation.");
        return new(texture, textureLength, mesh, meshLength);
    }
}
