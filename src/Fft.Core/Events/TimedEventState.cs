namespace Fft.Core.Events;

/// <summary>Immutable continuation and pending wait. Offset is the next operation while WaitingAtOffset identifies the wait.</summary>
public sealed class TimedEventState
{
    public TimedEventProgram Program { get; }
    internal ConditionalEventState Inner { get; }
    public int Offset => Inner.Offset;
    public ConditionalEventVariables Variables => Inner.Variables;
    public bool Exited => Inner.Exited;
    public bool IsWaiting => RemainingWaitTicks > 0;
    public int RemainingWaitTicks { get; }
    public int? WaitingAtOffset { get; }
    public long ElapsedTicks { get; }

    internal TimedEventState(TimedEventProgram program, ConditionalEventState inner,
        int remainingWaitTicks, int? waitingAtOffset, long elapsedTicks)
        => (Program, Inner, RemainingWaitTicks, WaitingAtOffset, ElapsedTicks) =
            (program, inner, remainingWaitTicks, waitingAtOffset, elapsedTicks);
}
