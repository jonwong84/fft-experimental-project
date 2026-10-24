using Xunit;

namespace Zodiac.Engine.Tests;

public sealed class DeterministicRandomTests
{
    // Synthetic stream with a different state shape; no game recurrence or state-width assumption.
    private readonly record struct CounterState(int Position, string Label);

    private sealed class LabeledCounter : IDeterministicRandom<CounterState>
    {
        public RandomDraw<CounterState> Next(CounterState state) =>
            new(state with { Position = checked(state.Position + 1) }, (uint)state.Label.Length);
    }

    [Fact]
    public void Contract_threads_arbitrary_immutable_state_and_retains_non_numeric_fields()
    {
        IDeterministicRandom<CounterState> source = new LabeledCounter();
        var original = new CounterState(10, "relay");
        RandomDraw<CounterState> first = source.Next(original);
        RandomDraw<CounterState> second = source.Next(first.State);
        Assert.Equal(new CounterState(10, "relay"), original);
        Assert.Equal(new CounterState(11, "relay"), first.State);
        Assert.Equal(new CounterState(12, "relay"), second.State);
        Assert.Equal(5u, first.Value);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(first, source.Next(original));
    }

    [Fact]
    public void One_provider_can_advance_independent_state_streams_without_hidden_progress()
    {
        IDeterministicRandom<CounterState> source = new LabeledCounter();
        var left = new CounterState(0, "a");
        var right = new CounterState(50, "other");
        RandomDraw<CounterState> a = source.Next(left);
        RandomDraw<CounterState> b = source.Next(right);
        RandomDraw<CounterState> a2 = source.Next(a.State);
        Assert.Equal(new CounterState(2, "a"), a2.State);
        Assert.Equal(new CounterState(51, "other"), b.State);
        Assert.Equal(1u, a2.Value);
        Assert.Equal(5u, b.Value);
        Assert.Equal(a, source.Next(left));
    }
}
