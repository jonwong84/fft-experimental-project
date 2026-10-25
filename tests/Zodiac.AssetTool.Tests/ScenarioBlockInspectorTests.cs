using System.Buffers.Binary;
using System.Security.Cryptography;
using Xunit;

namespace Zodiac.AssetTool.Tests;

public sealed class ScenarioBlockInspectorTests
{
    [Fact]
    public void Operand_metadata_matches_pinned_source_vector_and_excludes_F4_through_FF()
    {
        byte[] sizes = new byte[244];
        for (int op = 0; op < sizes.Length; op++)
        { Assert.True(ScenarioBlockInspector.TryGetOperandByteCount((byte)op, out int length)); sizes[op] = (byte)length; }
        // Independently captured from the pinned C metadata, not generated from the managed table.
        Assert.Equal("5f50d72e0e4d2a62215be25bc01f9123c0727a2abdcb9a4ff038511c569d915f", Convert.ToHexStringLower(SHA256.HashData(sizes)));
        for (int op = 244; op <= 255; op++) Assert.False(ScenarioBlockInspector.TryGetOperandByteCount((byte)op, out _));
    }

    [Theory]
    [InlineData(ScenarioBlockEncoding.Raw)]
    [InlineData(ScenarioBlockEncoding.Processed)]
    public void Header_is_metadata_and_inspection_preserves_bytes_offsets_and_opaque_text(ScenarioBlockEncoding encoding)
    {
        byte[] block = Block([0xbe, 0, 0, 0xd4, 7, 0xdb]);
        if (encoding == ScenarioBlockEncoding.Processed) BinaryPrimitives.WriteUInt32LittleEndian(block, ScenarioBlockInspector.ProcessedMarker);
        var inspected = ScenarioBlockInspector.Inspect(block, encoding, encoding == ScenarioBlockEncoding.Processed ? 10 : null);
        Assert.True(inspected.Complete); Assert.Empty(inspected.Issues);
        Assert.Equal(new[] { 4, 7, 9 }, inspected.Instructions.Select(r => r.Offset));
        Assert.Equal(new byte[] { 0xbe, 0xd4, 0xdb }, inspected.Instructions.Select(r => r.Opcode));
        Assert.Equal([0, 0], inspected.Instructions[0].Operands.Select(b => (int)b));
        Assert.Equal(0xfe, inspected.Bytes[10]);
        string hash = inspected.Sha256;
        block[4] = 0xff;
        Assert.Equal(0xbe, inspected.Bytes[4]); Assert.Equal(hash, inspected.Sha256);
    }

    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(8192)] [InlineData(int.MaxValue)]
    public void Raw_text_offset_must_be_in_block(uint offset)
    {
        byte[] block = Block([0xd4, 0, 0xdb]); BinaryPrimitives.WriteUInt32LittleEndian(block, offset);
        Assert.Throws<InvalidDataException>(() => ScenarioBlockInspector.Inspect(block, ScenarioBlockEncoding.Raw));
    }

    [Fact]
    public void Encoding_mismatch_or_missing_processed_provenance_fails_without_guessing()
    {
        byte[] raw = Block([0xd4, 0, 0xdb]);
        Assert.Throws<InvalidDataException>(() => ScenarioBlockInspector.Inspect(raw, ScenarioBlockEncoding.Processed, 7));
        Assert.Throws<InvalidDataException>(() => ScenarioBlockInspector.Inspect(raw, ScenarioBlockEncoding.Raw, 7));
        BinaryPrimitives.WriteUInt32LittleEndian(raw, ScenarioBlockInspector.ProcessedMarker);
        Assert.Throws<InvalidDataException>(() => ScenarioBlockInspector.Inspect(raw, ScenarioBlockEncoding.Raw));
        Assert.Throws<InvalidDataException>(() => ScenarioBlockInspector.Inspect(raw, ScenarioBlockEncoding.Processed));
        Assert.Throws<InvalidDataException>(() => ScenarioBlockInspector.Inspect(raw, ScenarioBlockEncoding.Processed, 8192));
    }

    [Theory]
    [InlineData(0)] [InlineData(8191)] [InlineData(8193)]
    public void Exact_block_extent_is_enforced(int length) => Assert.Throws<InvalidDataException>(() =>
        ScenarioBlockInspector.Inspect(new byte[length], ScenarioBlockEncoding.Raw));

    [Fact]
    public void Known_unsupported_instructions_are_preserved_but_unknown_lengths_stop_inspection()
    {
        // DisplayMessage has 14 operand bytes. Zeroes inside it are data, not instruction boundaries.
        byte[] code = [0x10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xdb];
        var inspected = ScenarioBlockInspector.Inspect(Block(code), ScenarioBlockEncoding.Raw);
        Assert.True(inspected.Complete); Assert.Equal(14, inspected.Instructions[0].Operands.Length);
        Assert.Equal(19, inspected.Instructions[1].Offset);
        var invalid = ScenarioBlockInspector.Inspect(Block([0xf2, 0xf4, 0xdb]), ScenarioBlockEncoding.Raw);
        Assert.False(invalid.Complete); Assert.Single(invalid.Instructions);
        Assert.Equal(5, Assert.Single(invalid.Issues).Offset); Assert.Equal(0xf4, invalid.Bytes[5]);
    }

    [Fact]
    public void Truncated_operands_missing_sentinel_and_text_bytes_are_not_reinterpreted()
    {
        var truncated = ScenarioBlockInspector.Inspect(Block([0xf2, 0xb0, 0]), ScenarioBlockEncoding.Raw);
        Assert.False(truncated.Complete); Assert.Single(truncated.Instructions);
        Assert.Equal(5, Assert.Single(truncated.Issues).Offset);
        var missing = ScenarioBlockInspector.Inspect(Block([0xd4, 0]), ScenarioBlockEncoding.Raw);
        Assert.False(missing.Complete); Assert.Contains("sentinel", Assert.Single(missing.Issues).Message);
        byte[] padding = Block([0xd4, 0, 0xdb, 0xff, 0xfe]);
        var complete = ScenarioBlockInspector.Inspect(padding, ScenarioBlockEncoding.Raw);
        Assert.True(complete.Complete); Assert.Equal(2, complete.Instructions.Length);
        Assert.Equal(0xff, complete.Bytes[7]); // Preserved post-sentinel bytes are not executable instructions.
    }

    private static byte[] Block(byte[] code)
    {
        byte[] block = new byte[8192]; int text = 4 + code.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(block, (uint)text);
        code.CopyTo(block, 4); block[text] = 0xfe; // One empty packed text entry; no 32-offset bank table here.
        return block;
    }
}
