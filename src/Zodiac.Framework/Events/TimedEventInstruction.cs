using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Either a supported synchronous operation or a non-tutorial, speed-one frame wait.</summary>
public sealed class TimedEventInstruction
{
    public int Offset { get; }
    public ConditionalEventInstruction? Operation { get; }
    public int? WaitFrames { get; }

    public TimedEventInstruction(ConditionalEventInstruction operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        (Offset, Operation) = (operation.Offset, operation);
    }

    public TimedEventInstruction(int offset, int waitFrames)
    {
        if (waitFrames is < 0 or > TimedEventProgram.MaxWaitFrames)
            throw new ArgumentOutOfRangeException(nameof(waitFrames), "Wait proof admits signed-halfword counts 0..32767 only.");
        (Offset, WaitFrames) = (offset, waitFrames);
    }
}
