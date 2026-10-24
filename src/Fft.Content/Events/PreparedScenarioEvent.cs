using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fft.Core;
using Fft.Core.Events;

namespace Fft.Content.Events;

public enum ScenarioEventScene { Battle, World }
public enum ScenarioOperation { Nop, ZeroVariable, AddImmediate, AddVariable, CompareEqual, JumpForwardIfZero, JumpForward, ForwardTarget, ForwardTargetExit, SearchEnd, WaitFrames, WaitForTaskClass }
public sealed record PreparedScenarioInstruction(int Offset, ScenarioOperation Operation, int? Operand1 = null, int? Operand2 = null, int? TargetOffset = null);
public sealed record ScenarioEventSource(string Kind, string Edition, string ReferenceRevision, string BlockSha256,
    string Encoding, int TextOffset);

/// <summary>Validated immutable synthetic event definition with an explicitly selected closed execution profile.</summary>
public sealed class PreparedScenarioEvent
{
    public const string Format = "fft.prepared-scenario-event";
    public const string Profile = "sample:event-profile/conditional-v1";
    public const string TimedProfile = "sample:event-profile/non-tutorial-speed1-timed-v1";
    public const string TaskBarrierProfile = "sample:event-profile/non-tutorial-speed1-task-barrier-v1";
    public const string Dialect = "fft.ps1-us:scenario-bytecode";
    public const int MaxJsonBytes = 1024 * 1024;
    public AssetId Id { get; }
    public ScenarioEventScene Scene { get; }
    public ScenarioEventSource Source { get; }
    public string ExecutionProfile { get; }
    public ImmutableArray<PreparedScenarioInstruction> Instructions { get; }
    public int EntryOffset => 4;
    public int EndOffset => Instructions[^1].Offset + 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, AllowDuplicateProperties = false, MaxDepth = 12,
    };

    public PreparedScenarioEvent(AssetId id, ScenarioEventScene scene, ScenarioEventSource source,
        IEnumerable<PreparedScenarioInstruction> instructions, string executionProfile = Profile)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(instructions);
        if (string.IsNullOrWhiteSpace(id.Value) || id.Value.Length > 256 || !Enum.IsDefined(scene))
            throw new InvalidDataException("Prepared event requires an explicit stable ID and Battle/World scene.");
        if (executionProfile is not (Profile or TimedProfile or TaskBarrierProfile) || (executionProfile != Profile && scene != ScenarioEventScene.Battle))
            throw new InvalidDataException("Event requires a supported explicit profile; timed/task proofs require Battle, non-tutorial speed one.");
        ExecutionProfile = executionProfile;
        if (source.Kind != "synthetic" || source.Edition != "SCUS-94221" || !Hex(source.ReferenceRevision, 40) ||
            !Hex(source.BlockSha256, 64) || source.Encoding is not ("raw" or "processed") || source.TextOffset is < 5 or >= 8192)
            throw new InvalidDataException("Event requires synthetic provenance, edition, reference/hash and a preserved in-block text boundary.");
        var records = instructions.Take(8189).ToImmutableArray();
        if (records.Length is < 2 or > 8188) throw new InvalidDataException("Invalid instruction count.");
        int next = 4;
        foreach (var record in records)
        {
            if (record is null || !Enum.IsDefined(record.Operation) || record.Offset != next)
                throw new InvalidDataException("Instructions must be nonnull, known and contiguous from block offset four.");
            ValidateOperands(record);
            next = checked(next + Size(record.Operation));
            if (next > source.TextOffset) throw new InvalidDataException("Prepared instructions cross the source text boundary.");
        }
        if (records[^1].Operation != ScenarioOperation.SearchEnd || records.Take(records.Length - 1).Any(r => r.Operation == ScenarioOperation.SearchEnd))
            throw new InvalidDataException("Exactly one final SearchEnd sentinel is required.");
        for (int index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Operation is ScenarioOperation.JumpForward or ScenarioOperation.JumpForwardIfZero)
            {
                int target = FindTarget(records, index);
                if (record.TargetOffset != target) throw new InvalidDataException($"Jump at {record.Offset} does not name its first legal forward target landing.");
            }
        }
        ValidateReachability(records);
        (Id, Scene, Source, Instructions) = (id, scene, source, records);
    }

    public static int Size(ScenarioOperation operation) => operation switch
    {
        ScenarioOperation.ZeroVariable or ScenarioOperation.WaitFrames or ScenarioOperation.WaitForTaskClass => 3,
        ScenarioOperation.AddImmediate or ScenarioOperation.AddVariable => 5,
        ScenarioOperation.JumpForwardIfZero or ScenarioOperation.JumpForward or ScenarioOperation.ForwardTarget or ScenarioOperation.ForwardTargetExit => 2,
        ScenarioOperation.Nop or ScenarioOperation.CompareEqual or ScenarioOperation.SearchEnd => 1,
        _ => throw new InvalidDataException("Unknown prepared operation."),
    };

    /// <summary>Resolve by source marker order, including D0-to-D4 and landing after the marker.</summary>
    public static int FindTarget(IReadOnlyList<PreparedScenarioInstruction> records, int index)
    {
        var jump = records[index];
        if (jump.Operation is not (ScenarioOperation.JumpForward or ScenarioOperation.JumpForwardIfZero))
            throw new InvalidDataException("Target resolution requires a forward jump.");
        for (int scan = index + 1; scan < records.Count; scan++)
        {
            var record = records[scan];
            if (record.Operation == ScenarioOperation.SearchEnd) break;
            if (record.Operand1 == jump.Operand1 && (record.Operation == ScenarioOperation.ForwardTarget ||
                (jump.Operation == ScenarioOperation.JumpForwardIfZero && record.Operation == ScenarioOperation.ForwardTargetExit)))
                return record.Offset + 2;
        }
        throw new InvalidDataException($"No legal forward target for jump at {jump.Offset} before SearchEnd.");
    }

    private void ValidateOperands(PreparedScenarioInstruction r)
    {
        bool variable = r.Operand1 is >= 0 and <= 3;
        bool valid = r.Operation switch
        {
            ScenarioOperation.ZeroVariable => variable && r.Operand2 is null,
            ScenarioOperation.AddImmediate => variable && r.Operand2 is >= 0 and <= ushort.MaxValue,
            ScenarioOperation.AddVariable => variable && r.Operand2 is >= 0 and <= 3,
            ScenarioOperation.WaitFrames => ExecutionProfile is TimedProfile or TaskBarrierProfile && r.Operand1 is >= 0 and <= short.MaxValue && r.Operand2 is null,
            ScenarioOperation.WaitForTaskClass => ExecutionProfile == TaskBarrierProfile && r.Operand1 is 4 or 54 or 55 or 56 && r.Operand2 is null,
            ScenarioOperation.JumpForwardIfZero or ScenarioOperation.JumpForward or ScenarioOperation.ForwardTarget or ScenarioOperation.ForwardTargetExit
                => r.Operand1 is >= 0 and <= byte.MaxValue && r.Operand2 is null,
            _ => r.Operand1 is null && r.Operand2 is null,
        };
        bool jump = r.Operation is ScenarioOperation.JumpForwardIfZero or ScenarioOperation.JumpForward;
        if (!valid || (!jump && r.TargetOffset is not null) || (jump && r.TargetOffset is null))
            throw new InvalidDataException($"Invalid operands/target at source offset {r.Offset}.");
    }

    private static void ValidateReachability(ImmutableArray<PreparedScenarioInstruction> records)
    {
        var byOffset = records.ToDictionary(r => r.Offset);
        var pending = new Queue<int>(); pending.Enqueue(4);
        var seen = new HashSet<int>();
        while (pending.TryDequeue(out int offset))
        {
            if (!seen.Add(offset)) continue;
            if (!byOffset.TryGetValue(offset, out var record) || record.Operation == ScenarioOperation.SearchEnd)
                throw new InvalidDataException($"Control flow reaches missing code or unsupported EventEnd at {offset}.");
            if (record.Operation == ScenarioOperation.ForwardTargetExit) continue;
            if (record.Operation is ScenarioOperation.JumpForward or ScenarioOperation.JumpForwardIfZero) pending.Enqueue(record.TargetOffset!.Value);
            if (record.Operation != ScenarioOperation.JumpForward) pending.Enqueue(record.Offset + Size(record.Operation));
        }
    }

    public byte[] ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(new EventDto(Format, 1, ExecutionProfile, Dialect, Id.Value,
        Scene.ToString(), Source, Instructions.Select(r => new InstructionDto(r.Offset, r.Operation.ToString(), r.Operand1, r.Operand2, r.TargetOffset)).ToArray()), Options);

    public static PreparedScenarioEvent Parse(ReadOnlyMemory<byte> utf8)
    {
        if (utf8.Length > MaxJsonBytes) throw new InvalidDataException("Prepared event exceeds 1 MiB.");
        try
        {
            var dto = JsonSerializer.Deserialize<EventDto>(utf8.Span, Options) ?? throw new InvalidDataException("Event document is required.");
            if (dto.Format != Format || dto.SchemaVersion != 1 || dto.Profile is not (Profile or TimedProfile or TaskBarrierProfile) || dto.Dialect != Dialect || !Enum.GetNames<ScenarioEventScene>().Contains(dto.Scene))
                throw new InvalidDataException("Unsupported event format/schema/profile/dialect/scene.");
            if (dto.Instructions.Length > 8188 || dto.Instructions.Any(r => r is null)) throw new InvalidDataException("Invalid event instruction collection.");
            return new(new(dto.Id), Enum.Parse<ScenarioEventScene>(dto.Scene), dto.Source,
                dto.Instructions.Select(r => new PreparedScenarioInstruction(r.Offset, ParseOperation(r.Operation), r.Operand1, r.Operand2, r.TargetOffset)), dto.Profile);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or OverflowException)
        { throw new InvalidDataException($"Invalid prepared scenario event: {exception.Message}", exception); }
    }

    public static PreparedScenarioEvent Load(LooseAssetStore store, AssetId id)
    {
        ArgumentNullException.ThrowIfNull(store);
        var definition = Parse(store.ReadAllBytes(id));
        if (definition.Id != id) throw new InvalidDataException("Prepared event ID differs from its manifest selection.");
        return definition;
    }

    /// <summary>Project validated bytecode structure into Core's closed semantic contract; DB and labels are not executable.</summary>
    public ConditionalEventProgram ToExecutionDefinition()
    {
        if (ExecutionProfile != Profile) throw new InvalidDataException("Conditional execution requires the explicit conditional profile.");
        return new(Id, Instructions.Where(r => r.Operation != ScenarioOperation.SearchEnd).Select(ToConditionalInstruction));
    }

    public TimedEventProgram ToTimedExecutionDefinition()
    {
        if (ExecutionProfile != TimedProfile) throw new InvalidDataException("Timed execution requires the explicit timed profile.");
        return new(Id, Instructions.Where(r => r.Operation != ScenarioOperation.SearchEnd).Select(r =>
            r.Operation == ScenarioOperation.WaitFrames ? new TimedEventInstruction(r.Offset, r.Operand1!.Value) : new(ToConditionalInstruction(r))));
    }

    public SceneEventProgram ToSceneExecutionDefinition()
    {
        if (ExecutionProfile != TaskBarrierProfile) throw new InvalidDataException("Scene execution requires the explicit task-barrier profile.");
        return new(Id, Instructions.Where(r => r.Operation != ScenarioOperation.SearchEnd).Select(r => r.Operation switch
        {
            ScenarioOperation.WaitFrames => new SceneEventInstruction(r.Offset, r.Operand1!.Value),
            ScenarioOperation.WaitForTaskClass => new SceneEventInstruction(r.Offset, (SceneTaskClass)r.Operand1!.Value),
            _ => new SceneEventInstruction(ToConditionalInstruction(r)),
        }));
    }

    private static ConditionalEventInstruction ToConditionalInstruction(PreparedScenarioInstruction r) => r.Operation switch
        {
            ScenarioOperation.Nop => new ConditionalEventInstruction(r.Offset, ConditionalEventOperation.Nop),
            ScenarioOperation.ZeroVariable => new(r.Offset, ConditionalEventOperation.ZeroVariable, r.Operand1),
            ScenarioOperation.AddImmediate => new(r.Offset, ConditionalEventOperation.AddImmediate, r.Operand1, r.Operand2),
            ScenarioOperation.AddVariable => new(r.Offset, ConditionalEventOperation.AddVariable, r.Operand1, r.Operand2),
            ScenarioOperation.CompareEqual => new(r.Offset, ConditionalEventOperation.CompareEqual),
            ScenarioOperation.JumpForwardIfZero => new(r.Offset, ConditionalEventOperation.JumpForwardIfZero, TargetOffset: r.TargetOffset),
            ScenarioOperation.JumpForward => new(r.Offset, ConditionalEventOperation.JumpForward, TargetOffset: r.TargetOffset),
            ScenarioOperation.ForwardTarget => new(r.Offset, ConditionalEventOperation.Target),
            ScenarioOperation.ForwardTargetExit => new(r.Offset, ConditionalEventOperation.Exit),
            _ => throw new InvalidDataException("Unsupported executable scenario operation."),
        };
    private static ScenarioOperation ParseOperation(string text) => Enum.GetNames<ScenarioOperation>().Contains(text)
        ? Enum.Parse<ScenarioOperation>(text) : throw new InvalidDataException($"Unknown prepared operation '{text}'.");
    private static bool Hex(string? value, int count) => value is not null && value.Length == count && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private sealed record EventDto(string Format, int SchemaVersion, string Profile, string Dialect, string Id, string Scene,
        ScenarioEventSource Source, InstructionDto[] Instructions);
    private sealed record InstructionDto(int Offset, string Operation, int? Operand1, int? Operand2, int? TargetOffset);
}
