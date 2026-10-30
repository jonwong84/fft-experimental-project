using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Zodiac.Content.Events;

/// <summary>Closed authored composition; never an imported original event or standalone dark timer.</summary>
public sealed record PreparedDarkScreenIntroduction
{
    public const string FormatName = "fft.prepared-dark-screen-introduction";
    public const string ProfileName = "sample:scene-profile/preloaded-dark-screen-introduction-v1";
    public required string Format { get; init; }
    public required int SchemaVersion { get; init; }
    public required string Profile { get; init; }
    public required string Id { get; init; }
    public required string DarkScreenId { get; init; }
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
        string[] ids = [Id, DarkScreenId, GeneralArtworkId, ReadyArtworkId, OwnerId];
        if (Format != FormatName || SchemaVersion != 1 || Profile != ProfileName || Provenance != "authored" ||
            ReferenceRevision != PreparedDarkScreen.ReferenceRevision || GeneralHold is < 0 or > 255 || ReadyHold is < 0 or > 255 ||
            Id is not { Length: > 0 and <= 248 } || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 256))
            throw new InvalidDataException("Expected the authored pinned dark-screen introduction and bounded IDs/holds.");
        string[] allIds = [.. ids, Id + "/general", Id + "/ready"];
        if (allIds.Distinct(StringComparer.Ordinal).Count() != allIds.Length)
            throw new InvalidDataException("Descriptor, resources, derived definitions and owner must have distinct identities.");
        foreach (string id in allIds) _ = new AssetId(id);
    }

    public DarkScreenIntroductionProgram ToProgram(PreparedDarkScreen dark)
    {
        Validate(); ArgumentNullException.ThrowIfNull(dark); dark.Validate();
        if (dark.Id != DarkScreenId || dark.OwnerId != OwnerId || dark.Source.Kind != "verified-retail" ||
            dark.Source.ReferenceRevision != ReferenceRevision)
            throw new InvalidDataException("Dark math must have the selected identity, owner and verified-retail provenance.");
        // HoldFrames is intentionally unused: the combined event releases after Ready's barrier.
        return new(new(Id), new(new(new(dark.Id), dark.Parameters, dark.Tiles), new(OwnerId)),
            new(new(new(Id + "/general"), 8, GeneralHold), new(GeneralArtworkId), new(OwnerId)),
            new(new(new(Id + "/ready"), 0, ReadyHold), new(ReadyArtworkId), new(OwnerId)));
    }

    public byte[] ToUtf8() { Validate(); return JsonSerializer.SerializeToUtf8Bytes(this, Options); }
    public static PreparedDarkScreenIntroduction Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > 16384) throw new InvalidDataException("Introduction descriptor exceeds 16 KiB.");
        try
        {
            var result = JsonSerializer.Deserialize<PreparedDarkScreenIntroduction>(bytes.Span, Options)
                ?? throw new InvalidDataException("Introduction descriptor required.");
            result.Validate(); return result;
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        { throw new InvalidDataException("Invalid introduction JSON.", e); }
    }
    public static PreparedDarkScreenIntroduction Load(LooseAssetStore store, AssetId id)
    {
        ArgumentNullException.ThrowIfNull(store);
        var result = Parse(store.ReadAllBytes(id, 16384));
        if (result.Id != id.Value) throw new InvalidDataException("Introduction identity mismatch.");
        return result;
    }
}
