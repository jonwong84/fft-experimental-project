using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Caller-owned proof that the immutable logical definition is ready; no artwork/file readiness claim.</summary>
public sealed record ReadyDarkScreenBinding(AssetId DefinitionId, AssetId OwnerId);
public sealed record StartDarkScreen(DarkScreenDefinition Definition, AssetId OwnerId);

/// <summary>A typed admission/release or existing authored scene instruction; never original bytecode.</summary>
public sealed class DarkScreenInstruction
{
    public int Offset => SceneInstruction.Offset;
    public SceneEventInstruction SceneInstruction { get; }
    public StartDarkScreen? Start { get; }
    public bool Release { get; }

    public DarkScreenInstruction(SceneEventInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        SceneInstruction = instruction;
    }

    public DarkScreenInstruction(int offset, StartDarkScreen start)
    {
        ArgumentNullException.ThrowIfNull(start);
        Start = start;
        SceneInstruction = new(new ConditionalEventInstruction(offset, ConditionalEventOperation.Nop));
    }

    /// <summary>A no-match release is an accepted no-op, including before admission and after completion.</summary>
    public DarkScreenInstruction(int releaseOffset)
    {
        Release = true;
        SceneInstruction = new(new ConditionalEventInstruction(releaseOffset, ConditionalEventOperation.Nop));
    }
}

public sealed class DarkScreenProgram
{
    public AssetId Id => Body.Id;
    public ImmutableArray<DarkScreenInstruction> Instructions { get; }
    internal SceneEventProgram Body { get; }
    internal StartDarkScreen? Start { get; }

    public DarkScreenProgram(AssetId id, IEnumerable<DarkScreenInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        Instructions = instructions.Take(ConditionalEventProgram.MaxInstructions + 1).ToImmutableArray();
        if (Instructions.Any(i => i is null)) throw new ArgumentException("Non-null instructions are required.", nameof(instructions));
        var starts = Instructions.Where(i => i.Start is not null).ToArray();
        if (starts.Length > 1) throw new ArgumentException("One syntactic admission is supported.", nameof(instructions));
        Start = starts.SingleOrDefault()?.Start;
        if (Start is { } start && (start.Definition is null || !SceneTaskState.ValidId(start.OwnerId)))
            throw new ArgumentException("A validated definition and owner ID are required.", nameof(instructions));
        Body = new(id, Instructions.Select(i => i.SceneInstruction));
    }

    public DarkScreenSceneState StartScene(ConditionalEventVariables variables, SceneTaskState tasks,
        AssetId consumerId, ReadyDarkScreenBinding? binding)
        => new(this, Body.Start(variables, tasks, consumerId), null, binding);
}

public sealed class DarkScreenSceneState
{
    public DarkScreenProgram Program { get; }
    public SceneEventState Event { get; }
    public DarkScreenState? Worker { get; }
    public ReadyDarkScreenBinding? ReadyBinding { get; }

    internal DarkScreenSceneState(DarkScreenProgram program, SceneEventState sceneEvent,
        DarkScreenState? worker, ReadyDarkScreenBinding? binding)
        => (Program, Event, Worker, ReadyBinding) = (program, sceneEvent, worker, binding);

    internal DarkScreenSceneState With(SceneEventState sceneEvent, DarkScreenState? worker) => new(Program, sceneEvent, worker, ReadyBinding);
}
