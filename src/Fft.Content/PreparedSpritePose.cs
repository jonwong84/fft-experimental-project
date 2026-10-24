using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Fft.Content.ContentPackages;
using Fft.Core;

namespace Fft.Content;

public sealed record SpritePosePart(int SourceIndex, int X, int Y, int U, int V, int Width, int Height,
    bool FlipHorizontal, bool FlipVertical);
public sealed record SpritePoseSource(string Edition, string ReferenceRevision, string SprSha256, string ShpSha256, string SeqSha256);
public sealed record LoadedSpritePose(PreparedSpritePose Definition, ImmutableArray<byte> Rgba);

/// <summary>Version 1 prepared static pose: 256x256 straight RGBA8 atlas, neutral opaque primary-sheet pieces.
/// No original-format parser, animation player or simulation dependency beyond stable asset IDs.</summary>
public sealed class PreparedSpritePose
{
    public const string Format = "fft.sprite-pose";
    public const string PixelFormat = "rgba8-straight-5to8-replication";
    public AssetId Id { get; }
    public AssetId TextureId { get; }
    public int SequenceId { get; }
    public int FrameId { get; }
    public int Wait { get; }
    public int PaletteIndex { get; }
    public ImmutableArray<ushort> RawPalette { get; }
    public ImmutableArray<SpritePosePart> PartsBackToFront { get; }
    public SpritePoseSource Source { get; }

