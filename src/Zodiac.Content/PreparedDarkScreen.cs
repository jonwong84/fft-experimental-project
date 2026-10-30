using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Zodiac.Content;

public sealed record DarkScreenRotation(int Angle, int AX, int AY, int BX, int BY);
public sealed record DarkScreenSource(string Kind, string ReferenceRevision, string? ExecutableSha256, string? SqrtSha256, string? SinCosSha256);

/// <summary>Separate authored preview over prepared original math. Never an imported original event.</summary>
public sealed record PreparedDarkScreen
{
    public const string Profile = "authored-dark-screen-preview-v1";
    public const string ReferenceRevision = "11464943bbdbd2a0af022d90cb7f10455cf911df";
    public const string ExecutableHash = "287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562";
    public const string SqrtHash = "45ca1b7b619b33961c03c82243b8baf59fc50c49b4d7eefc94b76edbd417a5c8";
    public const string SinCosHash = "b106234165685d79392c926192d1b2f9e30125307abde6040747e401a35d0e87";
    public required string Format { get; init; }
    public required int SchemaVersion { get; init; }
    public required string ExecutionProfile { get; init; }
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required int HoldFrames { get; init; }
    public required DarkScreenParameters Parameters { get; init; }
    public required ImmutableArray<DarkScreenTileDefinition> Tiles { get; init; }
    public required ImmutableArray<DarkScreenRotation> Rotations { get; init; }
    public required DarkScreenSource Source { get; init; }
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true, AllowDuplicateProperties = false, MaxDepth = 12,
    };
    public void Validate()
    {
        if (Format != "fft.dark-screen" || SchemaVersion != 1 || ExecutionProfile != Profile ||
            string.IsNullOrWhiteSpace(Id) || Id.Length > 256 || string.IsNullOrWhiteSpace(OwnerId) || OwnerId.Length > 256 ||
            HoldFrames is < 0 or > 255 || Parameters is null || Tiles.IsDefault || Rotations.IsDefault || Rotations.Length != 9)
            throw new InvalidDataException("Unsupported dark-screen metadata/profile.");
        try { _ = new AssetId(OwnerId); _ = new DarkScreenDefinition(new(Id), Parameters, Tiles); }
        catch (ArgumentException e) { throw new InvalidDataException("Invalid logical dark-screen definition.", e); }
        for (int i = 0; i < 9; i++)
        {
            var r = Rotations[i];
            if (r is null || r.Angle != i * 64 || new[] { r.AX, r.AY, r.BX, r.BY }.Any(c => c is < -4096 or > 4096) ||
                (long)r.AX * r.BY - (long)r.AY * r.BX <= 0)
                throw new InvalidDataException("Expected ordered bounded nondegenerate Q12 rotation axes.");
        }
        if (Rotations[8] != new DarkScreenRotation(512, 0, 4096, -4096, 0))
            throw new InvalidDataException("Full-size diamond axes differ.");
        if (Source is null || Source.ReferenceRevision != ReferenceRevision || Source.Kind is not ("verified-retail" or "authored-test") ||
            (Source.Kind == "authored-test" && (Source.ExecutableSha256 is not null || Source.SqrtSha256 is not null || Source.SinCosSha256 is not null)) ||
            (Source.Kind == "verified-retail" && (Source.ExecutableSha256 != ExecutableHash || Source.SqrtSha256 != SqrtHash || Source.SinCosSha256 != SinCosHash)))
            throw new InvalidDataException("Expected pinned authored-test or verified-retail math provenance.");
    }
    public DarkScreenProgram ToExecutionDefinition()
    {
        Validate();
        var definition = new DarkScreenDefinition(new(Id), Parameters, Tiles);
        return new(new("sample:event/dark-screen-preview"),
            [new(4, new StartDarkScreen(definition, new(OwnerId))), new(new SceneEventInstruction(7, SceneTaskClass.DarkScreenTransition)),
             new(new SceneEventInstruction(10, HoldFrames)), new(13), new(new SceneEventInstruction(16, SceneTaskClass.DarkScreenTransition)),
             new(new SceneEventInstruction(new ConditionalEventInstruction(19, ConditionalEventOperation.Exit)))]);
    }
    public byte[] ToUtf8() { Validate(); return JsonSerializer.SerializeToUtf8Bytes(this, Options); }
    public static PreparedDarkScreen Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > 65536) throw new InvalidDataException("Dark-screen metadata exceeds 64 KiB.");
        try
        {
            var definition = JsonSerializer.Deserialize<PreparedDarkScreen>(bytes.Span, Options) ?? throw new InvalidDataException("Missing definition.");
            using var document = JsonDocument.Parse(bytes);
            RequireFields(document.RootElement.GetProperty("parameters"), ["reserved", "mode", "divisor", "angleStep", "radiusStep", "image"]);
            foreach (var tile in document.RootElement.GetProperty("tiles").EnumerateArray())
                RequireFields(tile, ["x", "y", "initialDelay"]);
            definition.Validate(); return definition;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or OverflowException)
        { throw new InvalidDataException("Invalid dark-screen metadata.", e); }
    }
    private static void RequireFields(JsonElement element, string[] fields)
    {
        if (!element.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(fields.Order()))
            throw new InvalidDataException("All selected data fields are required explicitly.");
    }
    public static PreparedDarkScreen Load(LooseAssetStore store, AssetId id)
    {
        var definition = Parse(store.ReadAllBytes(id, 65536));
        if (definition.Id != id.Value) throw new InvalidDataException("Selected ID differs from definition.");
        return definition;
    }
}
