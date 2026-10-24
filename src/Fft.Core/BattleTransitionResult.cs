using System.Collections.ObjectModel;

namespace Fft.Core;

public enum BattleTransitionRejection
{
    None,
    UnknownUnit,
    UnitHasAlreadyMoved,
    DestinationOccupied,
    DestinationNotReachable,
}

/// <summary>The result of applying one command to an immutable battle snapshot.</summary>
public sealed class BattleTransitionResult
{
    private BattleTransitionResult(
        bool accepted,
        BattleTransitionRejection rejection,
        BattleState state,
        IReadOnlyList<BattleMovementEvent> events)
    {
        Accepted = accepted;
        Rejection = rejection;
        State = state;
        Events = events;
    }

    public bool Accepted { get; }

    public BattleTransitionRejection Rejection { get; }

    public BattleState State { get; }

    public IReadOnlyList<BattleMovementEvent> Events { get; }

    internal static BattleTransitionResult Reject(BattleState state, BattleTransitionRejection reason) =>
        new(false, reason, state, Array.Empty<BattleMovementEvent>());

    internal static BattleTransitionResult Move(BattleState state, BattleMovementEvent movementEvent) =>
        new(true, BattleTransitionRejection.None, state, new ReadOnlyCollection<BattleMovementEvent>([movementEvent]));
}
