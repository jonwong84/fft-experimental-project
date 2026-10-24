using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Fft.AssetTool.Cli;

/// <summary>Shared bounded address mapping for the pinned initialized PS-X EXE payload.</summary>
internal sealed class OriginalExecutableReader
{
    private readonly byte[] image;
    private readonly uint load, size;
    internal List<OriginalMetadataSourceRecord> Records { get; } = [];

    internal OriginalExecutableReader(ReadOnlySpan<byte> executable)
    {
        if (executable.Length < 0x800 || !executable[..8].SequenceEqual("PS-X EXE"u8))
            throw new InvalidDataException("Expected a complete PS-X EXE header.");
        load = BinaryPrimitives.ReadUInt32LittleEndian(executable[0x18..]);
        size = BinaryPrimitives.ReadUInt32LittleEndian(executable[0x1c..]);
        if (load != 0x80010000 || size == 0 || (ulong)size + 0x800 != (ulong)executable.Length)
            throw new InvalidDataException("Unsupported PS-X EXE load address or initialized payload size.");
        image = executable.ToArray();
    }

    internal static string Verify(ReadOnlySpan<byte> executable)
    {
        if (executable.Length != OriginalMetadataImporter.ExecutableLength)
            throw new InvalidDataException($"SCUS-94221 executable must contain {OriginalMetadataImporter.ExecutableLength} bytes.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(executable));
        if (hash != OriginalMetadataImporter.ExecutableSha256)
            throw new InvalidDataException($"SCUS-94221 executable SHA-256 mismatch: {hash}.");
        return hash;
    }

    internal byte[] Read(string table, uint tableAddress, int stride, int index, string? definitionId)
    {
        if (index < 0 || stride <= 0) throw new InvalidDataException("Invalid source record index/stride.");
        ulong address = (ulong)tableAddress + (ulong)stride * (uint)index;
        if (address < load || address + (uint)stride > (ulong)load + size)
            throw new InvalidDataException($"{table}[{index}] is outside initialized executable data.");
        int offset = checked((int)(address - load + 0x800));
        byte[] raw = image.AsSpan(offset, stride).ToArray();
        Records.Add(new(table, index, definitionId, checked((uint)address), offset, stride, Convert.ToHexStringLower(raw)));
        return raw;
    }
}
