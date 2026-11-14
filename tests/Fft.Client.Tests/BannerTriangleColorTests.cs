using Fft.Client.Presentation;
using Fft.Tests.Support;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BannerTriangleColorTests
{
    [Theory]
    [InlineData(10, 120, 173, 6, 232, 24, 37)]
    [InlineData(10, 120, 174, 6, 232, 24, 38)]
    [InlineData(10, 120, 57, 18, 232, 24, 92)]
    [InlineData(10, 120, 58, 18, 232, 24, 93)]
    [InlineData(120, 10, 173, 6, 232, 24, 93)]
    [InlineData(120, 10, 174, 6, 232, 24, 92)]
    [InlineData(0, 255, 0, 0, 256, 31, 0)]
    [InlineData(128, 128, 231, 23, 232, 24, 128)]
    [InlineData(10, 120, 231, 6, 232, 8, 93)]
    [InlineData(10, 120, 231, 6, 232, 16, 51)]
    public void EachTriangleSeedsTheQuantizedGradientAtItsOwnCoreVertex(
        int top, int bottom, int column, int row, int width, int height, int expected)
        => Assert.Equal(expected, FiveBitGeneralBannerPresentation.QuadVertexChannel(top, bottom, column, row, width, height));

    [Fact]
    public void DiagonalPartitionMatchesIndependentBiasedQ32EdgeWalk()
    {
        for (int width = 1; width <= 256; width++)
        for (int height = 1; height <= 31; height++)
        {
            // Source edge arithmetic uses a ceiling bias, negative-step rounding, then integer extraction.
            long edge = ((long)width << 32) + (1L << 32) - 2048;
            long step = (-((long)width << 32) - (height - 1)) / height;
            for (int row = 0; row < height; row++, edge += step)
            {
                long boundary = edge >> 32;
                Assert.Equal((width * (height - row) + height - 1) / height, boundary);
            }
        }
    }

    [Fact]
    public void CroppedCompositionUsesLocalTriangleCoordinatesAndPreservesBackground()
    {
        var source = AuthoredGeneralBanner.Loaded();
        var artwork = source with
        {
            Indices = [.. Enumerable.Repeat((byte)1, 51200)],
            Palette = [0, 0, 31, 128, .. new byte[28]], // Red five-bit texel with STP set.
            Definition = source.Definition with { Pieces = [.. source.Definition.Pieces.Select(p => p with
                { Colors = [new(10, 0, 0), new(10, 0, 0), new(120, 0, 0), new(120, 0, 0)] })] }
        };
        byte[] background = new byte[256 * 240 * 4];
        for (int at = 3; at < background.Length; at += 4) background[at] = 255;
        var piece = artwork.Definition.Pieces[2];
        var pixels = FiveBitGeneralBannerPresentation.ComposeOver(artwork, [new(new(2, 7, 24), new(false, 0, 0))], background);
        int y = piece.Y + 7 + 6;
        Assert.Equal(64, pixels[(y * 256 + piece.X + 173) * 4]);
        Assert.Equal(72, pixels[(y * 256 + piece.X + 174) * 4]);
        Assert.Equal(background[(piece.Y * 256 + piece.X) * 4], pixels[(piece.Y * 256 + piece.X) * 4]);
        Assert.All(background.Where((_, i) => i % 4 != 3), value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(-1, 0, 232, 24)] [InlineData(232, 0, 232, 24)]
    [InlineData(0, -1, 232, 24)] [InlineData(0, 24, 232, 24)]
    [InlineData(0, 0, 0, 24)] [InlineData(0, 0, 257, 24)]
    [InlineData(0, 0, 232, 0)] [InlineData(0, 0, 232, 257)]
    public void UnsupportedPixelAndRectangleBoundsReject(int column, int row, int width, int height)
        => Assert.Throws<ArgumentOutOfRangeException>(() => FiveBitGeneralBannerPresentation.QuadVertexChannel(10, 120, column, row, width, height));
}
