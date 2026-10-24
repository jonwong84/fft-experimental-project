using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Fft.AssetTool;

public enum ScenarioBlockEncoding { Raw, Processed }
public sealed record ScenarioInstructionRecord(int Offset, byte Opcode, ImmutableArray<byte> Operands);
public sealed record ScenarioInspectionIssue(int Offset, string Message);

/// <summary>Offline inspection only. Original bytes/unsupported records never become an executable program implicitly.</summary>
public sealed record ScenarioBlockInspection(ScenarioBlockEncoding Encoding, int TextOffset, string Sha256,
    ImmutableArray<byte> Bytes, ImmutableArray<ScenarioInstructionRecord> Instructions, ImmutableArray<ScenarioInspectionIssue> Issues)
{
    public bool Complete => Issues.IsEmpty;
}

/// <summary>SCUS-94221 block/operand metadata traced at the pinned reference; no event execution or text decoding.</summary>
public static class ScenarioBlockInspector
{
    public const int BlockBytes = 8192;
    public const uint ProcessedMarker = 0xf2f2f2f2;
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";

    // Protocol operand-byte counts, not handlers. Source: lib/event/script/control.c at ReferenceRevision.
    private static ReadOnlySpan<byte> OperandSizes =>
    [
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        14,5,2,2,2,0,0,5,6,16,5,14,1,0,0,5,
        6,2,3,3,0,4,2,0,8,2,0,0,7,6,8,0,
        0,5,7,5,2,4,2,2,2,0,0,12,2,2,9,4,
        5,4,0,1,2,3,2,8,0,0,0,0,2,1,3,1,
        1,5,2,7,2,2,0,0,3,1,1,1,3,1,1,6,
        2,3,6,1,2,0,0,2,3,8,5,5,2,2,12,2,
        4,2,0,14,6,6,6,0,2,4,2,2,0,1,4,4,
        3,3,0,5,1,1,3,4,0,0,0,0,6,0,0,1,
        3,3,5,2,2,0,0,2,2,2,0,0,0,0,0,0,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        4,4,4,4,4,4,4,4,4,4,4,4,4,4,2,0,
        16,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
        1,1,1,1,1,1,0,0,1,1,0,0,0,0,0,0,
        2,1,1,0,1,2,0,0,0,0,0,0,0,0,0,0,
        0,2,0,0,
    ];

    public static bool TryGetOperandByteCount(byte opcode, out int count)
    {
        count = opcode < OperandSizes.Length ? OperandSizes[opcode] : 0;
        return opcode < OperandSizes.Length;
    }

    public static ScenarioBlockInspection Inspect(ReadOnlySpan<byte> bytes, ScenarioBlockEncoding encoding, int? processedTextOffset = null)
    {
        if (bytes.Length != BlockBytes) throw new InvalidDataException($"Scenario block must contain exactly {BlockBytes} bytes.");
        if (!Enum.IsDefined(encoding)) throw new ArgumentOutOfRangeException(nameof(encoding));
        uint header = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        int textOffset;
        if (encoding == ScenarioBlockEncoding.Raw)
        {
            if (processedTextOffset is not null || header == ProcessedMarker || header is < 4 or >= BlockBytes)
                throw new InvalidDataException("Raw block requires an in-block text offset and no processed-boundary override.");
            textOffset = (int)header;
        }
        else
        {
            if (header != ProcessedMarker || processedTextOffset is null || processedTextOffset is < 4 or >= BlockBytes)
                throw new InvalidDataException("Processed block requires the NOP marker and an explicit preserved text offset.");
            textOffset = processedTextOffset.Value;
        }
        var instructions = ImmutableArray.CreateBuilder<ScenarioInstructionRecord>();
        var issues = ImmutableArray.CreateBuilder<ScenarioInspectionIssue>();
        int offset = 4; // Header is metadata; prepared execution need not synthesize four leading NOPs.
        bool ended = false;
        while (offset < textOffset)
        {
            byte opcode = bytes[offset];
            if (opcode >= OperandSizes.Length)
            { issues.Add(new(offset, $"Opcode 0x{opcode:x2} has no source operand-length entry; inspection stopped without guessing.")); break; }
            int size = OperandSizes[opcode];
            if (size > textOffset - offset - 1)
            { issues.Add(new(offset, "Instruction operands cross the text boundary.")); break; }
            instructions.Add(new(offset, opcode, ImmutableArray.Create(bytes.Slice(offset + 1, size).ToArray())));
            offset += 1 + size;
            if (opcode == 0xdb) { ended = true; break; } // Search sentinel; semantic EventEnd is not implemented.
        }
        if (!ended && issues.Count == 0) issues.Add(new(offset, "No DB search sentinel before the text boundary."));
        return new(encoding, textOffset, Convert.ToHexStringLower(SHA256.HashData(bytes)),
            ImmutableArray.Create(bytes.ToArray()), instructions.ToImmutable(), issues.ToImmutable());
    }
}
