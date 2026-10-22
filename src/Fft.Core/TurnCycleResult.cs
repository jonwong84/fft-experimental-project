using System.Collections.Immutable;

namespace Fft.Core;

public enum TurnCommandKind
{
    ConfirmMove,
    ConfirmAct,
    Wait,
}

/// <summary>A confirmed choice-consumption or end-turn decision; ConfirmAct does not resolve combat.</summary>
public sealed record TurnCommand(BattleUnitId UnitId, TurnCommandKind Kind);

public enum TurnCycleRejection
{
    None,
    TurnAlreadyActive,
    UnknownUnit,
    InactiveUnit,
    MovementAlreadyTaken,
    ActionAlreadyTaken,
    UnsupportedCommand,
    TickCountExhausted,
    RandomDrawCountExhausted,
}

public abstract record TurnCycleEvent;
public sealed record TurnClockAdvancedEvent(ulong Tick) : TurnCycleEvent;
public sealed record TurnReadyEvent(BattleUnitId UnitId, int CtBeforeSelection, int CtAfterSelection, uint UpkeepRoll) : TurnCycleEvent;
public sealed record TurnChoiceConsumedEvent(BattleUnitId UnitId, TurnCommandKind Choice) : TurnCycleEvent;
public sealed record TurnEndedEvent(BattleUnitId UnitId, int CtBeforeCompletion, int CtAfterCompletion) : TurnCycleEvent;

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
