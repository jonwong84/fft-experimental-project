using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Framework.Campaign;

namespace Zodiac.Content.Saves;

public sealed class WorldCheckpointSaveProfile
{
    public string Id { get; }
    public int Version { get; }
    public int ContentVersion { get; }
    public WorldCheckpointDefinition Definition { get; }
    public WorldCheckpointSaveProfile(string id, int version, int contentVersion, WorldCheckpointDefinition definition)
    {
        WorldCheckpointDefinition.CheckId(new(id)); ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(contentVersion, 1); ArgumentNullException.ThrowIfNull(definition);
        (Id, Version, ContentVersion, Definition) = (id, version, contentVersion, definition);
    }
}
public sealed class WorldSaveValidationException(string message, Exception? inner = null) : Exception(message, inner);
public sealed record WorldSaveSummary(string LocationId, int PartyCount, int Funds, int Month, int Day, long ElapsedSeconds, bool Progress);
public sealed record WorldCheckpointLoad(WorldCheckpointState State, WorldSaveSummary Summary, int SourceSchemaVersion)
{
    public bool WasMigrated => SourceSchemaVersion != WorldCheckpointSave.SchemaVersion;
}

/// <summary>Scalar/ID DTOs only. The selected profile owns definitions; input cannot select content.</summary>
public static class WorldCheckpointSave
{
    public const string Format = "fft.synthetic-world-save", Checkpoint = "world-idle", RngPolicy = "unsupported";
    public const int SchemaVersion = 2, MaximumBytes = 65536;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true, AllowDuplicateProperties = false, MaxDepth = 12,
    };
    public static WorldSaveSummary Summarize(WorldCheckpointState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new(state.LocationId.Value, state.Party.Length, state.Funds, state.Date.Month, state.Date.Day, state.ElapsedSeconds, state.Progress);
    }

    public static byte[] ToUtf8(WorldCheckpointSaveProfile profile, WorldCheckpointState state)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(state);
        if (!ReferenceEquals(profile.Definition, state.Definition)) throw new WorldSaveValidationException("State must use the selected profile definition.");
        var snapshot = new Snapshot(profile.Id, profile.Version, profile.ContentVersion, Checkpoint, RngPolicy,
            state.Definition.RouteId.Value, state.LocationId.Value, state.Definition.ProgressFlagId.Value,
            state.Progress, state.Funds, state.Date.Month, state.Date.Day, state.ElapsedSeconds,
            [.. state.Party.Select(p => new Member(p.Id.Value, p.JobId.Value, p.EquippedItemId?.Value))],
            [.. state.Inventory.Select(i => new Stack(i.ItemId.Value, i.Quantity))], Summarize(state));
        _ = Restore(profile, snapshot);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new Envelope<Snapshot>(Format, SchemaVersion, Hash(snapshot), snapshot), Options);
        if (bytes.Length > MaximumBytes) throw new WorldSaveValidationException("World save exceeds 64 KiB.");
        return bytes;
    }
    public static WorldCheckpointLoad Read(WorldCheckpointSaveProfile profile, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (bytes.Length > MaximumBytes) throw new WorldSaveValidationException("World save exceeds 64 KiB.");
        try
        {
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 12, AllowDuplicateProperties = false });
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out int schema))
                throw new WorldSaveValidationException("An integer schemaVersion is required.");
            Snapshot snapshot;
            if (schema == SchemaVersion)
            {
                var envelope = JsonSerializer.Deserialize<Envelope<Snapshot>>(bytes.Span, Options)!;
                ValidateEnvelope(envelope); snapshot = envelope.Snapshot;
            }
            else if (schema == 1)
            {
                var envelope = JsonSerializer.Deserialize<Envelope<LegacySnapshot>>(bytes.Span, Options)!;
                ValidateEnvelope(envelope); var old = envelope.Snapshot;
                snapshot = new(old.ProfileId, old.ProfileVersion, old.ContentVersion, Checkpoint, RngPolicy, old.RouteId,
                    old.LocationId, old.ProgressFlagId, old.Progress, old.Funds, old.Month, old.Day, old.ElapsedSeconds, old.Party, old.Inventory, old.Summary);
            }
            else throw new WorldSaveValidationException("Unsupported world save schema version.");
            var state = Restore(profile, snapshot);
            return new(state, Summarize(state), schema);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        { throw new WorldSaveValidationException("Invalid world save: " + e.Message, e); }
    }
    private static void ValidateEnvelope<T>(Envelope<T> envelope)
    {
        if (envelope.Format != Format || envelope.Snapshot is null || envelope.Sha256 != Hash(envelope.Snapshot))
            throw new WorldSaveValidationException("Wrong format, missing snapshot or checksum mismatch.");
    }
    private static WorldCheckpointState Restore(WorldCheckpointSaveProfile profile, Snapshot saved)
    {
        if (saved.ProfileId != profile.Id || saved.ProfileVersion != profile.Version || saved.ContentVersion != profile.ContentVersion ||
            saved.Checkpoint != Checkpoint || saved.RngPolicy != RngPolicy)
            throw new WorldSaveValidationException("Unsupported profile/version, checkpoint or RNG capability.");
        if (saved.Party.Length > WorldCheckpointDefinition.MaximumParty || saved.Inventory.Length > WorldCheckpointDefinition.MaximumStacks || saved.Party.Any(p => p is null))
            throw new WorldSaveValidationException("World collections exceed the proof budget or contain null members.");
        var state = profile.Definition.Restore(new(saved.RouteId), new(saved.LocationId),
            saved.Party.Select(p => new WorldPartyMember(new(p.Id), new(p.JobId), p.EquippedItemId is null ? null : new AssetId(p.EquippedItemId))),
            saved.Inventory.Select(i => new WorldItemStack(new(i.ItemId), i.Quantity)), saved.Funds, new(saved.Month, saved.Day),
            new(saved.ProgressFlagId), saved.Progress, saved.ElapsedSeconds);
        if (saved.Summary != Summarize(state)) throw new WorldSaveValidationException("Saved slot summary disagrees with validated world state.");
        return state;
    }
    private static string Hash<T>(T snapshot) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshot, Options)));
    private sealed record Envelope<T>(string Format, int SchemaVersion, string Sha256, T Snapshot);
    private sealed record Member(string Id, string JobId, string? EquippedItemId);
    private readonly record struct Stack(string ItemId, int Quantity);
    private sealed record Snapshot(string ProfileId, int ProfileVersion, int ContentVersion, string Checkpoint, string RngPolicy,
        string RouteId, string LocationId, string ProgressFlagId, bool Progress, int Funds, int Month, int Day, long ElapsedSeconds,
        Member[] Party, Stack[] Inventory, WorldSaveSummary Summary);
    private sealed record LegacySnapshot(string ProfileId, int ProfileVersion, int ContentVersion,
        string RouteId, string LocationId, string ProgressFlagId, bool Progress, int Funds, int Month, int Day, long ElapsedSeconds,
        Member[] Party, Stack[] Inventory, WorldSaveSummary Summary);
}
