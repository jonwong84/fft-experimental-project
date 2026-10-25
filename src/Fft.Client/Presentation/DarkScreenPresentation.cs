using System.Collections.Immutable;
using Zodiac.Content;
using Zodiac.Framework.Events;

namespace Fft.Client.Presentation;

public readonly record struct DarkScreenPoint(int X, int Y);
public sealed record DarkScreenQuad(DarkScreenPoint A, DarkScreenPoint B, DarkScreenPoint C, DarkScreenPoint D,
    int Red = 48, int Green = 40, int Blue = 16);

/// <summary>Selected integer-sample/top-left flat RGB5 policy; original entry/release pixels remain unverified.</summary>
public static class DarkScreenPresentation
{
    public const string Policy = "untextured-rgb5-flat-quad-v1";
    public static ImmutableArray<DarkScreenQuad> Project(PreparedDarkScreen definition, DarkScreenState? state)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        if (state is null) return [];
        if (state.Definition.Id.Value != definition.Id || state.OwnerId.Value != definition.OwnerId || !state.Definition.Tiles.SequenceEqual(definition.Tiles))
            throw new ArgumentException("Accepted worker is not bound to this prepared definition.", nameof(state));
        if (state.Draw.Kind == DarkScreenDrawKind.None) return [];
        if (state.Draw.Kind == DarkScreenDrawKind.Flash) return [new(new(0, 0), new(256, 0), new(0, 256), new(256, 256))];
        // Native append prepends: reverse the worker's row-major submission list before rasterizing.
        return [.. state.Draw.Tiles.Reverse().Select(d => Quad(definition, d))];
    }
    public static DarkScreenQuad Quad(PreparedDarkScreen definition, DarkScreenTileDraw draw)
    {
        if (draw.TileIndex is < 0 or >= 288 || draw.Angle is < 0 or > 512 || draw.Angle % 64 != 0 ||
            draw.Radius is < 0 or > 48 || draw.Radius % 4 != 0) throw new ArgumentException("Unsupported selected projection.", nameof(draw));
        var tile = definition.Tiles[draw.TileIndex]; var r = definition.Rotations[draw.Angle / 64];
        // Signed C# division truncates toward zero, including negative Q12 axes.
        int ax = r.AX * draw.Radius / 16384, ay = r.AY * draw.Radius / 16384;
        int bx = r.BX * draw.Radius / 16384, by = r.BY * draw.Radius / 16384;
        return new(new(tile.X + ax, tile.Y + ay), new(tile.X + bx, tile.Y + by), new(tile.X - bx, tile.Y - by), new(tile.X - ax, tile.Y - ay));
    }
    public static byte[] CheckerBackground()
    {
        var rgba = new byte[256 * 240 * 4];
        for (int y = 0; y < 240; y++) for (int x = 0; x < 256; x++)
        {
            int at = (y * 256 + x) * 4, shade = ((x / 16 + y / 16) & 1) * 16;
            rgba[at] = (byte)(64 + shade); rgba[at + 1] = (byte)(96 + shade); rgba[at + 2] = (byte)(128 + shade); rgba[at + 3] = 255;
        }
        return rgba;
    }
    public static byte[] Compose(ImmutableArray<DarkScreenQuad> draws, ReadOnlySpan<byte> background)
    {
        if (draws.IsDefault || draws.Length > 288) throw new ArgumentException("Explicit bounded draw list required.", nameof(draws));
        if (background.Length != 256 * 240 * 4) throw new ArgumentException("Expected explicit 256x240 RGBA background.", nameof(background));
        for (int at = 0; at < background.Length; at += 4)
            if (background[at + 3] != 255 || ((background[at] | background[at + 1] | background[at + 2]) & 7) != 0)
                throw new ArgumentException("Background must be opaque zero-padded RGB5.", nameof(background));
        foreach (var q in draws)
            if (q is null || new[] { q.A, q.B, q.C, q.D }.Any(p => p.X is < -1024 or > 1024 || p.Y is < -1024 or > 1024) ||
                q.Red is < 0 or > 255 || q.Green is < 0 or > 255 || q.Blue is < 0 or > 255)
                throw new ArgumentException("Invalid bounded flat quad.", nameof(draws));
        var rgba = background.ToArray();
        foreach (var q in draws)
        {
            Triangle(q.A, q.B, q.C, q, rgba);
            Triangle(q.B, q.C, q.D, q, rgba);
        }
        return rgba;
    }
    private static void Triangle(DarkScreenPoint a, DarkScreenPoint b, DarkScreenPoint c, DarkScreenQuad quad, byte[] rgba)
    {
        int area = Edge(a, b, c);
        if (area == 0) return;
        if (area < 0) (b, c) = (c, b);
        int left = Math.Max(0, Math.Min(a.X, Math.Min(b.X, c.X))), right = Math.Min(255, Math.Max(a.X, Math.Max(b.X, c.X)));
        int top = Math.Max(0, Math.Min(a.Y, Math.Min(b.Y, c.Y))), bottom = Math.Min(239, Math.Max(a.Y, Math.Max(b.Y, c.Y)));
        for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++)
        {
            var p = new DarkScreenPoint(x, y);
            if (!Inside(a, b, p) || !Inside(b, c, p) || !Inside(c, a, p)) continue;
            int at = (y * 256 + x) * 4;
            rgba[at] = (byte)average(rgba[at], quad.Red);
            rgba[at + 1] = (byte)average(rgba[at + 1], quad.Green);
            rgba[at + 2] = (byte)average(rgba[at + 2], quad.Blue);
        }
        static int average(byte background, int source) => (((background >> 3) + (source >> 3)) >> 1) << 3;
    }
    private static int Edge(DarkScreenPoint a, DarkScreenPoint b, DarkScreenPoint p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
    private static bool Inside(DarkScreenPoint a, DarkScreenPoint b, DarkScreenPoint p)
    {
        int edge = Edge(a, b, p);
        return edge > 0 || edge == 0 && (b.Y < a.Y || b.Y == a.Y && b.X > a.X);
    }
}
