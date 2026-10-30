using System.Buffers.Binary;
using System.Collections.Immutable;
using Zodiac.Content;
using Zodiac.Framework.Events;

namespace Fft.Client.Presentation;

public sealed record ReadyRasterState(string Policy, bool DitherEnabled, int FramebufferX, int FramebufferY,
    int TextureWindowX, int TextureWindowY, int TextureWindowWidth, int TextureWindowHeight, bool MaskSet, bool MaskEvaluate)
{
    public static ReadyRasterState Selected { get; } = new(ReadyPresentation.Policy, false, 0, 0, 0, 0, 0, 0, false, false);
    public void Validate()
    {
        if (Policy != ReadyPresentation.Policy || DitherEnabled || FramebufferX is < 0 or > 768 || FramebufferY is < 0 or > 272 ||
            TextureWindowX != 0 || TextureWindowY != 0 || TextureWindowWidth != 0 || TextureWindowHeight != 0 || MaskSet || MaskEvaluate)
            throw new ArgumentException("Ready requires its explicit point/modulated/average, dither-off, window-zero, mask-off state.");
    }
}
public sealed record ReadyGlyphDraw(int GlyphIndex, int Scale, ReadyRasterState State);

/// <summary>Bounded accepted-snapshot Ready RGB5 CPU composition; source derivation is ZOD-085.</summary>
public static class ReadyPresentation
{
    public const string Policy = "ready-glyph-rgb5-v1";
    public static ImmutableArray<ReadyGlyphDraw> Project(ReadyDisplayState? state, ReadyRasterState raster)
    {
        ArgumentNullException.ThrowIfNull(raster);
        raster.Validate();
        return state?.DrawScale is { } scale
            ? [.. Enumerable.Range(0, 6).Reverse().Select(i => new ReadyGlyphDraw(i, scale, raster))] : [];
    }
    public static int ScaleChannel(int channel, int scale)
    {
        if (channel is < 0 or > 255 || scale is < 48 or > 128) throw new ArgumentOutOfRangeException(nameof(scale));
        return checked(channel * scale) / 128;
    }
    public static int VertexChannel(int a, int b, int c, int d, int x, int y, int width, int height)
    {
        if (a is < 0 or > 255 || b is < 0 or > 255 || c is < 0 or > 255 || d is < 0 or > 255 ||
            width is not (16 or 17) || height != 24 || x < 0 || x >= width || y < 0 || y >= height)
            throw new ArgumentOutOfRangeException(nameof(x));
        // Signed division truncates toward zero. The diagonal belongs to the BL-seeded second triangle.
        return checked(x * height < width * (height - y)
            ? (a * 4096 + 2048 + (b - a) * 4096 / width * x + (c - a) * 4096 / height * y) >> 12
            : (c * 4096 + 2048 + (d - c) * 4096 / width * x + (d - b) * 4096 / height * (y - height)) >> 12);
    }
    public static int Shade5(int texture, int color)
    {
        if (texture is < 0 or > 31 || color is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(texture));
        return Math.Clamp(((texture * color) >> 4) >> 3, 0, 31);
    }
    public static int Blend5(int background, int source, bool stp)
    {
        if (background is < 0 or > 31 || source is < 0 or > 31) throw new ArgumentOutOfRangeException(nameof(source));
        return stp ? (background + source) >> 1 : source;
    }
    public static byte[] Compose(LoadedReadyArtwork artwork, ImmutableArray<ReadyGlyphDraw> draws)
        => ComposeCore(artwork, draws, default);
    public static byte[] ComposeOver(LoadedReadyArtwork artwork, ImmutableArray<ReadyGlyphDraw> draws, ReadOnlySpan<byte> background)
    {
        if (background.Length != 256 * 240 * 4) throw new ArgumentException("Expected 256x240 RGBA background.", nameof(background));
        for (int at = 0; at < background.Length; at += 4)
            if (background[at + 3] != 255 || ((background[at] | background[at + 1] | background[at + 2]) & 7) != 0)
                throw new ArgumentException("Background requires opaque zero-padded RGB5.", nameof(background));
        return ComposeCore(artwork, draws, background);
    }
    private static byte[] ComposeCore(LoadedReadyArtwork artwork, ImmutableArray<ReadyGlyphDraw> draws, ReadOnlySpan<byte> background)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        artwork.Validate();
        if (draws.IsDefault || draws.Length > 6) throw new ArgumentException("Explicit bounded Ready draw list required.", nameof(draws));
        var seen = new HashSet<int>();
        foreach (var draw in draws)
        {
            if (draw is null || draw.State is null || draw.GlyphIndex is < 0 or >= 6 || !seen.Add(draw.GlyphIndex) || draw.Scale is < 48 or > 128)
                throw new ArgumentException("Invalid Ready draw identity, scale or state.", nameof(draws));
            draw.State.Validate();
        }
        byte[] rgba = new byte[256 * 240 * 4];
        for (int y = 0; y < 240; y++)
            for (int x = 0; x < 256; x++)
            {
                int at = (y * 256 + x) * 4;
                if (background.IsEmpty)
                {
                    int shade = ((x / 16 + y / 16) & 1) * 12;
                    rgba[at] = (byte)((40 + shade) >> 3); rgba[at + 1] = (byte)((56 + shade) >> 3); rgba[at + 2] = (byte)((72 + shade) >> 3);
                }
                else for (int ch = 0; ch < 3; ch++) rgba[at + ch] = (byte)(background[at + ch] >> 3);
                rgba[at + 3] = 255;
            }
        foreach (var draw in draws)
        {
            var g = artwork.Definition.Glyphs[draw.GlyphIndex];
            var colors = g.Colors.Select(c => new[] { ScaleChannel(c.R, draw.Scale), ScaleChannel(c.G, draw.Scale), ScaleChannel(c.B, draw.Scale) }).ToArray();
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    int index = artwork.Indices[(g.V + y) * 256 + g.U + x];
                    ushort word = BinaryPrimitives.ReadUInt16LittleEndian(artwork.Palette.AsSpan(index * 2, 2));
                    if (word == 0) continue;
                    int at = ((g.Y + y) * 256 + g.X + x) * 4;
                    for (int ch = 0; ch < 3; ch++)
                    {
                        int color = VertexChannel(colors[0][ch], colors[1][ch], colors[2][ch], colors[3][ch], x, y, g.Width, g.Height);
                        int source = Shade5((word >> (5 * ch)) & 31, color);
                        rgba[at + ch] = (byte)Blend5(rgba[at + ch], source, (word & 0x8000) != 0);
                    }
                }
        }
        for (int at = 0; at < rgba.Length; at += 4) { rgba[at] <<= 3; rgba[at + 1] <<= 3; rgba[at + 2] <<= 3; }
        return rgba;
    }
}
