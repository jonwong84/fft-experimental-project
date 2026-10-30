using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Zodiac.AssetTool;

public sealed record DecodedReadyGlyph(int SourceIndex, int X, int Y, int U, int V, int Width, int Height,
    ImmutableArray<BannerVertex> Colors);
public sealed record DecodedReadyArtwork(ImmutableArray<byte> Indices, ImmutableArray<byte> Palette,
    ImmutableArray<DecodedReadyGlyph> Glyphs);

/// <summary>Offline image-0/palette-2 selection traced by ZOD-085; no general-banner crop conversion.</summary>
public static class ReadyArtworkDecoder
{
    public static (int X, int Y, int U, int V) Normalize(int sx, int sy, int dx, int dy)
        => (checked(dx + 128), checked(dy + 120), sx, sy);

    public static DecodedReadyArtwork Decode(ReadOnlySpan<byte> image, ReadOnlySpan<byte> quads,
        ReadOnlySpan<byte> range, ReadOnlySpan<byte> colors, ReadOnlySpan<byte> paletteRect)
    {
        if (image.Length != 0x6800 || quads.Length != 72 || colors.Length != 120 ||
            range.Length != 2 || range[0] != 0 || range[1] != 6 || paletteRect.Length != 8 ||
            BinaryPrimitives.ReadInt16LittleEndian(paletteRect) != 384 ||
            BinaryPrimitives.ReadInt16LittleEndian(paletteRect[2..]) != 254 ||
            BinaryPrimitives.ReadInt16LittleEndian(paletteRect[4..]) != 64 ||
            BinaryPrimitives.ReadInt16LittleEndian(paletteRect[6..]) != 2)
            throw new InvalidDataException("Expected image 0, palette rectangle and six Ready glyph records.");
        var glyphs = ImmutableArray.CreateBuilder<DecodedReadyGlyph>();
        for (int i = 0; i < 6; i++)
        {
            var q = quads.Slice(i * 12, 12);
            int sx = BinaryPrimitives.ReadInt16LittleEndian(q), sy = BinaryPrimitives.ReadInt16LittleEndian(q[2..]);
            int w = BinaryPrimitives.ReadInt16LittleEndian(q[4..]), h = BinaryPrimitives.ReadInt16LittleEndian(q[6..]);
            var n = Normalize(sx, sy, BinaryPrimitives.ReadInt16LittleEndian(q[8..]), BinaryPrimitives.ReadInt16LittleEndian(q[10..]));
            if (w is not (16 or 17) || h != 24 || n.X < 0 || n.X > 256 - w || n.Y < 0 || n.Y > 240 - h ||
                n.U < 0 || n.U > 255 - w || n.V < 0 || n.V > 200 - h)
                throw new InvalidDataException("Unsupported Ready glyph bounds; UV wrapping is excluded.");
            var vertices = ImmutableArray.CreateBuilder<BannerVertex>();
            for (int corner = 0; corner < 4; corner++)
            {
                int at = i * 20 + 4 + corner * 4;
                vertices.Add(new(colors[at], colors[at + 1], colors[at + 2]));
            }
            glyphs.Add(new(i, n.X, n.Y, n.U, n.V, w, h, vertices.ToImmutable()));
        }
        return new([.. GeneralBannerDecoder.Unpack(image[..0x6400])], [.. image.Slice(0x6440, 32)], glyphs.ToImmutable());
    }
}
