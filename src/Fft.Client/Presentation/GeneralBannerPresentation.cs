using System.Buffers.Binary;
using System.Collections.Immutable;
using Zodiac.Content;
using Zodiac.Framework.Events;

namespace Fft.Client.Presentation;

public sealed record BannerDraw(int PieceIndex, int CropTop, int VisibleRows);

/// <summary>Pure accepted-snapshot projection and declared modern RGB8 compositor, never a task authority.</summary>
public static class GeneralBannerPresentation
{
    public static (int Offset, int Rows) Crop(int height, int amount, bool reveal)
    {
        if (height < 1 || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        amount = Math.Min(height, amount);
        return reveal ? (height - amount, amount) : (amount, height - amount);
    }
    public const string Policy = "256x240 authored checker background; point/half-open sampling; pixel-center vertical gradient; RGB555 bit replication; neutral-128 modulation; floor then RGB8 saturation; ordering-table traversal; no PS1 raster/dither/pixel-aspect or parity equivalence";

    public static ImmutableArray<BannerDraw> Project(GeneralBannerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsTerminal || state.ExitCause == GeneralBannerExitCause.CircleSkipped || state.RevealCompleted == 0) return [];
        var draws = ImmutableArray.CreateBuilder<BannerDraw>();
        if (state.HideCompleted > 0)
        {
            var hide = Crop(31, (state.HideCompleted - 1) * 8, false);
            for (int i = 0; i < 6; i++) draws.Add(new(i, hide.Offset, hide.Rows));
        }
        else
        {
            // Counts record the last submitted iteration, even when that iteration changed phase.
            var first = Crop(31, (state.RevealCompleted - 1) * 8, true);
            for (int i = 0; i < 3; i++) draws.Add(new(i, first.Offset, first.Rows));
            if (state.RevealCompleted >= 10)
            {
                var second = Crop(31, (state.RevealCompleted - 10) * 8, true);
                for (int i = 3; i < 6; i++) draws.Add(new(i, second.Offset, second.Rows));
            }
        }
        return draws.ToImmutable();
    }

    /// <summary>Source-loop helper: each visited height clamps the shared offset before the next visit.</summary>
    public static (ImmutableArray<int> Offsets, int Next) ClampShared(int offset, IEnumerable<int> heights)
    {
        var result = ImmutableArray.CreateBuilder<int>();
        foreach (int height in heights)
        {
            if (height < 0 || offset < 0) throw new ArgumentOutOfRangeException(nameof(heights));
            offset = Math.Min(offset, height);
            result.Add(offset);
        }
        return (result.ToImmutable(), checked(offset + 8));
    }

    public static (BannerRgb Rgb, bool Discard, bool Stp) DecodeColor(ushort word)
    {
        static byte Expand(int c) => (byte)((c << 3) | (c >> 2));
        return (new(Expand(word & 31), Expand((word >> 5) & 31), Expand((word >> 10) & 31)), word == 0, (word & 0x8000) != 0);
    }

    public static int Shade(int texture, int top, int bottom, int row, int height)
    {
        // Interpolate before modulation; one exact rational division floors the product, avoiding early RGB quantization.
        long color = (long)top * (2 * height - 2 * row - 1) + (long)bottom * (2 * row + 1);
        return (int)Math.Clamp(texture * color / (256L * height), 0, 255);
    }

    public static int Blend(int background, int source, bool stp, string material) => !stp ? source : material switch
    {
        "add" => Math.Min(255, background + source),
        "subtract" => Math.Max(0, background - source),
        _ => throw new ArgumentException("Unsupported banner material.", nameof(material)),
    };

    public static byte[] Compose(LoadedGeneralBanner banner, ImmutableArray<BannerDraw> draws)
    {
        byte[] rgba = new byte[256 * 240 * 4];
        for (int y = 0; y < 240; y++)
        for (int x = 0; x < 256; x++)
        {
            int at = (y * 256 + x) * 4, shade = ((x / 16 + y / 16) & 1) * 12;
            rgba[at] = (byte)(40 + shade); rgba[at + 1] = (byte)(56 + shade); rgba[at + 2] = (byte)(72 + shade); rgba[at + 3] = 255;
        }
        // Project supplies ascending source indices: head-insertion traversal, distinct from source calls.
        foreach (var draw in draws)
        {
            var p = banner.Definition.Pieces[draw.PieceIndex];
            if (draw.CropTop < 0 || draw.VisibleRows < 0 || draw.CropTop + draw.VisibleRows != p.Height)
                throw new ArgumentException("Invalid half-open crop.", nameof(draws));
            for (int row = 0; row < draw.VisibleRows; row++)
            for (int x = 0; x < p.Width; x++)
            {
                int index = banner.Indices[(p.V + draw.CropTop + row) * 256 + p.U + x];
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(banner.Palette.AsSpan(index * 2, 2));
                var color = DecodeColor(word);
                if (color.Discard) continue;
                int at = ((p.Y + draw.CropTop + row) * 256 + p.X + x) * 4;
                var top = p.Colors[0]; var bottom = p.Colors[2];
                rgba[at] = (byte)Blend(rgba[at], Shade(color.Rgb.R, top.R, bottom.R, row, draw.VisibleRows), color.Stp, p.Material);
                rgba[at + 1] = (byte)Blend(rgba[at + 1], Shade(color.Rgb.G, top.G, bottom.G, row, draw.VisibleRows), color.Stp, p.Material);
                rgba[at + 2] = (byte)Blend(rgba[at + 2], Shade(color.Rgb.B, top.B, bottom.B, row, draw.VisibleRows), color.Stp, p.Material);
            }
        }
        return rgba;
    }
}
