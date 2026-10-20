using Fft.AssetTool;
using Xunit;

namespace Fft.AssetTool.Tests;

public sealed class SpriteAttackImageDecoderTests
{
    [Fact]
    public void Decode_reads_nibbles_high_first_and_packs_first_pixel_high()
    {
        byte[] source = EncodeWithTransparentRemainder([1, 2, 3, 4]);

        byte[] decoded = SpriteAttackImageDecoder.Decode(source);

        Assert.Equal(SpriteAttackImageDecoder.DecodedByteLength, decoded.Length);
        Assert.Equal(new byte[] { 0x12, 0x34 }, decoded[..2]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(15)]
    public void Decode_expands_direct_transparent_run_lengths(int runLength)
    {
        var nibbles = new List<int> { 0, runLength, 0xa, 0xb };
        byte[] source = EncodeWithTransparentRemainder(nibbles.ToArray(), runLength + 2);

        byte[] decoded = SpriteAttackImageDecoder.Decode(source);

        AssertPixel(decoded, runLength, 0xa);
        AssertPixel(decoded, runLength + 1, 0xb);
    }

    [Fact]
    public void Decode_reads_extended_run_length_nibbles_low_part_first()
    {
        // 0,7 selects the two-nibble form; nibbles 3,1 encode 0x13 pixels.
        byte[] source = EncodeWithTransparentRemainder([0, 7, 3, 1, 0xc], 0x13 + 1);

        byte[] decoded = SpriteAttackImageDecoder.Decode(source);

        AssertPixel(decoded, 0x13, 0xc);
    }

    [Fact]
    public void Decode_reads_one_nibble_extended_run_length()
    {
        // 0,0 selects the one-extra-nibble form.
        byte[] source = EncodeWithTransparentRemainder([0, 0, 6, 0xd], 7);

        byte[] decoded = SpriteAttackImageDecoder.Decode(source);

        AssertPixel(decoded, 6, 0xd);
    }

    [Fact]
    public void Decode_reads_three_nibble_extended_run_length()
    {
        // 0,8 selects the three-extra-nibble form; 4,2,1 encode 0x124 pixels.
        byte[] source = EncodeWithTransparentRemainder([0, 8, 4, 2, 1, 0xe], 0x124 + 1);

        byte[] decoded = SpriteAttackImageDecoder.Decode(source);

        AssertPixel(decoded, 0x124, 0xe);
    }

    [Fact]
    public void Decode_clips_transparent_run_at_end_of_image()
    {
        var nibbles = new List<int>();
        for (var i = 0; i < 13; i++)
        {
            nibbles.AddRange([0, 8, 0xf, 0xf, 0xf]);
        }

        byte[] source = EncodeNibbles(nibbles);

        byte[] decoded = SpriteAttackImageDecoder.Decode(source);

        Assert.Equal(SpriteAttackImageDecoder.DecodedByteLength, decoded.Length);
        Assert.All(decoded, value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x07 })]
    public void Decode_rejects_truncated_input(byte[] source)
    {
        Assert.Throws<InvalidDataException>(() => SpriteAttackImageDecoder.Decode(source));
    }

    private static byte[] EncodeWithTransparentRemainder(int[] prefix, int prefixPixelCount = -1)
    {
        prefixPixelCount = prefixPixelCount < 0 ? prefix.Length : prefixPixelCount;
        var nibbles = new List<int>(prefix);
        var remainingPixels = (SpriteAttackImageDecoder.DecodedByteLength * 2) - prefixPixelCount;
        while (remainingPixels > 0)
        {
            var runLength = Math.Min(remainingPixels, 0xfff);
            nibbles.AddRange([0, 8, runLength & 0xf, (runLength >> 4) & 0xf, (runLength >> 8) & 0xf]);
            remainingPixels -= runLength;
        }

        return EncodeNibbles(nibbles);
    }

    private static byte[] EncodeNibbles(IReadOnlyList<int> nibbles)
    {
        var bytes = new byte[(nibbles.Count + 1) / 2];
        for (var i = 0; i < nibbles.Count; i++)
        {
            bytes[i >> 1] |= (byte)(nibbles[i] << ((i & 1) == 0 ? 4 : 0));
        }

        return bytes;
    }

    private static void AssertPixel(byte[] packedPixels, int pixelPosition, int expected)
    {
        var packedByte = packedPixels[pixelPosition >> 1];
        var actual = (pixelPosition & 1) == 0 ? packedByte >> 4 : packedByte & 0x0f;
        Assert.Equal(expected, actual);
    }
}
