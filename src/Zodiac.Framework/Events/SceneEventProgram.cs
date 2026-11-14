using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

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