    public PreparedSpritePose(AssetId id, AssetId textureId, int sequenceId, int frameId, int wait, int paletteIndex,
        IEnumerable<ushort> rawPalette, IEnumerable<SpritePosePart> partsBackToFront, SpritePoseSource source)
    {
        ArgumentNullException.ThrowIfNull(rawPalette);
        ArgumentNullException.ThrowIfNull(partsBackToFront);
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(id.Value) || string.IsNullOrEmpty(textureId.Value) || id == textureId)
            throw new ArgumentException("Distinct explicit pose and texture IDs are required.");
        if (sequenceId is < 0 or > 255 || frameId is < 0 or >= 208 || wait is < 0 or > 255 || paletteIndex is < 0 or > 5)
            throw new ArgumentException("Prepared pose selection is outside the bounded profile.");
        var palette = rawPalette.ToImmutableArray();
        if (palette.Length != 16 || palette[0] != 0) throw new ArgumentException("Pose requires 16 raw colors with transparent color zero.");
        var parts = partsBackToFront.Take(9).ToImmutableArray();
        if (parts.Length is < 1 or > 8) throw new ArgumentException("Pose requires 1..8 pieces.");
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p is null || p.SourceIndex != parts.Length - 1 - i || p.X is < -128 or > 127 || p.Y is < -128 or > 127 ||
                (p.Width, p.Height) is not ((8, 8) or (16, 16) or (24, 16) or (24, 24)) ||
                p.U < 0 || p.V < 0 || p.U > 256 - p.Width || p.V > 256 - p.Height || p.U % 8 != 0 || p.V % 8 != 0)
                throw new ArgumentException("Invalid source rectangle, signed offset or reverse piece order.");
        }
        if (source.Edition != "SCUS-94221" || !Hex(source.ReferenceRevision, 40) || !Hex(source.SprSha256, 64) ||
            !Hex(source.ShpSha256, 64) || !Hex(source.SeqSha256, 64))
            throw new ArgumentException("Pose requires edition, pinned reference and source SHA-256 identities.");
        (Id, TextureId, SequenceId, FrameId, Wait, PaletteIndex, RawPalette, PartsBackToFront, Source) =
            (id, textureId, sequenceId, frameId, wait, paletteIndex, palette, parts, source);
    }

    public byte[] ToUtf8() => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        format = Format, schemaVersion = 1, id = Id.Value, textureId = TextureId.Value,
        pixelFormat = PixelFormat, width = 256, height = 256, sequenceId = SequenceId,
        frameId = FrameId, wait = Wait, paletteIndex = PaletteIndex, rawPalette = RawPalette,
        partsBackToFront = PartsBackToFront, source = Source,
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) + "\n");

    public static PreparedSpritePose Parse(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length > 32768) throw new InvalidDataException("Prepared pose metadata exceeds 32 KiB.");
        try
        {
            using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            var v = new PackageJsonValidation(null);
            v.Fields(root, null, "$", "format", "schemaVersion", "id", "textureId", "pixelFormat", "width", "height",
                "sequenceId", "frameId", "wait", "paletteIndex", "rawPalette", "partsBackToFront", "source");
            string Text(JsonElement n, string key) => v.Text(n.GetProperty(key), null, key);
            int Number(JsonElement n, string key, int min, int max) => v.Integer(n.GetProperty(key), null, key, min, max);
            if (Text(root, "format") != Format || Number(root, "schemaVersion", 1, 1) != 1 || Text(root, "pixelFormat") != PixelFormat ||
                Number(root, "width", 256, 256) != 256 || Number(root, "height", 256, 256) != 256)
                throw new InvalidDataException("Unsupported prepared pose format or profile.");
            var palette = v.Array(root.GetProperty("rawPalette"), null, "rawPalette", 16)
                .Select(n => (ushort)v.Integer(n, null, "rawPalette", 0, 65535)).ToArray();
            var parts = new List<SpritePosePart>();
            foreach (var p in v.Array(root.GetProperty("partsBackToFront"), null, "partsBackToFront", 8))
            {
                v.Fields(p, null, "part", "sourceIndex", "x", "y", "u", "v", "width", "height", "flipHorizontal", "flipVertical");
                bool Flag(string key) => p.GetProperty(key).ValueKind switch
                {
                    JsonValueKind.True => true, JsonValueKind.False => false,
                    _ => throw new InvalidDataException($"Expected boolean {key}."),
                };
                parts.Add(new(Number(p, "sourceIndex", 0, 7), Number(p, "x", -128, 127), Number(p, "y", -128, 127),
                    Number(p, "u", 0, 255), Number(p, "v", 0, 255), Number(p, "width", 1, 256), Number(p, "height", 1, 256),
                    Flag("flipHorizontal"), Flag("flipVertical")));
            }
            var source = root.GetProperty("source");
            v.Fields(source, null, "source", "edition", "referenceRevision", "sprSha256", "shpSha256", "seqSha256");
            return new(new(Text(root, "id")), new(Text(root, "textureId")), Number(root, "sequenceId", 0, 255),
                Number(root, "frameId", 0, 207), Number(root, "wait", 0, 255), Number(root, "paletteIndex", 0, 5), palette, parts,
                new(Text(source, "edition"), Text(source, "referenceRevision"), Text(source, "sprSha256"), Text(source, "shpSha256"), Text(source, "seqSha256")));
        }
        catch (Exception e) when (e is ArgumentException or ContentPackageValidationException or JsonException)
        {
            throw new InvalidDataException("Invalid prepared sprite pose metadata: " + e.Message, e);
        }
    }

    public static LoadedSpritePose Load(LooseAssetStore store, AssetId id)
    {
        ArgumentNullException.ThrowIfNull(store);
        var pose = Parse(store.ReadAllBytes(id));
        if (pose.Id != id) throw new InvalidDataException("Prepared pose ID does not match its manifest selection.");
        byte[] rgba = store.ReadAllBytes(pose.TextureId);
        if (rgba.Length != 256 * 256 * 4) throw new InvalidDataException("Prepared pose atlas must be 256x256 RGBA8.");
        for (int i = 3; i < rgba.Length; i += 4)
            if (rgba[i] is not (0 or 255)) throw new InvalidDataException("The selected opaque profile permits only alpha 0/255.");
        return new(pose, [.. rgba]);
    }

    private static bool Hex(string? value, int length) => value is not null && value.Length == length &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
