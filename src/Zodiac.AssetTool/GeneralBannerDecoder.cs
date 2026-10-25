using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Zodiac.AssetTool;

public sealed record BannerVertex(byte R, byte G, byte B);
public sealed record DecodedBannerPiece(int SourceIndex, int X, int Y, int U, int V, int Width, int Height,
    string Material, ImmutableArray<BannerVertex> Colors);
public sealed record DecodedGeneralBanner(ImmutableArray<byte> Indices, ImmutableArray<byte> Palette,
    ImmutableArray<DecodedBannerPiece> Pieces);

/// <summary>Offline selected general-banner layout only; no upload suffix or packet parity emulation.</summary>
public static class GeneralBannerDecoder
{
    public static byte[] Unpack(ReadOnlySpan<byte> pixels)
    {
        if (pixels.Length != 0x6400) throw new InvalidDataException("Expected 256x200 packed 4bpp pixels.");
        byte[] result = new byte[51200];
        for (int i = 0; i < pixels.Length; i++)
        {
            result[2 * i] = (byte)(pixels[i] & 15);
            result[2 * i + 1] = (byte)(pixels[i] >> 4);
        }
        return result;
    }

    public static (int X, int Y, int U, int V) Normalize(int sx, int sy, int dx, int dy)
        => (checked(dx + 128), checked(dy + 120), sx & 255, (sy - 128) & 255);

    public static DecodedGeneralBanner Decode(ReadOnlySpan<byte> image, ReadOnlySpan<byte> quads,
        ReadOnlySpan<byte> pages, ReadOnlySpan<byte> range, ReadOnlySpan<byte> colors)
    {
        if (image.Length != 0x6800 || quads.Length != 72 || pages.Length != 24 || colors.Length != 120 ||
            range.Length != 2 || range[0] != 0 || range[1] != 6)
            throw new InvalidDataException("Only image 0's six-piece general layout is supported.");
        var pieces = ImmutableArray.CreateBuilder<DecodedBannerPiece>();
        for (int i = 0; i < 6; i++)
        {
            var q = quads.Slice(i * 12, 12);
            int sx = BinaryPrimitives.ReadInt16LittleEndian(q), sy = BinaryPrimitives.ReadInt16LittleEndian(q[2..]);
            int w = BinaryPrimitives.ReadInt16LittleEndian(q[4..]), h = BinaryPrimitives.ReadInt16LittleEndian(q[6..]);
            int dx = BinaryPrimitives.ReadInt16LittleEndian(q[8..]), dy = BinaryPrimitives.ReadInt16LittleEndian(q[10..]);
            var n = Normalize(sx, sy, dx, dy);
            int blend = pages[i * 4 + 2] & 0x60;
            if (w != 232 || h != 31 || n.U + w > 256 || n.V + h > 200 || n.X < 0 || n.X + w > 256 || n.Y < 0 || n.Y + h > 240 ||
                blend != (i % 3 == 2 ? 0x20 : 0x40))
                throw new InvalidDataException("Unsupported normalized general-banner geometry/material.");
            var vertices = ImmutableArray.CreateBuilder<BannerVertex>();
            for (int v = 0; v < 4; v++)
            {
                int at = i * 20 + 4 + v * 4;
                vertices.Add(new(colors[at], colors[at + 1], colors[at + 2]));
            }
            if (vertices[0] != vertices[1] || vertices[2] != vertices[3])
                throw new InvalidDataException("Only vertical gradients are supported.");
            pieces.Add(new(i, n.X, n.Y, n.U, n.V, w, h, blend == 0x20 ? "add" : "subtract", vertices.ToImmutable()));
        }
        return new([.. Unpack(image[..0x6400])], [.. image.Slice(0x6480, 32)], pieces.ToImmutable());
    }
}
