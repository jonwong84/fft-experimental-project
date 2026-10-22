namespace Zodiac.Engine;

/// <summary>One random value and the explicit state to use for the next draw.</summary>
public readonly record struct RandomDraw<TState>(TState State, uint Value);

/// <summary>A deterministic random transition with no ambient mutable state.</summary>
/// <remarks>Value interpretation and any range conversion belong to the consuming game policy.</remarks>
public interface IDeterministicRandom<TState>
{
    RandomDraw<TState> Next(TState state);
}
