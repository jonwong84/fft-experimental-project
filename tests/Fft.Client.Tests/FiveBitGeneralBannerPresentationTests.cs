using System.Collections.Immutable;
using Fft.Client.Presentation;
using Fft.Content;
using Fft.Tests.Support;
using Xunit;

namespace Fft.Client.Tests;

public sealed class FiveBitGeneralBannerPresentationTests
{
    [Theory]
    [InlineData(31, 128, 0, 31)] [InlineData(31, 128, -4, 30)] [InlineData(31, 128, 3, 31)]
    [InlineData(31, 64, 0, 15)] [InlineData(31, 255, 3, 31)] [InlineData(0, 0, -4, 0)]
    public void FiveBitModulationQuantizesBeforeBlend(int texel, int vertex, int noise, int expected)
        => Assert.Equal(expected, FiveBitGeneralBannerPresentation.Shade5(texel, vertex, noise));

    [Theory]
    [InlineData(0, 10, 1, 4, 3)] [InlineData(128, 64, 1, 31, 126)]
    [InlineData(88, 168, 6, 31, 103)] [InlineData(0, 128, 0, 8, 0)] [InlineData(0, 128, 7, 8, 112)]
    public void GradientUsesQuantizedSignedSlopeAndColorBias(int top, int bottom, int row, int height, int expected)
        => Assert.Equal(expected, FiveBitGeneralBannerPresentation.VertexChannel(top, bottom, row, height));

    [Theory]
    [InlineData(30, 10, true, "add", 31)] [InlineData(3, 10, true, "subtract", 0)]
    [InlineData(20, 7, false, "add", 7)] [InlineData(20, 7, false, "subtract", 7)]
    public void BlendSaturatesEachFiveBitPassAndOpaqueTexelsReplace(int background, int source, bool stp, string material, int expected)
        => Assert.Equal(expected, FiveBitGeneralBannerPresentation.Blend5(background, source, stp, material));

    [Fact]
    public void PerPassSaturationAndTraversalOrderMatter()
    {
        int selected = FiveBitGeneralBannerPresentation.Blend5(10, 20, true, "subtract");
        selected = FiveBitGeneralBannerPresentation.Blend5(selected, 12, true, "add");
        Assert.Equal(12, selected);
        int reversed = FiveBitGeneralBannerPresentation.Blend5(10, 12, true, "add");
        Assert.Equal(2, FiveBitGeneralBannerPresentation.Blend5(reversed, 20, true, "subtract"));
    }

