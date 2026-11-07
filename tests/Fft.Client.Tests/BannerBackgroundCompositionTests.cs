using System.Collections.Immutable;
using Fft.Client.Presentation;
using Fft.Tests.Support;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BannerBackgroundCompositionTests
{
    private static byte[] Background(byte r = 160, byte g = 40, byte b = 248)
    {
        var pixels = new byte[256 * 240 * 4];
        for (int at = 0; at < pixels.Length; at += 4)
        { pixels[at] = r; pixels[at + 1] = g; pixels[at + 2] = b; pixels[at + 3] = 255; }
        return pixels;
    }

    [Fact]
    public void EmptyDrawsPreserveEveryBackgroundChannelWithoutAliasing()
    {
        var background = Background();
        var result = FiveBitGeneralBannerPresentation.ComposeOver(AuthoredGeneralBanner.Loaded(), [], background);
        Assert.Equal(background, result); Assert.NotSame(background, result);
        background[0] = 0; Assert.Equal(160, result[0]);
    }

    [Theory]
    [InlineData(0)][InlineData(32768)]
    public void TransparentAndStpBlackKeepTheExplicitBackground(int word)
    {
        var artwork = AuthoredGeneralBanner.Loaded() with { Palette = [0, 0, (byte)word, (byte)(word >> 8), .. new byte[28]] };
        var background = Background();
        var result = FiveBitGeneralBannerPresentation.ComposeOver(artwork, [new(new(0, 0, 31), new(false, 0, 0))], background);
        Assert.Equal(background, result);
    }

    [Fact]
    public void SaturationAndOrderUseTheSuppliedBackgroundForEveryPass()
    {
        ushort word = 0x8000 | 10 | (10 << 5) | (10 << 10);
        var artwork = AuthoredGeneralBanner.Loaded() with { Palette = [0, 0, (byte)word, (byte)(word >> 8), .. new byte[28]] };
        var background = Background(); var before = background.ToArray();
        static ImmutableArray<BannerRasterDraw> Draws(params int[] indices)
            => [.. indices.Select(i => new BannerRasterDraw(new(i, 0, 31), new(false, 0, 0)))];
        var selected = FiveBitGeneralBannerPresentation.ComposeOver(artwork, Draws(0, 1, 2), background);
        var reversed = FiveBitGeneralBannerPresentation.ComposeOver(artwork, Draws(2, 1, 0), background);
        int at = (20 * 256 + 4) * 4;
        Assert.Equal(new byte[] { 80, 80, 168, 255 }, selected.AsSpan(at, 4).ToArray());
        Assert.Equal(new byte[] { 80, 0, 88, 255 }, reversed.AsSpan(at, 4).ToArray());
        Assert.Equal(before, background);
        Assert.Equal(selected, FiveBitGeneralBannerPresentation.ComposeOver(artwork, Draws(0, 1, 2), background));
    }

    [Fact]
    public void OpaqueTexelReplacesBackgroundAndCropLeavesOtherRowsIntact()
    {
        var artwork = AuthoredGeneralBanner.Loaded() with { Palette = [0, 0, 31, 0, .. new byte[28]] };
        var background = Background();
        var result = FiveBitGeneralBannerPresentation.ComposeOver(artwork, [new(new(0, 8, 23), new(false, 0, 0))], background);
        Assert.Equal(new byte[] { 248, 0, 0, 255 }, result.AsSpan((28 * 256 + 4) * 4, 4).ToArray());
        Assert.Equal(background.AsSpan((20 * 256 + 4) * 4, 4).ToArray(), result.AsSpan((20 * 256 + 4) * 4, 4).ToArray());
    }

    [Fact]
    public void ExplicitCheckerMatchesTheDefaultPathWithPerDrawDithering()
    {
        var artwork = AuthoredGeneralBanner.Loaded();
        ImmutableArray<BannerRasterDraw> draws = [new(new(0, 0, 31), new(true, 1, 2)), new(new(2, 0, 31), new(false, 0, 0))];
        var checker = FiveBitGeneralBannerPresentation.Compose(artwork, []);
        Assert.Equal(FiveBitGeneralBannerPresentation.Compose(artwork, draws), FiveBitGeneralBannerPresentation.ComposeOver(artwork, draws, checker));
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(245759)][InlineData(245761)]
    public void BackgroundMustHaveExactlyOneNativeCanvas(int length)
        => Assert.Throws<ArgumentException>(() => FiveBitGeneralBannerPresentation.ComposeOver(AuthoredGeneralBanner.Loaded(), [], new byte[length]));

    [Theory]
    [InlineData(0, 1)][InlineData(1, 7)][InlineData(2, 255)][InlineData(3, 254)]
    public void AlphaAndLowBitsCannotBeSilentlyQuantized(int channel, byte value)
    {
        var background = Background(); background[100 * 4 + channel] = value;
        Assert.Throws<ArgumentException>(() => FiveBitGeneralBannerPresentation.ComposeOver(AuthoredGeneralBanner.Loaded(), [], background));
    }
}
