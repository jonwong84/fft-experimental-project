using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Fft.Core;

namespace Fft.Content;

public sealed record MapVector(short X, short Y, short Z);
public sealed record MapUv(byte U, byte V);
public sealed record MapTerrainReference(int X, int Y, int Layer);
public sealed record MapPolygon(string Kind, int SourceIndex, ImmutableArray<MapVector> Vertices,
    ImmutableArray<MapVector> Normals, ImmutableArray<MapUv> Uvs, int? TexturePage, int? PaletteBank,
    ushort Flags, MapTerrainReference? Terrain, ImmutableArray<ushort> SourceMaterial, ushort? SourceTerrainTag);
public sealed record MapTile(string SurfaceId, int SourceSurface, int Height, int HalfHeight, int WaterDepth,
    int SlopeEast, int SlopeWest, int SlopeSouth, int SlopeNorth, int CeilingDepth, int MovementFlags,
    ImmutableArray<byte> SourceBytes);
public sealed record MapPresentation(ImmutableArray<short> LightMatrices, ImmutableArray<byte> AmbientAndGradient,
    ImmutableArray<byte> TextureAnimations, ImmutableArray<ushort> PaletteAnimationFrames,
    ImmutableArray<ushort> MarkedTilePalettes);
public sealed record MapSource(string Edition, string ReferenceRevision, string GnsSha256, string MeshSha256, string TextureSha256);
public sealed record LoadedBattleMap(PreparedBattleMap Definition, ImmutableArray<byte> TextureIndices);

/// <summary>Prepared day/dry default scene, with presentation frozen at base upload. No original file parsing.</summary>
public sealed class PreparedBattleMap
{
    public const int MaxMetadataBytes = 4 * 1024 * 1024;
    public const int TextureByteLength = 1024 * 256;
    public const string Format = "fft.prepared-battle-map";
    public const string Profile = "SCUS-94221/day-dry/default/frozen-base";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AssetId Id { get; }
    public AssetId TextureId { get; }
    public int Width { get; }
    public int Depth { get; }
    public ImmutableArray<MapPolygon> Polygons { get; }
    public ImmutableArray<MapTile> Tiles { get; }
    public ImmutableArray<ushort> Palettes { get; }
    public MapPresentation Presentation { get; }
    public MapSource Source { get; }

    public PreparedBattleMap(AssetId id, AssetId textureId, int width, int depth, IEnumerable<MapPolygon> polygons,
        IEnumerable<MapTile> tiles, IEnumerable<ushort> palettes, MapPresentation presentation, MapSource source)
    {
        Check(!string.IsNullOrWhiteSpace(id.Value) && !string.IsNullOrWhiteSpace(textureId.Value) && id != textureId, "Distinct map and texture IDs are required.");
        Check(width > 0 && depth > 0 && (long)width * depth <= 256, "Map dimensions require 1..256 tiles per source layer.");
        ArgumentNullException.ThrowIfNull(polygons); ArgumentNullException.ThrowIfNull(tiles); ArgumentNullException.ThrowIfNull(palettes);
        ArgumentNullException.ThrowIfNull(presentation); ArgumentNullException.ThrowIfNull(source);
        Polygons = polygons.Take(1601).ToImmutableArray();
        Tiles = tiles.Take(513).ToImmutableArray();
        Palettes = palettes.Take(257).ToImmutableArray();
        Check(Polygons.Length <= 1600, "Too many polygons.");
        Check(Tiles.Length == width * depth * 2, "Exactly two dense layer-major terrain layers are required.");
        Check(Palettes.Length == 256, "Exactly 256 palette entries are required.");
        var seen = new HashSet<(string, int)>();
        foreach (var polygon in Polygons)
        {
            ArgumentNullException.ThrowIfNull(polygon);
            var (vertices, capacity, textured) = polygon.Kind switch
            {
                "textured-triangle" => (3, 512, true), "textured-quad" => (4, 768, true),
                "untextured-triangle" => (3, 64, false), "untextured-quad" => (4, 256, false),
                _ => throw new InvalidDataException("Unknown polygon kind."),
            };
            Check(polygon.SourceIndex >= 0 && polygon.SourceIndex < capacity && seen.Add((polygon.Kind, polygon.SourceIndex)), "Invalid or duplicate polygon source index.");
            Length(polygon.Vertices, vertices, "vertices");
            Length(polygon.Normals, textured ? vertices : 0, "normals");
            Length(polygon.Uvs, textured ? vertices : 0, "UVs");
            Check(polygon.Vertices.All(v => v is not null) && polygon.Normals.All(v => v is not null) && polygon.Uvs.All(v => v is not null), "Null polygon coordinate.");
            Length(polygon.SourceMaterial, textured ? vertices + 2 : 2, "source material");
            Check(textured ? polygon.TexturePage is >= 0 and <= 3 && polygon.PaletteBank is >= 0 and <= 15 && polygon.SourceTerrainTag.HasValue
                : polygon.TexturePage is null && polygon.PaletteBank is null && polygon.Terrain is null && polygon.SourceTerrainTag is null,
                "Invalid textured/untextured metadata.");
            if (polygon.Terrain is { } terrain)
                Check(terrain.X >= 0 && terrain.X < width && terrain.Y >= 0 && terrain.Y < depth && terrain.Layer is >= 0 and <= 1, "Terrain reference outside map.");
        }
        foreach (var tile in Tiles)
        {
            ArgumentNullException.ThrowIfNull(tile);
            Check(tile.SourceSurface is >= 0 and <= 63 && tile.SurfaceId == $"base:terrain/ps1/{tile.SourceSurface:x2}", "Invalid stable surface identity.");
            Check(tile.Height is >= 0 and <= 255 && tile.HalfHeight is >= 0 and <= 31 && tile.WaterDepth is >= 0 and <= 7 && tile.CeilingDepth is >= 0 and <= 31, "Terrain field outside source range.");
            Check(tile.SlopeEast is >= 0 and <= 3 && tile.SlopeWest is >= 0 and <= 3 && tile.SlopeSouth is >= 0 and <= 3 && tile.SlopeNorth is >= 0 and <= 3 && tile.MovementFlags is >= 0 and <= 7, "Invalid normalized terrain flags or slopes.");
            Length(tile.SourceBytes, 8, "opaque source tile bytes");
        }
        Length(presentation.LightMatrices, 18, "light matrices");
        Length(presentation.AmbientAndGradient, 9, "ambient/gradient");
        Length(presentation.TextureAnimations, 640, "texture animations");
        Length(presentation.PaletteAnimationFrames, 256, "palette animation frames");
        Length(presentation.MarkedTilePalettes, 256, "marked tile palettes");
        Check(source.Edition == "SCUS-94221" && Hex(source.ReferenceRevision, 40) && Hex(source.GnsSha256, 64) && Hex(source.MeshSha256, 64) && Hex(source.TextureSha256, 64), "Invalid source identities.");
        Id = id; TextureId = textureId; Width = width; Depth = depth; Presentation = presentation; Source = source;
    }

