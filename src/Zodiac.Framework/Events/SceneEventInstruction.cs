using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

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
