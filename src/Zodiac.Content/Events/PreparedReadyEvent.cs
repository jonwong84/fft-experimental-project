using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Zodiac.Content.Events;

/// <summary>Separate authored BATTLE, non-tutorial, speed-one condition-0 profile.</summary>
public sealed class PreparedReadyEvent
{
    public const string Format = "fft.prepared-ready-event";
    public const string Profile = "sample:event-profile/non-tutorial-speed1-preloaded-ready-v1";
    public AssetId Id { get; }
    public AssetId ArtworkId { get; }
    public AssetId OwnerId { get; }
    public AssetId DefinitionId => new(Id.Value + "/ready");
    public ScenarioEventSource Source { get; }
    public ImmutableArray<PreparedOrderedBannerInstruction> Instructions { get; }
    private readonly PreparedScenarioEvent body;
    private readonly PreparedOrderedBannerInstruction display;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        MaxDepth = 12,
    };
    public PreparedReadyEvent(AssetId id, AssetId artworkId, AssetId ownerId, ScenarioEventSource source,
        IEnumerable<PreparedOrderedBannerInstruction> instructions, ScenarioEventScene scene = ScenarioEventScene.Battle,
        bool tutorial = false, int speed = 1)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(instructions);
        if (id.Value is not { Length: > 0 and <= 250 } || artworkId.Value is not { Length: > 0 and <= 256 } ||
            ownerId.Value is not { Length: > 0 and <= 256 } || id == artworkId || scene != ScenarioEventScene.Battle || tutorial || speed != 1 ||
            source.ReferenceRevision != PreparedReadyArtwork.ReferenceRevision)
            throw new InvalidDataException("Ready requires explicit IDs, pinned source, BATTLE, non-tutorial speed one.");
        Instructions = instructions.Take(8189).ToImmutableArray();
        if (Instructions.Any(i => i is null || !Enum.IsDefined(i.Operation))) throw new InvalidDataException("Unknown Ready instruction.");
        var displays = Instructions.Where(i => i.Operation == OrderedScenarioOperation.DisplayConditions).ToArray();
        if (displays.Length != 1 || displays[0].Operand1 != 0 || displays[0].Operand2 is not (>= 0 and <= 255) || displays[0].TargetOffset is not null)
            throw new InvalidDataException("Exactly one condition-0 display with unsigned-byte hold is required, including unreachable records.");
        display = displays[0];
        // Reuse byte-span/CFG/operand validation, not a legacy display execution path.
        body = new(id, scene, source, Instructions.Select(PreparedOrderedBannerEvent.ForValidation), PreparedScenarioEvent.TaskBarrierProfile);
        (Id, ArtworkId, OwnerId, Source) = (id, artworkId, ownerId, source);
    }
    public ReadySceneProgram ToExecutionDefinition() => new(Id, body.ToSceneExecutionDefinition().Instructions.Select(i =>
        i.Offset == display.Offset ? new ReadySceneInstruction(i.Offset, new(new(DefinitionId, 0, display.Operand2!.Value), ArtworkId, OwnerId)) : new ReadySceneInstruction(i)));
    public byte[] ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(new EventDto(Format, 1, Profile, PreparedScenarioEvent.Dialect,
        Id.Value, ArtworkId.Value, OwnerId.Value, "Battle", false, 1, Source,
        Instructions.Select(i => new InstructionDto(i.Offset, i.Operation.ToString(), i.Operand1, i.Operand2, i.TargetOffset)).ToArray()), Options);
    public static PreparedReadyEvent Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > PreparedScenarioEvent.MaxJsonBytes) throw new InvalidDataException("Ready event exceeds 1 MiB.");
        try
        {
            var d = JsonSerializer.Deserialize<EventDto>(bytes.Span, Options) ?? throw new InvalidDataException("Ready event required.");
            if (d.Format != Format || d.SchemaVersion != 1 || d.Profile != Profile || d.Dialect != PreparedScenarioEvent.Dialect ||
                d.Scene != "Battle" || d.Tutorial || d.Speed != 1 || d.Instructions.Length > 8188)
                throw new InvalidDataException("Unsupported Ready event format/profile/domain.");
            return new(new(d.Id), new(d.ArtworkId), new(d.OwnerId), d.Source, d.Instructions.Select(i =>
            {
                if (i is null || !Enum.GetNames<OrderedScenarioOperation>().Contains(i.Operation)) throw new InvalidDataException("Unknown Ready operation.");
                return new PreparedOrderedBannerInstruction(i.Offset, Enum.Parse<OrderedScenarioOperation>(i.Operation), i.Operand1, i.Operand2, i.TargetOffset);
            }));
        }
        catch (Exception e) when (e is JsonException or ArgumentException or OverflowException)
        { throw new InvalidDataException("Invalid prepared Ready event: " + e.Message, e); }
    }
    public static PreparedReadyEvent Load(LooseAssetStore store, AssetId id)
    {
        var result = Parse(store.ReadAllBytes(id, PreparedScenarioEvent.MaxJsonBytes));
        if (result.Id != id) throw new InvalidDataException("Ready event ID differs from manifest selection.");
        return result;
    }
    private sealed record EventDto(string Format, int SchemaVersion, string Profile, string Dialect, string Id, string ArtworkId,
        string OwnerId, string Scene, bool Tutorial, int Speed, ScenarioEventSource Source, InstructionDto[] Instructions);
    private sealed record InstructionDto(int Offset, string Operation, int? Operand1, int? Operand2, int? TargetOffset);
}
