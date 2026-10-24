using System.Text.Json;
using Fft.Core;
using Xunit;
using Zodiac.Engine;

namespace Fft.Core.Tests;

public sealed class FftBiosRandomTests
{
    [Fact]
    public void Every_recorded_bios_draw_matches_ordered_state_and_output()
    {
        // Committed numeric ROM-execution observations only; no BIOS bytes, captures or C runtime needed.
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "scph5501-rng-vectors.json")));
        Assert.Equal("fft.bios-rng-vectors", fixture.RootElement.GetProperty("format").GetString());
        Assert.Equal("independently-observed", fixture.RootElement.GetProperty("confidence").GetString());
        IDeterministicRandom<FftRandomState> random = new FftBiosRandom();
        int checkedDraws = 0;
        foreach (JsonElement sequence in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            FftRandomState state = FftBiosRandom.Seed(sequence.GetProperty("seed").GetUInt32());
            Assert.Equal(sequence.GetProperty("seedState").GetUInt32(), state.Value);
            Assert.Equal(0ul, state.DrawCount);
            foreach (JsonElement expected in sequence.GetProperty("draws").EnumerateArray())
            {
                Assert.Equal(expected.GetProperty("stateBefore").GetUInt32(), state.Value);
                FftRandomState before = state;
                RandomDraw<FftRandomState> actual = random.Next(state);
                Assert.Equal(before, state);
                Assert.Equal(expected.GetProperty("stateAfter").GetUInt32(), actual.State.Value);
                Assert.Equal(expected.GetProperty("value").GetUInt32(), actual.Value);
                Assert.Equal(expected.GetProperty("draw").GetUInt64(), actual.State.DrawCount);
                Assert.InRange(actual.Value, 0u, 32767u);
                state = actual.State;
                checkedDraws++;
            }
        }
        Assert.Equal(64, checkedDraws);
        Assert.Equal("fft.ps1:rng/bios-v3-us", FftBiosRandom.AlgorithmId);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(0x7fffffffu)]
    [InlineData(0x80000000u)]
    [InlineData(uint.MaxValue)]
    public void Seed_preserves_all_32_bits_and_reseeding_restarts_history(uint seed)
    {
        var random = new FftBiosRandom();
        FftRandomState initial = FftBiosRandom.Seed(seed);
        Assert.Equal(new FftRandomState(seed, 0), initial);
        RandomDraw<FftRandomState> first = random.Next(initial);
        random.Next(random.Next(first.State).State);
        Assert.Equal(first, random.Next(FftBiosRandom.Seed(seed)));
        Assert.Equal(new FftRandomState(seed, 0), initial);
    }

    [Fact]
    public void Shared_provider_interleaving_does_not_change_either_stream()
    {
        var random = new FftBiosRandom();
        FftRandomState a = FftBiosRandom.Seed(1);
        FftRandomState b = FftBiosRandom.Seed(uint.MaxValue);
        for (int i = 0; i < 20; i++)
        {
            RandomDraw<FftRandomState> left = random.Next(a);
            RandomDraw<FftRandomState> right = random.Next(b);
            Assert.Equal(left, new FftBiosRandom().Next(a));
            Assert.Equal(right, new FftBiosRandom().Next(b));
            Assert.Equal(left, random.Next(a));
            a = left.State;
            b = right.State;
        }
        Assert.Equal(20ul, a.DrawCount);
        Assert.Equal(20ul, b.DrawCount);
    }

    [Theory]
    [InlineData(0xfc77a683u, 0u, 0u)]
    [InlineData(0x0dbdbb1eu, uint.MaxValue, 32767u)]
    public void State_arithmetic_wraps_modulo_32_bits_even_when_called_from_checked_code(
        uint initial, uint expectedState, uint expectedValue)
    {
        // Independently recorded boundary seeds in the BIOS fixture exercise multiply/add overflow.
        RandomDraw<FftRandomState> actual;
        checked { actual = new FftBiosRandom().Next(new(initial, 7)); }
        Assert.Equal(new FftRandomState(expectedState, 8), actual.State);
        Assert.Equal(expectedValue, actual.Value);
    }

    [Fact]
    public void Draw_count_overflow_fails_without_mutating_the_input()
    {
        var random = new FftBiosRandom();
        var before = new FftRandomState(1, ulong.MaxValue);
        Assert.Throws<OverflowException>(() => random.Next(before));
        Assert.Equal(new FftRandomState(1, ulong.MaxValue), before);
        RandomDraw<FftRandomState> last = random.Next(new(1, ulong.MaxValue - 1));
        Assert.Equal(ulong.MaxValue, last.State.DrawCount);
        Assert.Throws<OverflowException>(() => random.Next(last.State));
        Assert.Equal(random.Next(FftBiosRandom.Seed(1)), new FftBiosRandom().Next(FftBiosRandom.Seed(1)));
    }
}
