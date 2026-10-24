using System.Collections.Immutable;
using Fft.Content;
using Fft.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Fft.Client.Presentation;

public sealed record PreparedSceneDraw(string Kind, int SourceIndex, int PaletteBank,
    ImmutableArray<VertexPositionColorTexture> Vertices);

/// <summary>Fixed, authored inspection scene. No simulation transitions, original parsers or animation clock.</summary>
public sealed class PreparedSceneGeometry
{
    public const int CameraYaw = 0xe00;
    public static TilePosition UnitTile => new(3, 2, 0);
    public static Vector3 Target => new(112, 84, 182);
    public static Vector3 Eye => Target + new Vector3(-320, 360, -320);
    public const float ViewWidth = 630;
    public const float ViewHeight = 420;
    // Source Y points down. Reflecting it changes handedness; restore the native yaw's
    // horizontal camera row rather than silently mirroring the map in a right-handed look-at.
    public static Matrix View => Matrix.CreateLookAt(Eye, Target, Vector3.Up) * Matrix.CreateScale(-1, 1, 1);
    public static Matrix Projection => Matrix.CreateOrthographic(ViewWidth, ViewHeight, 1, 2000);
    public Vector3 UnitAnchor { get; }
    public ImmutableArray<PreparedSceneDraw> MapDraws { get; }
    public ImmutableArray<PreparedSceneDraw> PoseDraws { get; }
    public int HiddenPolygons { get; }

    private PreparedSceneGeometry(Vector3 anchor, ImmutableArray<PreparedSceneDraw> map,
        ImmutableArray<PreparedSceneDraw> pose, int hidden) =>
        (UnitAnchor, MapDraws, PoseDraws, HiddenPolygons) = (anchor, map, pose, hidden);

    // Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, battle_map_mesh.h.
    // These documented queue hide bits are distinct from lighting and from GPU backface culling.
    public static bool Visible(ushort flags, int yaw)
    {
        if (yaw is < 0 or > 4095) throw new ArgumentOutOfRangeException(nameof(yaw));
        int mask = 4 << (((yaw - 1) & 0xe00) >> 9);
        if (((yaw + 0x200) & 0x3ff) == 0) mask |= 0x400 << (yaw >> 10);
        return (flags & mask) == 0;
    }

    public static Vector3 World(MapVector source) => new(source.X, -source.Y, source.Z);

    /// <summary>Only the traced flat, dry, unblocked placement domain; not a general slope/water rule.</summary>
    public static Vector3 FlatAnchor(BattleMapDefinition terrain, TilePosition position)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        if (!terrain.TryGetTile(position, out var tile)) throw new InvalidDataException("Inspection anchor is outside the terrain.");
        if (tile.IsBlocked || tile.IsUntargetable || tile.CannotStop || tile.WaterDepth != 0 || tile.HalfHeight != 0 || tile.Slope != default)
            throw new NotSupportedException("Inspection placement requires flat, dry, unblocked terrain.");
        return new(28f * position.X + 14, 12f * tile.Height, 28f * position.Y + 14);
    }

    public static Vector3 Project(Vector3 world)
    {
        Vector3 clip = Vector3.Transform(world, View * Projection); // Orthographic W is always one.
        return new((clip.X + 1) * SceneLayout.CanvasWidth / 2,
            (1 - clip.Y) * SceneLayout.CanvasHeight / 2, clip.Z);
    }

    public static PreparedSceneGeometry Create(LoadedBattleMap map, LoadedSpritePose pose)
    {
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(pose);
        Vector3 anchor = FlatAnchor(map.Definition.ToBattleMap(), UnitTile);
        var mapDraws = ImmutableArray.CreateBuilder<PreparedSceneDraw>();
        int hidden = 0;
        foreach (var polygon in map.Definition.Polygons)
        {
            if (!Visible(polygon.Flags, CameraYaw)) { hidden++; continue; }
            var vertices = new VertexPositionColorTexture[polygon.Vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                bool textured = polygon.TexturePage.HasValue;
                // Explicit neutral texture preview: no GTE light modulation. Untextured native initialization is (0,0,1).
                Color color = textured ? Color.White : new Color(0, 0, 1);
                Vector2 uv = textured ? new((polygon.TexturePage!.Value * 256 + polygon.Uvs[i].U + 0.5f) / 1024,
                    (polygon.Uvs[i].V + 0.5f) / 256) : Vector2.Zero;
                vertices[i] = new(World(polygon.Vertices[i]), color, uv);
            }
            mapDraws.Add(new(polygon.Kind, polygon.SourceIndex, polygon.PaletteBank ?? -1, Triangles(vertices)));
        }
        var poseDraws = ImmutableArray.CreateBuilder<PreparedSceneDraw>();
        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, Target - Eye));
        Vector3 up = Vector3.Normalize(Vector3.Cross(Target - Eye, right));
        foreach (var part in pose.Definition.PartsBackToFront)
        {
            // Prepared signed offsets are relative to the projected unit origin, never the image bounding box.
            Vector3 At(float x, float y) => anchor + right * x - up * y;
            float left = part.U / 256f, top = part.V / 256f;
            float rightUv = (part.U + part.Width) / 256f, bottom = (part.V + part.Height) / 256f;
            if (part.FlipHorizontal) (left, rightUv) = (rightUv, left);
            if (part.FlipVertical) (top, bottom) = (bottom, top);
            poseDraws.Add(new("sprite-part", part.SourceIndex, -2, Triangles([
                new(At(part.X, part.Y), Color.White, new(left, top)),
                new(At(part.X + part.Width, part.Y), Color.White, new(rightUv, top)),
                new(At(part.X, part.Y + part.Height), Color.White, new(left, bottom)),
                new(At(part.X + part.Width, part.Y + part.Height), Color.White, new(rightUv, bottom))])));
        }
        return new(anchor, mapDraws.ToImmutable(), poseDraws.ToImmutable(), hidden);
    }

    // Native GPU quads are strip ordered. A perimeter fan would cross the floor.
    private static ImmutableArray<VertexPositionColorTexture> Triangles(VertexPositionColorTexture[] v) =>
        v.Length == 3 ? [v[0], v[1], v[2]] : [v[0], v[1], v[2], v[2], v[1], v[3]];

    /// <summary>Opaque map primitive: word zero discards; STP black and every other nonzero word stay opaque.</summary>
    public static Color PaletteColor(ushort word)
    {
        if (word == 0) return Color.Transparent;
        static byte Expand(int bits) => (byte)((bits << 3) | (bits >> 2));
        return new(Expand(word & 31), Expand((word >> 5) & 31), Expand((word >> 10) & 31), (byte)255);
    }

    public static Color[] PaletteTexture(LoadedBattleMap map, int bank)
    {
        if (bank is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(bank));
        if (map.TextureIndices.Length != PreparedBattleMap.TextureByteLength) throw new InvalidDataException("Invalid index atlas length.");
        var colors = new Color[map.TextureIndices.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            byte index = map.TextureIndices[i];
            if (index > 15) throw new InvalidDataException("Invalid map palette index.");
            colors[i] = PaletteColor(map.Definition.Palettes[bank * 16 + index]);
        }
        return colors;
    }
}
