using Zodiac.Engine;

namespace Samples.Relay;

/// <summary>An invented courier crossing a small board; no game assets or FFT rules.</summary>
public static class RelayScenario
{
    public sealed record State(GridPosition Courier, int Deliveries);
    public sealed record Outcome(State State, bool Accepted);

    public static State Initial => new(new GridPosition(0, 0, 0), 0);

    public static Outcome Move(State state, GridPosition destination)
    {
        var reachable = FlatGridMovementRange.Calculate(new GridBounds(4, 2, 1), state.Courier, move: 2);
        if (!reachable.Contains(destination))
            return new(state, false);

        int deliveries = state.Deliveries + (destination == new GridPosition(3, 1, 0) ? 1 : 0);
        return new(new State(destination, deliveries), true);
    }
}
