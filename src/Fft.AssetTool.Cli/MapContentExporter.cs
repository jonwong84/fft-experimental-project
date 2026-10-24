using System.Collections.Immutable;
using Fft.Content;
using Fft.Core;

namespace Fft.AssetTool.Cli;

/// <summary>Maps offline original records into normalized prepared data; runtime never parses source chunks.</summary>
public static class MapContentExporter
{
    public static PreparedBattleMap Create(DecodedBattleMap map, AssetId id, AssetId textureId, MapSource source)
    {
        ArgumentNullException.ThrowIfNull(map);
        var polygons = map.Polygons.Select(p =>
        {
            bool textured = p.Kind.StartsWith("textured-", StringComparison.Ordinal);
            var uvs = ImmutableArray.CreateBuilder<MapUv>();
            if (textured)
            {
                foreach (int index in p.Vertices.Length == 3 ? new[] { 0, 2, 4 } : new[] { 0, 2, 4, 5 })
                    uvs.Add(new((byte)p.Material[index], (byte)(p.Material[index] >> 8)));
            }
            MapTerrainReference? terrain = p.TerrainTag is null or 0xfffe ? null :
                new(p.TerrainTag.Value >> 8, (p.TerrainTag.Value >> 1) & 127, p.TerrainTag.Value & 1);
            return new MapPolygon(p.Kind, p.SourceIndex, p.Vertices.Select(v => new MapVector(v.X, v.Y, v.Z)).ToImmutableArray(),
                p.Normals.Select(v => new MapVector(v.X, v.Y, v.Z)).ToImmutableArray(), uvs.ToImmutable(),
                textured ? (p.Material[3] & 15) - 12 : null, textured ? p.Material[1] & 63 : null,
                p.Flags, terrain, p.Material, p.TerrainTag);
        });
        var tiles = map.Tiles.Select(tile =>
        {
            var b = tile.Bytes;
            int surface = b[0] & 63;
            int movement = (b[6] & 3) | ((b[6] & 128) == 0 ? 0 : 4);
            return new MapTile($"base:terrain/ps1/{surface:x2}", surface, b[2], b[3] & 31, b[3] >> 5,
                b[4] & 3, (b[4] >> 2) & 3, (b[4] >> 4) & 3, (b[4] >> 6) & 3, b[5] & 31, movement, b);
        });
        var presentation = map.Presentation;
        return new(id, textureId, map.Width, map.Depth, polygons, tiles, map.Palettes,
            new(presentation.LightMatrices, presentation.AmbientAndGradient, presentation.TextureAnimations,
                presentation.PaletteAnimationFrames, presentation.MarkedTilePalettes), source);
    }
}