    public byte[] ToUtf8()
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(Format, 1, Profile, Id.Value, TextureId.Value,
            Width, Depth, Polygons, Tiles, Palettes, Presentation, Source), Options);
        Check(bytes.Length <= MaxMetadataBytes, "Prepared metadata exceeds 4MiB.");
        return bytes;
    }

    public static PreparedBattleMap Parse(ReadOnlyMemory<byte> bytes)
    {
        Check(bytes.Length <= MaxMetadataBytes, "Prepared metadata exceeds 4MiB.");
        try
        {
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            Shape(json.RootElement, typeof(Document), "$", nullable: false);
            var document = json.RootElement.Deserialize<Document>(Options)!;
            Check(document.Format == Format && document.Version == 1 && document.Profile == Profile, "Unsupported prepared map profile or version.");
            return new(new(document.Id), new(document.TextureId), document.Width, document.Depth, document.Polygons,
                document.Tiles, document.Palettes, document.Presentation, document.Source);
        }
        catch (JsonException exception) { throw new InvalidDataException("Invalid prepared map JSON.", exception); }
    }

    public BattleMapDefinition ToBattleMap() => new(Width, Depth, 2, Tiles.Select(tile =>
        new BattleTile(new(tile.SurfaceId), tile.Height, tile.HalfHeight, tile.WaterDepth,
            new TileSlope(tile.SlopeEast, tile.SlopeWest, tile.SlopeSouth, tile.SlopeNorth), tile.CeilingDepth,
            (BattleTileMovementFlags)tile.MovementFlags)).ToArray());

    public static LoadedBattleMap Load(LooseAssetStore store, AssetId id)
    {
        ArgumentNullException.ThrowIfNull(store);
        var definition = Parse(store.ReadAllBytes(id, MaxMetadataBytes));
        Check(definition.Id == id, "Prepared map ID does not match requested asset.");
        var texture = store.ReadAllBytes(definition.TextureId, TextureByteLength);
        Check(texture.Length == TextureByteLength && texture.All(value => value <= 15), "Texture must contain exactly 1024x256 four-bit indices.");
        return new(definition, texture.ToImmutableArray());
    }

    private sealed record Document(string Format, int Version, string Profile, string Id, string TextureId,
        int Width, int Depth, ImmutableArray<MapPolygon> Polygons, ImmutableArray<MapTile> Tiles,
        ImmutableArray<ushort> Palettes, MapPresentation Presentation, MapSource Source);

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static bool Hex(string? value, int length) => value?.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Length<T>(ImmutableArray<T> values, int length, string name) => Check(!values.IsDefault && values.Length == length, $"Invalid {name} length; expected {length}.");

    // Explicit recursive shape validation rejects missing/default fields, unknown fields and duplicates
    // before typed deserialization. Raw byte arrays remain arrays, never encoded source structures.
    private static void Shape(JsonElement element, Type type, string path, bool nullable)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (element.ValueKind == JsonValueKind.Null) { Check(nullable || underlying is not null, $"Null {path}."); return; }
        type = underlying ?? type;
        if (type == typeof(string)) { Check(element.ValueKind == JsonValueKind.String, $"Expected string at {path}."); return; }
        if (type.IsPrimitive) { Check(element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _), $"Expected integer at {path}."); return; }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>))
        {
            Check(element.ValueKind == JsonValueKind.Array, $"Expected array at {path}.");
            foreach (var child in element.EnumerateArray()) Shape(child, type.GetGenericArguments()[0], path + "[]", false);
            return;
        }
        Check(element.ValueKind == JsonValueKind.Object, $"Expected object at {path}.");
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            Check(properties.TryGetValue(property.Name, out var expected) && found.Add(property.Name), $"Unknown or duplicate field {path}.{property.Name}.");
            Shape(property.Value, expected!.PropertyType, path + "." + property.Name, expected.PropertyType == typeof(MapTerrainReference));
        }
        Check(found.Count == properties.Count, $"Missing field at {path}.");
    }
}
