using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed class ConditionalEventResult
{
    public bool Accepted => Rejection == ConditionalEventRejection.None;
    public ConditionalEventRejection Rejection { get; }
    public string? Diagnostic { get; }
    public int? FailureOffset { get; }
    public ConditionalEventState State { get; }
    public ImmutableArray<ConditionalEventTrace> Trace { get; }

    private ConditionalEventResult(ConditionalEventState state, ImmutableArray<ConditionalEventTrace> trace,
        ConditionalEventRejection rejection, string? diagnostic, int? failureOffset)
        => (State, Trace, Rejection, Diagnostic, FailureOffset) = (state, trace, rejection, diagnostic, failureOffset);

    internal static ConditionalEventResult Accept(ConditionalEventState state, ImmutableArray<ConditionalEventTrace> trace)
        => new(state, trace, ConditionalEventRejection.None, null, null);
    internal static ConditionalEventResult Reject(ConditionalEventState original, ConditionalEventRejection reason, string message, int offset)
        => new(original, [], reason, message, offset);
}
