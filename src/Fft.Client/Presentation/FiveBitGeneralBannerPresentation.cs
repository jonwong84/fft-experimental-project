using System.Buffers.Binary;
using System.Collections.Immutable;
using Zodiac.Content;

namespace Fft.Client.Presentation;

/// <summary>Physical framebuffer origin controls dither phase; state is explicit for each draw.</summary>
public sealed record BannerRasterState(bool DitherEnabled, int FramebufferX, int FramebufferY)
{
    public void Validate()
    {
        if (FramebufferX is < 0 or > 768 || FramebufferY is < 0 or > 272)
            throw new ArgumentOutOfRangeException(nameof(FramebufferX), "The 256x240 canvas must fit the 1024x512 framebuffer.");
    }
}

public sealed record BannerRasterDraw(BannerDraw Geometry, BannerRasterState State);

/// <summary>Bounded axis-aligned five-bit material profile, not a general PS1 GPU.</summary>
public static class FiveBitGeneralBannerPresentation
{
    public const string Policy = "256x240 authored checker quantized to RGB5; point/half-open sampling; Q12 triangle-seeded vertical colors with half-unit color bias; RGB5 texture modulation and per-pass saturation; explicit per-draw dither/framebuffer phase; zero-padded RGB5 scanout; no general triangle, timing, pixel-aspect or hardware equivalence";
    private static readonly int[,] Dither = { { -4, 0, -3, 1 }, { 2, -2, 3, -1 }, { -3, 1, -4, 0 }, { 3, -1, 2, -2 } };

    public static int VertexChannel(int top, int bottom, int row, int height)
    {
        if (top is < 0 or > 255 || bottom is < 0 or > 255 || height is < 1 or > 256 || row < 0 || row >= height)
            throw new ArgumentOutOfRangeException(nameof(row));
        // Signed division truncates toward zero, including falling gradients.
        int step = (bottom - top) * 4096 / height;
        return (top * 4096 + 2048 + step * row) >> 12;
    }

    /// <summary>Color for the supported TL/TR/BL/BR rectangle with equal colors on each horizontal edge.</summary>
    public static int QuadVertexChannel(int top, int bottom, int column, int row, int width, int height)
    {
        if (width is < 1 or > 256 || column < 0 || column >= width)
            throw new ArgumentOutOfRangeException(nameof(column));
        int firstTriangle = VertexChannel(top, bottom, row, height);
        // TL/TR/BL seeds at TL; TR/BL/BR seeds at BL. The shared diagonal belongs to the second triangle.
        // See docs/development/banner-triangle-colors.md for the source trace and bounded arithmetic reduction.
        if (column * height < width * (height - row)) return firstTriangle;
        int step = (bottom - top) * 4096 / height;
        return (bottom * 4096 + 2048 + step * (row - height)) >> 12;
    }

    public static int Shade5(int texture, int vertex, int dither)
    {
        if (texture is < 0 or > 31 || vertex is < 0 or > 255 || dither is < -4 or > 3)
            throw new ArgumentOutOfRangeException(nameof(texture));
        return Math.Clamp((((texture * vertex) >> 4) + dither) >> 3, 0, 31);
    }

    public static int Blend5(int background, int source, bool stp, string material)
    {
        if (background is < 0 or > 31 || source is < 0 or > 31 || material is not ("add" or "subtract"))
            throw new ArgumentOutOfRangeException(nameof(source));
        return !stp ? source : material == "add" ? Math.Min(31, background + source) : Math.Max(0, background - source);
    }

    public static byte[] Compose(LoadedGeneralBanner banner, ImmutableArray<BannerRasterDraw> draws)
        => ComposeCore(banner, draws, default);

    /// <summary>Compose over explicit opaque, zero-padded RGB5 scanout pixels; no implicit quantization.</summary>
    public static byte[] ComposeOver(LoadedGeneralBanner banner, ImmutableArray<BannerRasterDraw> draws,
        ReadOnlySpan<byte> backgroundRgba)
    {
        if (backgroundRgba.Length != 256 * 240 * 4)
            throw new ArgumentException("Expected a 256x240 RGBA background.", nameof(backgroundRgba));
        for (int at = 0; at < backgroundRgba.Length; at += 4)
            if (backgroundRgba[at + 3] != 255 ||
                ((backgroundRgba[at] | backgroundRgba[at + 1] | backgroundRgba[at + 2]) & 7) != 0)
                throw new ArgumentException("Background must be opaque zero-padded RGB5.", nameof(backgroundRgba));
        return ComposeCore(banner, draws, backgroundRgba);
    }

