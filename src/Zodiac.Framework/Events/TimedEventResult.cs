using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed class TimedEventResult
{
    public bool Accepted => Rejection == TimedEventRejection.None;
    public TimedEventRejection Rejection { get; }
    public string? Diagnostic { get; }
    public int? FailureOffset { get; }
    public TimedEventState State { get; }
    public ImmutableArray<TimedEventTrace> Trace { get; }
    public int InstructionsExecuted { get; }
    public int TicksConsumed { get; }

    private TimedEventResult(TimedEventState state, ImmutableArray<TimedEventTrace> trace,
        int instructionsExecuted, int ticksConsumed, TimedEventRejection rejection, string? diagnostic, int? failureOffset)
        => (State, Trace, InstructionsExecuted, TicksConsumed, Rejection, Diagnostic, FailureOffset) =
            (state, trace, instructionsExecuted, ticksConsumed, rejection, diagnostic, failureOffset);

    internal static TimedEventResult Accept(TimedEventState state, ImmutableArray<TimedEventTrace> trace, int instructions, int ticks)
        => new(state, trace, instructions, ticks, TimedEventRejection.None, null, null);
    internal static TimedEventResult Reject(TimedEventState state, TimedEventRejection reason, string message, int offset)
        => new(state, [], 0, 0, reason, message, offset);
}
