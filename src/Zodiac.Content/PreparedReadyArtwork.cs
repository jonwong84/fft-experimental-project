using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;

namespace Zodiac.Content;

public sealed record ReadyGlyph(int SourceIndex, int X, int Y, int U, int V, int Width, int Height,
    string Material, ImmutableArray<BannerRgb> Colors);
public sealed record LoadedReadyArtwork(PreparedReadyArtwork Definition, ImmutableArray<byte> Indices, ImmutableArray<byte> Palette)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Definition);
        Definition.Validate();
        if (Indices.IsDefault || Indices.Length != 51200 || Indices.Any(i => i > 15) || Palette.IsDefault || Palette.Length != 32)
            throw new InvalidDataException("Ready artwork requires immutable 256x200 indices and a sixteen-word palette.");
    }
}

/// <summary>Closed Ready artwork profile; hold/ticks and graphics resources do not belong to this content.</summary>
public sealed record PreparedReadyArtwork
{
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    public required string Format { get; init; }
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string IndicesId { get; init; }
    public required string PaletteId { get; init; }
    public required string IndexFormat { get; init; }
    public required string PaletteFormat { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Condition { get; init; }
    public required int Image { get; init; }
    public required int PaletteBank { get; init; }
    public required ImmutableArray<ReadyGlyph> Glyphs { get; init; }
    public required GeneralBannerSource Source { get; init; }
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        MaxDepth = 12,
    };
    public void Validate()
    {
        if (Format != "fft.ready-artwork" || SchemaVersion != 1 || IndexFormat != "index4-unpacked-u8" ||
            PaletteFormat != "rgb555-stp-le16" || Width != 256 || Height != 200 || Condition != 0 || Image != 0 || PaletteBank != 2)
            throw new InvalidDataException("Unsupported Ready artwork format/profile.");
        string[] ids = [Id, IndicesId, PaletteId];
        if (ids.Any(i => string.IsNullOrWhiteSpace(i) || i.Length > 256) || ids.Distinct(StringComparer.Ordinal).Count() != 3)
            throw new InvalidDataException("Ready artwork requires three distinct stable IDs.");
        foreach (string id in ids) _ = new AssetId(id);
        if (Glyphs.IsDefault || Glyphs.Length != 6) throw new InvalidDataException("Exactly six Ready glyphs required.");
        for (int i = 0; i < 6; i++)
        {
            var g = Glyphs[i];
            if (g is null || g.SourceIndex != i || g.Width is not (16 or 17) || g.Height != 24 ||
                g.X < 0 || g.X > 256 - g.Width || g.Y < 0 || g.Y > 240 - g.Height ||
                g.U < 0 || g.U > 255 - g.Width || g.V < 0 || g.V > 200 - g.Height || g.Material != "average" ||
                g.Colors.IsDefault || g.Colors.Length != 4 || g.Colors.Any(c => c is null))
                throw new InvalidDataException("Invalid Ready glyph identity, geometry, material or corners.");
        }
        if (Source is null || Source.ReferenceRevision != ReferenceRevision || Source.Kind is not ("authored-test" or "verified-retail") ||
            (Source.Kind == "authored-test" && (Source.BonusSha256 is not null || Source.RequireSha256 is not null)) ||
            (Source.Kind == "verified-retail" && (Source.BonusSha256 != "c5d881b1fb62c04d1d7f0e5238faff4131676165c08e35b5cadb68b21c89165f" ||
                Source.RequireSha256 != "97112c50c0b037b2cb5c9d93ae9e2a7506b582c5edbd86a1debfb7ab8e777630")))
            throw new InvalidDataException("Ready artwork requires pinned authored-test or verified-retail provenance.");
    }
    public byte[] ToUtf8()
    {
        Validate();
        return JsonSerializer.SerializeToUtf8Bytes(this, Options);
    }
    public static PreparedReadyArtwork Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > 32768) throw new InvalidDataException("Ready metadata exceeds 32 KiB.");
        try
        {
            var result = JsonSerializer.Deserialize<PreparedReadyArtwork>(bytes.Span, Options) ?? throw new InvalidDataException("Ready artwork required.");
            result.Validate();
            return result;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or OverflowException)
        { throw new InvalidDataException("Invalid Ready artwork: " + e.Message, e); }
    }
    public static LoadedReadyArtwork Load(LooseAssetStore store, AssetId id)
    {
        var definition = Parse(store.ReadAllBytes(id, 32768));
        if (definition.Id != id.Value) throw new InvalidDataException("Ready artwork ID differs from manifest selection.");
        var loaded = new LoadedReadyArtwork(definition, [.. store.ReadAllBytes(new(definition.IndicesId), 51200)],
            [.. store.ReadAllBytes(new(definition.PaletteId), 32)]);
        loaded.Validate();
        return loaded;
    }
}
