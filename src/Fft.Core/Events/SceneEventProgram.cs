using System.Collections.Immutable;

namespace Fft.Core.Events;

/// <summary>A conditional operation, frame wait, or selected BATTLE task-class barrier.</summary>
public sealed class SceneEventInstruction
{
    public int Offset { get; }
    public ConditionalEventInstruction? Operation { get; }
    public int? WaitFrames { get; }
    public SceneTaskClass? TaskClass { get; }

    public SceneEventInstruction(ConditionalEventInstruction operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        (Offset, Operation) = (operation.Offset, operation);
    }

    public SceneEventInstruction(int offset, int waitFrames)
    {
        if (waitFrames is < 0 or > TimedEventProgram.MaxWaitFrames)
            throw new ArgumentOutOfRangeException(nameof(waitFrames), "Frame waits require counts 0..32767.");
        (Offset, WaitFrames) = (offset, waitFrames);
    }

    public SceneEventInstruction(int offset, SceneTaskClass taskClass)
    {
        if (!Enum.IsDefined(taskClass)) throw new ArgumentOutOfRangeException(nameof(taskClass), "Unsupported BATTLE task selector.");
        (Offset, TaskClass) = (offset, taskClass);
    }
}

/// <summary>Separate synthetic task-barrier profile. Its private conditional body validates control flow only.</summary>
public sealed class SceneEventProgram
{
    public AssetId Id => Body.Id;
    public ImmutableArray<SceneEventInstruction> Instructions { get; }
    internal ConditionalEventProgram Body { get; }

    public SceneEventProgram(AssetId id, IEnumerable<SceneEventInstruction> instructions)
    {
        if (!SceneTaskState.ValidId(id)) throw new ArgumentException("A namespaced program ID of at most 256 characters is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(instructions);
        Instructions = instructions.Take(ConditionalEventProgram.MaxInstructions + 1).ToImmutableArray();
        if (Instructions.Any(instruction => instruction is null))
            throw new ArgumentException("Scene operations must be nonnull.", nameof(instructions));
        Body = new(id, Instructions.Select(instruction => instruction.Operation ??
            new ConditionalEventInstruction(instruction.Offset, ConditionalEventOperation.Nop)));
    }

    /// <summary>Start one consumer using a fresh scene or an exited event's retained ledger. Never reset scene IDs/counters.</summary>
    public SceneEventState Start(ConditionalEventVariables variables, SceneTaskState tasks, AssetId consumerId)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (!SceneTaskState.ValidId(consumerId)) throw new ArgumentException("A nondefault consumer ID of at most 256 characters is required.", nameof(consumerId));
        if (tasks.BarrierStatus == SceneBarrierStatus.Pending)
            throw new ArgumentException("The scene already has a pending barrier consumer.", nameof(tasks));
        return new(this, Body.Start(variables), tasks, consumerId, SceneEventSuspension.Ready, 0, null);
    }
}