    private static byte[] ComposeCore(LoadedGeneralBanner banner, ImmutableArray<BannerRasterDraw> draws,
        ReadOnlySpan<byte> backgroundRgba)
    {
        ArgumentNullException.ThrowIfNull(banner);
        banner.Definition.Validate();
        if (draws.IsDefault) throw new ArgumentException("Explicit draw list required.", nameof(draws));
        // All state/geometry is checked before composition. No state is cached between paints.
        foreach (var draw in draws)
        {
            ArgumentNullException.ThrowIfNull(draw);
            ArgumentNullException.ThrowIfNull(draw.Geometry);
            ArgumentNullException.ThrowIfNull(draw.State);
            draw.State.Validate();
            var g = draw.Geometry;
            if (g.PieceIndex is < 0 or >= 6 || g.CropTop < 0 || g.VisibleRows < 0 ||
                g.CropTop > 31 || g.VisibleRows != 31 - g.CropTop)
                throw new ArgumentException("Invalid banner draw/crop.", nameof(draws));
        }
        byte[] rgba = new byte[256 * 240 * 4];
        for (int y = 0; y < 240; y++)
        for (int x = 0; x < 256; x++)
        {
            int at = (y * 256 + x) * 4;
            if (backgroundRgba.IsEmpty)
            {
                int shade = ((x / 16 + y / 16) & 1) * 12;
                rgba[at] = (byte)((40 + shade) >> 3); rgba[at + 1] = (byte)((56 + shade) >> 3);
                rgba[at + 2] = (byte)((72 + shade) >> 3);
            }
            else
                for (int channel = 0; channel < 3; channel++) rgba[at + channel] = (byte)(backgroundRgba[at + channel] >> 3);
            rgba[at + 3] = 255;
        }
        foreach (var draw in draws)
        {
            var g = draw.Geometry;
            var p = banner.Definition.Pieces[g.PieceIndex];
            for (int row = 0; row < g.VisibleRows; row++)
            {
                var top = p.Colors[0]; var bottom = p.Colors[2];
                for (int x = 0; x < p.Width; x++)
                {
                    int index = banner.Indices[(p.V + g.CropTop + row) * 256 + p.U + x];
                    ushort word = BinaryPrimitives.ReadUInt16LittleEndian(banner.Palette.AsSpan(index * 2, 2));
                    if (word == 0) continue;
                    int red = QuadVertexChannel(top.R, bottom.R, x, row, p.Width, g.VisibleRows);
                    int green = QuadVertexChannel(top.G, bottom.G, x, row, p.Width, g.VisibleRows);
                    int blue = QuadVertexChannel(top.B, bottom.B, x, row, p.Width, g.VisibleRows);
                    int screenX = p.X + x, screenY = p.Y + g.CropTop + row;
                    int noise = draw.State.DitherEnabled ? Dither[(draw.State.FramebufferY + screenY) & 3, (draw.State.FramebufferX + screenX) & 3] : 0;
                    int at = (screenY * 256 + screenX) * 4;
                    bool stp = (word & 0x8000) != 0;
                    rgba[at] = (byte)Blend5(rgba[at], Shade5(word & 31, red, noise), stp, p.Material);
                    rgba[at + 1] = (byte)Blend5(rgba[at + 1], Shade5((word >> 5) & 31, green, noise), stp, p.Material);
                    rgba[at + 2] = (byte)Blend5(rgba[at + 2], Shade5((word >> 10) & 31, blue, noise), stp, p.Material);
                }
            }
        }
        for (int at = 0; at < rgba.Length; at += 4)
        { rgba[at] <<= 3; rgba[at + 1] <<= 3; rgba[at + 2] <<= 3; }
        return rgba;
    }
}
