using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Zodiac.Content.Events;

/// <summary>A closed authored descriptor, not an original bytecode or general event interpreter format.</summary>
public sealed record PreparedBannerReadySequence
{
    public const string FormatName = "fft.prepared-banner-ready-sequence";
    public const string ProfileName = "sample:scene-profile/preloaded-banner-ready-v1";
    public required string Format { get; init; }
    public required int SchemaVersion { get; init; }
    public required string Profile { get; init; }
    public required string Id { get; init; }
    public required string GeneralArtworkId { get; init; }
    public required string ReadyArtworkId { get; init; }
    public required string OwnerId { get; init; }
    public required int GeneralHold { get; init; }
    public required int ReadyHold { get; init; }
    public required string Provenance { get; init; }
    public required string ReferenceRevision { get; init; }
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 4,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
    };
    public void Validate()
    {
        if (Format != FormatName || SchemaVersion != 1 || Profile != ProfileName || Provenance != "authored" ||
            ReferenceRevision != PreparedReadyArtwork.ReferenceRevision || GeneralHold is < 0 or > 255 || ReadyHold is < 0 or > 255 ||
            Id is not { Length: > 0 and <= 248 } || new[] { Id, GeneralArtworkId, ReadyArtworkId, OwnerId }.Any(string.IsNullOrWhiteSpace) ||
            new[] { Id, GeneralArtworkId, ReadyArtworkId, OwnerId }.Distinct(StringComparer.Ordinal).Count() != 4)
            throw new InvalidDataException("Expected an authored, pinned, preloaded banner/Ready descriptor with distinct IDs and holds 0..255.");
        foreach (string id in new[] { Id, GeneralArtworkId, ReadyArtworkId, OwnerId }) _ = new AssetId(id);
        _ = ToProgramUnchecked();
    }
    private BannerReadyProgram ToProgramUnchecked() => new(new(Id), new(new(new(Id + "/general"), 8, GeneralHold), new(GeneralArtworkId), new(OwnerId)),
        new(new(new(Id + "/ready"), 0, ReadyHold), new(ReadyArtworkId), new(OwnerId)));
    public BannerReadyProgram ToProgram() { Validate(); return ToProgramUnchecked(); }
    public byte[] ToUtf8() { Validate(); return JsonSerializer.SerializeToUtf8Bytes(this, Options); }
    public static PreparedBannerReadySequence Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > 16384) throw new InvalidDataException("Sequence descriptor exceeds 16 KiB.");
        try
        {
            var result = JsonSerializer.Deserialize<PreparedBannerReadySequence>(bytes.Span, Options) ?? throw new InvalidDataException("Descriptor required.");
            result.Validate(); return result;
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { throw new InvalidDataException("Invalid sequence JSON.", e); }
    }
    public static PreparedBannerReadySequence Load(LooseAssetStore store, AssetId id)
    {
        ArgumentNullException.ThrowIfNull(store);
        var result = Parse(store.ReadAllBytes(id, 16384));
        if (result.Id != id.Value) throw new InvalidDataException("Sequence identity mismatch.");
        return result;
    }
}
