using System.Buffers.Binary;
using Xunit;

namespace Fft.AssetTool.Tests;

public sealed class OrdinaryUnitPoseTests
{
    [Fact]
    public void Shp_copies_signed_parts_and_maps_missing_offsets_to_the_first_record()
    {
        var bytes = Shape(0xc000);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), -1);
        var parsed = UnitShapeFile.Parse(bytes);
        bytes[0x40c] = 99;
        Assert.Equal(208, parsed.Primary.Length);
        Assert.Equal(parsed.Primary, parsed.Submerged);
        var part = Assert.Single(parsed.Primary[0].Parts);
        Assert.Equal(-128, part.X); Assert.Equal(-22, part.Y);
        Assert.True(part.FlipHorizontal); Assert.True(part.FlipVertical);
        Assert.Equal(61, parsed.FirstAttackFrame);
    }

    [Fact]
    public void Optional_secondary_set_is_distinct_and_must_not_overlap_primary_payload()
    {
        var primary = Shape(0);
        var bytes = new byte[primary.Length + primary.Length - 8];
        primary.CopyTo(bytes, 0);
        primary.AsSpan(8).CopyTo(bytes.AsSpan(primary.Length));
        BinaryPrimitives.WriteInt32LittleEndian(bytes, primary.Length);
        bytes[primary.Length + 0x404] = 7; // first part's signed X in the second set
        var parsed = UnitShapeFile.Parse(bytes);
        Assert.Equal(-128, parsed.Primary[0].Parts[0].X);
        Assert.Equal(7, parsed.Submerged[0].Parts[0].X);
        BinaryPrimitives.WriteInt32LittleEndian(bytes, primary.Length - 1);
        Assert.Throws<InvalidDataException>(() => UnitShapeFile.Parse(bytes));
    }

    [Theory]
    [InlineData("header")] [InlineData("table")] [InlineData("payload")]
    [InlineData("negative")] [InlineData("large")] [InlineData("parts")]
    [InlineData("second-negative")] [InlineData("second-large")]
    public void Shp_rejects_truncation_bad_offsets_and_partial_records(string kind)
    {
        var bytes = Shape(0);
        switch (kind)
        {
            case "header": bytes = bytes[..7]; break;
            case "table": bytes = bytes[..0x408]; break;
            case "payload": bytes = bytes[..^1]; break;
            case "negative": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), -2); break;
            case "large": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), int.MaxValue); break;
            case "parts": bytes[0x40a] = 7; break;
            case "second-negative": BinaryPrimitives.WriteInt32LittleEndian(bytes, -1); break;
            case "second-large": BinaryPrimitives.WriteInt32LittleEndian(bytes, int.MaxValue); break;
        }
        Assert.Throws<InvalidDataException>(() => UnitShapeFile.Parse(bytes));
    }

    [Fact]
    public void Uninstalled_shp_entries_are_not_interpreted_as_native_frame_pointers()
    {
        var bytes = Shape(0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8 + 208 * 4), int.MinValue);
        Assert.Equal(208, UnitShapeFile.Parse(bytes).Primary.Length);
    }

    [Theory]
    [InlineData("header")] [InlineData("payload")] [InlineData("empty")]
    [InlineData("negative")] [InlineData("large")]
    public void Seq_rejects_truncated_payloads_and_invalid_signed_offsets(string kind)
    {
        var bytes = Sequence();
        switch (kind)
        {
            case "header": bytes = bytes[..0x405]; break;
            case "payload": bytes = bytes[..^1]; break;
            case "empty": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x404), 0); break;
            case "negative": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), -2); break;
            case "large": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), int.MaxValue); break;
        }
        Assert.Throws<InvalidDataException>(() => UnitSequenceFile.Parse(bytes));
    }

    [Fact]
    public void Seq_owns_its_payload_preserves_zero_wait_and_normalizes_minus_one()
    {
        var bytes = Sequence(wait: 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + 6 * 4), -1);
        var seq = UnitSequenceFile.Parse(bytes);
        bytes[0x406] = 99;
        Assert.Equal(new UnitSequencePose(9, 0), seq.FirstPose(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => seq.FirstPose(256));
        Assert.Throws<NotSupportedException>(() => seq.FirstPose(120));
    }

    [Fact]
    public void Seq_first_pose_does_not_interpret_commands_or_read_past_a_one_byte_tail()
    {
        var bytes = Sequence(); bytes[0x406] = 0xff;
        Assert.Throws<NotSupportedException>(() => UnitSequenceFile.Parse(bytes).FirstPose(6));
        bytes[0x406] = 9;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + 6 * 4), 1);
        Assert.Throws<InvalidDataException>(() => UnitSequenceFile.Parse(bytes).FirstPose(6));
    }

    [Fact]
    public void Ordinary_pixels_are_low_first_and_stp_does_not_make_an_opaque_primitive_translucent()
    {
        var sprite = Sprite();
        var pose = Decode(sprite, Shape(0));
        Assert.Equal(new byte[] { 255,0,0,255, 0,255,0,255, 0,0,0,0, 0,0,255,255, 0,0,0,255 }, pose.Rgba.Take(20));
        Assert.Equal(0, pose.RawPalette[4]); // nonzero index with a black raw color becomes opaque via native STP load
        Assert.Equal(6, pose.Wait); Assert.Equal(9, pose.FrameId);
        Assert.Equal(0, pose.PaletteIndex);
    }

    [Fact]
    public void Piece_order_is_reversed_while_signed_anchors_and_flip_bits_are_preserved()
    {
        var bytes = Shape(0x4000, 0x8000);
        var pose = Decode(Sprite(), bytes);
        Assert.Equal(new[] { 1, 0 }, pose.PartsBackToFront.Select(p => p.SourceIndex));
        Assert.True(pose.PartsBackToFront[0].FlipVertical);
        Assert.False(pose.PartsBackToFront[0].FlipHorizontal);
        Assert.True(pose.PartsBackToFront[1].FlipHorizontal);
        Assert.Equal(-128, pose.PartsBackToFront[1].X);
        Assert.Equal(8, pose.PartsBackToFront[0].Width);
    }

    [Theory]
    [InlineData("rotation")] [InlineData("blend")] [InlineData("special-size")]
    [InlineData("attack-frame")] [InlineData("sp2-frame")] [InlineData("nonzero-transparent-color")]
    public void Unsupported_pose_paths_fail_instead_of_being_approximated(string kind)
    {
        var sprite = Sprite(); var shape = Shape(0);
        switch (kind)
        {
            case "rotation": shape[0x40a] = 8; break;
            case "blend": shape[0x40b] = 1; break;
            case "special-size": BinaryPrimitives.WriteUInt16LittleEndian(shape.AsSpan(0x40e), 14 << 10); break;
            case "attack-frame": BinaryPrimitives.WriteUInt16LittleEndian(shape.AsSpan(4), 9); break;
            case "sp2-frame": BinaryPrimitives.WriteUInt16LittleEndian(shape.AsSpan(6), 9); break;
            case "nonzero-transparent-color": sprite[0] = 1; break;
        }
        Assert.Throws<NotSupportedException>(() => Decode(sprite, shape));
    }

    [Fact]
    public void Rectangles_must_fit_the_atlas_and_runtime_palette_selection_is_bounded()
    {
        Assert.Throws<InvalidDataException>(() => Decode(Sprite(), Shape((5 << 10) | 31)));
        var edge = Decode(Sprite(), Shape(31));
        Assert.Equal(248, edge.PartsBackToFront[0].U);
        Assert.Throws<NotSupportedException>(() => OrdinaryUnitPoseDecoder.Decode(UnitSpriteSheet.Parse(Sprite()),
            UnitShapeFile.Parse(Shape(0)), UnitSequenceFile.Parse(Sequence()), 6, 6));
    }

    private static DecodedUnitPose Decode(byte[] spr, byte[] shp) => OrdinaryUnitPoseDecoder.Decode(
        UnitSpriteSheet.Parse(spr), UnitShapeFile.Parse(shp), UnitSequenceFile.Parse(Sequence()), 6, 0);

    private static byte[] Shape(params ushort[] attributes)
    {
        var bytes = new byte[0x40a + 2 + 4 * attributes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 61);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 255);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x408), (ushort)(2 + 4 * attributes.Length));
        bytes[0x40a] = (byte)(attributes.Length - 1);
        for (int i = 0; i < attributes.Length; i++)
        {
            int at = 0x40c + 4 * i;
            bytes[at] = 128; bytes[at + 1] = 234;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 2), attributes[i]);
        }
        return bytes;
    }

    private static byte[] Sequence(byte wait = 6)
    {
        var bytes = new byte[0x408];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 120);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 255);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x404), 2);
        bytes[0x406] = 9; bytes[0x407] = wait;
        return bytes;
    }

    private static byte[] Sprite()
    {
        var bytes = new byte[UnitSpriteSheet.FixedLength];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 31);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 31 << 5);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 31 << 10);
        bytes[0x200] = 0x21; bytes[0x201] = 0x30; bytes[0x202] = 0x04;
        return bytes;
    }
}
