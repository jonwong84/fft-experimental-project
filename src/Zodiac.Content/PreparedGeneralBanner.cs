using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Zodiac.Content;

public sealed record BannerRgb(byte R, byte G, byte B);
public sealed record GeneralBannerPiece(int SourceIndex, int Triplet, int X, int Y, int U, int V,
    int Width, int Height, string Material, ImmutableArray<BannerRgb> Colors);
public sealed record GeneralBannerSource(string Kind, string ReferenceRevision, string? BonusSha256, string? RequireSha256);
public sealed record LoadedGeneralBanner(PreparedGeneralBanner Definition, ImmutableArray<byte> Indices, ImmutableArray<byte> Palette);

/// <summary>Closed prepared condition-8/image-0 profile. Original formats remain offline.</summary>
public sealed record PreparedGeneralBanner
{
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
    public required ImmutableArray<GeneralBannerPiece> Pieces { get; init; }
    public required GeneralBannerSource Source { get; init; }
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, MaxDepth = 12,
    };

    public void Validate()
    {
        if (Format != "fft.general-banner" || SchemaVersion != 1 || IndexFormat != "index4-unpacked-u8" ||
            PaletteFormat != "rgb555-stp-le16" || Width != 256 || Height != 200 || Condition != 8 || Image != 0 || PaletteBank != 4)
            throw new InvalidDataException("Unsupported prepared general-banner format/profile.");
        var ids = new[] { Id, IndicesId, PaletteId };
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != 3)
            throw new InvalidDataException("Three distinct banner asset IDs are required.");
        foreach (string id in ids) _ = new AssetId(id);
        if (Pieces.IsDefault || Pieces.Length != 6) throw new InvalidDataException("Exactly six banner pieces are required.");
        for (int i = 0; i < 6; i++)
        {
            var p = Pieces[i];
            if (p is null || p.SourceIndex != i || p.Triplet != i / 3 || p.Width != 232 || p.Height != 31 ||
                p.U < 0 || p.U > 256 - p.Width || p.V < 0 || p.V > 200 - p.Height ||
                p.X < 0 || p.X > 256 - p.Width || p.Y < 0 || p.Y > 240 - p.Height ||
                p.Material != (i % 3 == 2 ? "add" : "subtract") || p.Colors.IsDefault || p.Colors.Length != 4 ||
                p.Colors.Any(c => c is null) || p.Colors[0] != p.Colors[1] || p.Colors[2] != p.Colors[3])
                throw new InvalidDataException("Unsupported banner piece identity, geometry, colors or material.");
        }
        static bool Hex(string? s, int count) => s is not null && s.Length == count && s.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if (Source is null || !Hex(Source.ReferenceRevision, 40) || Source.Kind is not ("authored-test" or "verified-retail") ||
            (Source.Kind == "authored-test" && (Source.BonusSha256 is not null || Source.RequireSha256 is not null)) ||
            (Source.Kind == "verified-retail" && (Source.ReferenceRevision != "11464943bbdbd2a0af022d90cb7f10455cf911df" ||
                Source.BonusSha256 != "c5d881b1fb62c04d1d7f0e5238faff4131676165c08e35b5cadb68b21c89165f" ||
                Source.RequireSha256 != "97112c50c0b037b2cb5c9d93ae9e2a7506b582c5edbd86a1debfb7ab8e777630")))
            throw new InvalidDataException("Explicit authored-test or verified-retail provenance is required.");
    }

    public byte[] ToUtf8()
    {
        Validate();
        return System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this, Options) + "\n");
    }

    public static PreparedGeneralBanner Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > 32768) throw new InvalidDataException("Banner metadata exceeds 32 KiB.");
        try
        {
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
            // Serializer rejects unknown/missing fields; also reject duplicate keys at every depth.
            static void Unique(JsonElement node)
            {
                if (node.ValueKind == JsonValueKind.Object)
                {
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var p in node.EnumerateObject())
                    {
                        if (!names.Add(p.Name)) throw new InvalidDataException("Duplicate banner field: " + p.Name);
                        Unique(p.Value);
                    }
                }
                else if (node.ValueKind == JsonValueKind.Array) foreach (var item in node.EnumerateArray()) Unique(item);
            }
            Unique(doc.RootElement);
            var result = JsonSerializer.Deserialize<PreparedGeneralBanner>(bytes.Span, Options) ?? throw new InvalidDataException("Empty banner metadata.");
            result.Validate();
            return result;
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        { throw new InvalidDataException("Invalid prepared banner: " + e.Message, e); }
    }

    public GeneralBannerDefinition ToLogicalDefinition(int holdFrames)
    {
        Validate();
        if (holdFrames is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(holdFrames));
        return new(new(Id), Condition, holdFrames);
    }

    public static LoadedGeneralBanner Load(LooseAssetStore store, AssetId id)
    {
        var definition = Parse(store.ReadAllBytes(id, 32768));
        if (definition.Id != id.Value) throw new InvalidDataException("Banner ID differs from manifest selection.");
        var indices = store.ReadAllBytes(new(definition.IndicesId), 51200);
        var palette = store.ReadAllBytes(new(definition.PaletteId), 32);
        if (indices.Length != 51200 || palette.Length != 32 || indices.Any(i => i > 15))
            throw new InvalidDataException("Invalid banner index/palette payload.");
        return new(definition, [.. indices], [.. palette]);
    }
}
