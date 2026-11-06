using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed class TurnCycleResult
{
    public bool Accepted { get; }
    public TurnCycleRejection Rejection { get; }
    public TurnCycleState State { get; }
    public ImmutableArray<TurnCycleEvent> Events { get; }

    private TurnCycleResult(bool accepted, TurnCycleRejection rejection, TurnCycleState state, ImmutableArray<TurnCycleEvent> events)
    {
        Accepted = accepted;
        Rejection = rejection;
        State = state;
        Events = events;
    }

    internal static TurnCycleResult Reject(TurnCycleState state, TurnCycleRejection rejection) => new(false, rejection, state, []);
    internal static TurnCycleResult Accept(TurnCycleState state, IEnumerable<TurnCycleEvent> events) => new(true, TurnCycleRejection.None, state, events.ToImmutableArray());
}