    [Fact]
    public void DitherPatternUsesExplicitPhysicalFramebufferPhase()
    {
        var artwork = Flat(31);
        int[,] expected = { { 240, 248, 240, 248 }, { 248, 240, 248, 240 }, { 240, 248, 240, 248 }, { 248, 240, 248, 240 } };
        var pixels = Compose(artwork, true);
        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 4; col++) Assert.Equal(expected[row, col], pixels[((20 + row) * 256 + 4 + col) * 4]);
        var shifted = FiveBitGeneralBannerPresentation.Compose(artwork, [new(new(0, 0, 31), new(true, 1, 0))]);
        Assert.Equal(248, shifted[(20 * 256 + 4) * 4]);
        Assert.Equal(pixels, FiveBitGeneralBannerPresentation.Compose(artwork, [new(new(0, 0, 31), new(true, 0, 240))]));
    }

    [Fact]
    public void NativeOutputIsZeroPaddedAndModernOutputRemainsIdentifiable()
    {
        var artwork = Flat(31);
        var native = Compose(artwork, false);
        var modern = GeneralBannerPresentation.Compose(artwork, [new(0, 0, 31)]);
        Assert.Equal(248, native[(20 * 256 + 4) * 4]); Assert.Equal(255, modern[(20 * 256 + 4) * 4]);
        Assert.All(native.Where((_, i) => i % 4 != 3), channel => Assert.Equal(0, channel & 7));
        Assert.All(native.Where((_, i) => i % 4 == 3), channel => Assert.Equal(255, channel));
    }

    [Theory]
    [InlineData(0)] [InlineData(32768)]
    public void TransparentWordAndStpBlackPreserveQuantizedBackground(int word)
    {
        var pixels = Compose(Flat(word), true);
        Assert.Equal(48, pixels[(20 * 256 + 4) * 4]);
    }

    [Fact]
    public void IndexZeroCanBeOpaqueInEitherMaterial()
    {
        var a = Flat(31) with { Indices = [.. new byte[51200]], Palette = [31, 0, .. new byte[30]] };
        foreach (int index in new[] { 0, 2 })
            Assert.Equal(248, FiveBitGeneralBannerPresentation.Compose(a, [new(new(index, 0, 31), new(false, 0, 0))])[(20 * 256 + 4) * 4]);
    }

    [Fact]
    public void CroppingSamplesOriginalRowsButUsesCurrentQuadGradient()
    {
        var a = Flat(31);
        a = a with { Definition = a.Definition with { Pieces = [.. a.Definition.Pieces.Select(p => p with
            { Colors = [new(0, 0, 0), new(0, 0, 0), new(128, 128, 128), new(128, 128, 128)] })] } };
        var cropped = FiveBitGeneralBannerPresentation.Compose(a, [new(new(0, 23, 8), new(false, 0, 0))]);
        Assert.Equal(0, cropped[(43 * 256 + 4) * 4]); Assert.Equal(216, cropped[(50 * 256 + 4) * 4]);
        var indices = new byte[51200]; indices[(128 + 30) * 256] = 1;
        a = Flat(31) with { Indices = [.. indices] };
        cropped = FiveBitGeneralBannerPresentation.Compose(a, [new(new(0, 23, 8), new(false, 0, 0))]);
        Assert.Equal(248, cropped[(50 * 256 + 4) * 4]); Assert.Equal(40, cropped[(43 * 256 + 4) * 4]);
    }

    [Fact]
    public void RepaintingAndAlternatingDrawStatesHaveNoCachedMaterialState()
    {
        var a = Flat(31); var first = Compose(a, true);
        Assert.NotEqual(first[(20 * 256 + 4) * 4], Compose(a, false)[(20 * 256 + 4) * 4]);
        Assert.Equal(first, Compose(a, true));
        var finalOff = FiveBitGeneralBannerPresentation.Compose(a,
            [new(new(0, 0, 31), new(true, 0, 0)), new(new(0, 0, 31), new(false, 0, 0))]);
        Assert.Equal(248, finalOff[(20 * 256 + 4) * 4]);
    }

    [Fact]
    public void HiddenDegenerateDrawsDoNotDivideByZeroOrManufacturePixels()
    {
        var a = Flat(31);
        Assert.Equal(FiveBitGeneralBannerPresentation.Compose(a, []),
            FiveBitGeneralBannerPresentation.Compose(a, [new(new(0, 31, 0), new(true, 0, 0))]));
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(769, 0)] [InlineData(0, -1)] [InlineData(0, 273)]
    public void FramebufferBoundsReject(int x, int y)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new BannerRasterState(false, x, y).Validate());

    [Fact]
    public void MissingStateAndMalformedDrawsReject()
    {
        var a = Flat(31);
        Assert.Throws<ArgumentNullException>(() => FiveBitGeneralBannerPresentation.Compose(a, [new(new(0, 0, 31), null!)]));
        foreach (var g in new[] { new BannerDraw(-1, 0, 31), new(6, 0, 31), new(0, -1, 32), new(0, 32, -1), new(0, 0, 30) })
            Assert.Throws<ArgumentException>(() => FiveBitGeneralBannerPresentation.Compose(a, [new(g, new(false, 0, 0))]));
        Assert.Throws<ArgumentException>(() => FiveBitGeneralBannerPresentation.Compose(a, default));
    }

    [Fact]
    public void FiveBitSelectionRequiresExplicitDitherAndKeepsLegacyArguments()
    {
        var legacy = BannerPreviewOptions.Parse(["--general-banner", "--content-root", "prepared", "--check", "scratch/check"]);
        Assert.False(legacy.FiveBit); Assert.Null(legacy.Dither); Assert.Equal("scratch/check", legacy.CheckRoot);
        foreach (string setting in new[] { "off", "on" })
        {
            var selected = BannerPreviewOptions.Parse(["--general-banner", "--content-root", "prepared", "--precision", "five-bit", "--dither", setting]);
            Assert.True(selected.FiveBit); Assert.Equal(setting == "on", selected.Dither);
        }
        Assert.Throws<ArgumentException>(() => BannerPreviewOptions.Parse(["--general-banner", "--content-root", "prepared", "--precision", "five-bit"]));
        Assert.Throws<ArgumentException>(() => BannerPreviewOptions.Parse(["--general-banner", "--content-root", "prepared", "--dither", "off"]));
        Assert.Throws<ArgumentException>(() => BannerPreviewOptions.Parse(["--general-banner", "--content-root", "prepared", "--precision", "five-bit", "--dither", "auto"]));
        Assert.Throws<ArgumentException>(() => BannerPreviewOptions.Parse(["--general-banner", "--content-root", "prepared", "--precision", "rgb8", "--precision", "five-bit"]));
    }

    private static LoadedGeneralBanner Flat(int word)
    {
        var a = AuthoredGeneralBanner.Loaded();
        return a with { Palette = [0, 0, (byte)word, (byte)(word >> 8), .. new byte[28]],
            Definition = a.Definition with { Pieces = [.. a.Definition.Pieces.Select(p => p with
                { Colors = [new(128, 128, 128), new(128, 128, 128), new(128, 128, 128), new(128, 128, 128)] })] } };
    }
    private static byte[] Compose(LoadedGeneralBanner artwork, bool dither)
        => FiveBitGeneralBannerPresentation.Compose(artwork, [new(new(0, 0, 31), new(dither, 0, 0))]);
}
