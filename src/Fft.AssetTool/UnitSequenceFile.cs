using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Fft.AssetTool;

public readonly record struct UnitSequencePose(byte Frame, byte Wait);

/// <summary>Offline unit SEQ table and first-frame reader; no opcode interpreter or animation clock.
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, unit_seq_data_unpack/unit_seq_run.</summary>
public sealed class UnitSequenceFile
{
    public ushort FirstAttackSequence { get; }
    public ushort FirstSp2Sequence { get; }
    private readonly ImmutableArray<int> offsets;
    private readonly byte[] payload;

    private UnitSequenceFile(ushort attack, ushort sp2, ImmutableArray<int> offsets, byte[] payload) =>
        (FirstAttackSequence, FirstSp2Sequence, this.offsets, this.payload) = (attack, sp2, offsets, payload);

    public static UnitSequenceFile Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 0x406) throw new InvalidDataException("Truncated unit SEQ table.");
        int length = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x404..]);
        if (length == 0 || length > bytes.Length - 0x406) throw new InvalidDataException("Invalid SEQ payload length.");
        var offsets = ImmutableArray.CreateBuilder<int>(256);
        for (int i = 0; i < 256; i++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes[(4 + i * 4)..]);
            if (offset == -1) offset = 0;
            if (offset < 0 || offset >= length) throw new InvalidDataException($"Invalid SEQ offset {i}.");
            offsets.Add(offset);
        }
        return new(BinaryPrimitives.ReadUInt16LittleEndian(bytes), BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]),
            offsets.ToImmutable(), bytes.Slice(0x406, length).ToArray());
    }

    public UnitSequencePose FirstPose(int sequenceId)
    {
        if (sequenceId is < 0 or >= 256) throw new ArgumentOutOfRangeException(nameof(sequenceId));
        if (sequenceId >= FirstAttackSequence || sequenceId >= FirstSp2Sequence)
            throw new NotSupportedException("Attack/SP2 sequences are outside the ordinary pose slice.");
        int offset = offsets[sequenceId];
        if (offset > payload.Length - 2) throw new InvalidDataException("Truncated SEQ frame instruction.");
        if (payload[offset] == 0xff) throw new NotSupportedException("Commands before the first pose are unsupported.");
        return new(payload[offset], payload[offset + 1]); // Zero wait means a held pose, never a busy loop.
    }
}
