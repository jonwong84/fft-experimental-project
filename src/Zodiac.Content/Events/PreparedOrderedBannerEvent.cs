using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zodiac.Framework;
using Zodiac.Framework.Events;

namespace Zodiac.Content.Events;

// Separate vocabulary/schema: the legacy prepared event parser and profiles remain closed.
public enum OrderedScenarioOperation
{
    Nop, ZeroVariable, AddImmediate, AddVariable, CompareEqual, JumpForwardIfZero, JumpForward,
    ForwardTarget, ForwardTargetExit, SearchEnd, WaitFrames, WaitForTaskClass, DisplayConditions,
}
public sealed record PreparedOrderedBannerInstruction(int Offset, OrderedScenarioOperation Operation,
    int? Operand1 = null, int? Operand2 = null, int? TargetOffset = null);

/// <summary>Authored BATTLE/non-tutorial/speed-one, with exactly one preloaded condition-8 record.</summary>
public sealed class PreparedOrderedBannerEvent
{
    public const string Format = "fft.prepared-ordered-banner-event";
    public const string Profile = "sample:event-profile/non-tutorial-speed1-preloaded-banner-ordered-v1";
    public AssetId Id { get; }
    public AssetId ArtworkId { get; }
    public AssetId OwnerId { get; }
    public AssetId DefinitionId => new(Id.Value + "/banner");
    public ScenarioEventSource Source { get; }
    public ImmutableArray<PreparedOrderedBannerInstruction> Instructions { get; }
    private readonly PreparedScenarioEvent validatedBody;
    private readonly PreparedOrderedBannerInstruction display;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, AllowDuplicateProperties = false, MaxDepth = 12,
    };

    public PreparedOrderedBannerEvent(AssetId id, AssetId artworkId, AssetId ownerId, ScenarioEventSource source,
        IEnumerable<PreparedOrderedBannerInstruction> instructions, ScenarioEventScene scene = ScenarioEventScene.Battle,
        bool tutorial = false, int speed = 1)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(instructions);
        if (id.Value is not { Length: > 0 and <= 249 } || artworkId.Value is not { Length: > 0 and <= 256 } ||
            ownerId.Value is not { Length: > 0 and <= 256 } || id == artworkId || scene != ScenarioEventScene.Battle || tutorial || speed != 1)
            throw new InvalidDataException("Ordered banner requires distinct event/artwork IDs, an owner, BATTLE, non-tutorial and speed one.");
        if (source.ReferenceRevision != "11464943bbdbd2a0af022d90cb7f10455cf911df")
            throw new InvalidDataException("Ordered banner requires its pinned source revision.");
        Instructions = instructions.Take(8189).ToImmutableArray();
        if (Instructions.Any(i => i is null || !Enum.IsDefined(i.Operation))) throw new InvalidDataException("Unknown ordered instruction.");
        var displays = Instructions.Where(i => i.Operation == OrderedScenarioOperation.DisplayConditions).ToArray();
        if (displays.Length != 1 || displays[0].Operand1 != 8 || displays[0].Operand2 is not (>= 0 and <= 255) || displays[0].TargetOffset is not null)
            throw new InvalidDataException("Exactly one condition-8 display with an unsigned-byte hold and no target is required.");
        display = displays[0];
        // Structural validation only: a three-byte display occupies the same span as a zero frame wait.
        // This private body is never executed; it reuses closed operand, marker, boundary and flow validation.
        validatedBody = new(id, scene, source, Instructions.Select(ForValidation), PreparedScenarioEvent.TaskBarrierProfile);
        (Id, ArtworkId, OwnerId, Source) = (id, artworkId, ownerId, source);
    }

    public static PreparedScenarioInstruction ForValidation(PreparedOrderedBannerInstruction instruction)
        => instruction.Operation == OrderedScenarioOperation.DisplayConditions
            ? new(instruction.Offset, ScenarioOperation.WaitFrames, 0)
            : new(instruction.Offset, Enum.Parse<ScenarioOperation>(instruction.Operation.ToString()), instruction.Operand1, instruction.Operand2, instruction.TargetOffset);

    public OrderedBannerProgram ToExecutionDefinition()
        => new(Id, validatedBody.ToSceneExecutionDefinition().Instructions.Select(i => i.Offset == display.Offset
            ? new OrderedBannerInstruction(i.Offset, new(new(DefinitionId, 8, display.Operand2!.Value), ArtworkId, OwnerId))
            : new OrderedBannerInstruction(i)));

    public byte[] ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(new EventDto(Format, 1, Profile, PreparedScenarioEvent.Dialect,
        Id.Value, ArtworkId.Value, OwnerId.Value, "Battle", false, 1, Source,
        Instructions.Select(i => new InstructionDto(i.Offset, i.Operation.ToString(), i.Operand1, i.Operand2, i.TargetOffset)).ToArray()), Options);

    public static PreparedOrderedBannerEvent Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > PreparedScenarioEvent.MaxJsonBytes) throw new InvalidDataException("Ordered event exceeds 1 MiB.");
        try
        {
            var dto = JsonSerializer.Deserialize<EventDto>(bytes.Span, Options) ?? throw new InvalidDataException("An ordered event document is required.");
            if (dto.Format != Format || dto.SchemaVersion != 1 || dto.Profile != Profile || dto.Dialect != PreparedScenarioEvent.Dialect ||
                dto.Scene != "Battle" || dto.Tutorial || dto.Speed != 1 || dto.Instructions.Length > 8188)
                throw new InvalidDataException("Unsupported ordered event format/profile/domain.");
            return new(new(dto.Id), new(dto.ArtworkId), new(dto.OwnerId), dto.Source, dto.Instructions.Select(i =>
            {
                if (i is null || !Enum.GetNames<OrderedScenarioOperation>().Contains(i.Operation)) throw new InvalidDataException("Unknown ordered operation.");
                return new PreparedOrderedBannerInstruction(i.Offset, Enum.Parse<OrderedScenarioOperation>(i.Operation), i.Operand1, i.Operand2, i.TargetOffset);
            }));
        }
        catch (Exception e) when (e is JsonException or ArgumentException or OverflowException)
        { throw new InvalidDataException("Invalid prepared ordered banner event: " + e.Message, e); }
    }

    public static PreparedOrderedBannerEvent Load(LooseAssetStore store, AssetId id)
    {
        var result = Parse(store.ReadAllBytes(id, PreparedScenarioEvent.MaxJsonBytes));
        if (result.Id != id) throw new InvalidDataException("Ordered event ID differs from manifest selection.");
        return result;
    }

    private sealed record EventDto(string Format, int SchemaVersion, string Profile, string Dialect, string Id, string ArtworkId,
        string OwnerId, string Scene, bool Tutorial, int Speed, ScenarioEventSource Source, InstructionDto[] Instructions);
    private sealed record InstructionDto(int Offset, string Operation, int? Operand1, int? Operand2, int? TargetOffset);
}
