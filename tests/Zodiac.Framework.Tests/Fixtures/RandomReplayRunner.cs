using Zodiac.Framework;
using Tessera.Engine;

namespace Zodiac.Framework.Tests.Fixtures;

// Test-only numeric replay; this is not a runtime persistence format or a movement-fixture extension.
internal static class RandomReplayRunner
{
    internal sealed record Report(bool Passed, int? DrawIndex, string? Field, string Diagnostic);

    internal static Report Run(FftRandomState initial, IReadOnlyList<RandomDraw<FftRandomState>> expected)
    {
        var random = new FftBiosRandom();
        FftRandomState state = initial;
        for (int i = 0; i < expected.Count; i++)
        {
            RandomDraw<FftRandomState> actual = random.Next(state);
            RandomDraw<FftRandomState> wanted = expected[i];
            if (wanted.State.Value != actual.State.Value)
                return Mismatch(i, "state.value", wanted.State.Value, actual.State.Value);
            if (wanted.Value != actual.Value)
                return Mismatch(i, "value", wanted.Value, actual.Value);
            if (wanted.State.DrawCount != actual.State.DrawCount)
                return Mismatch(i, "state.drawCount", wanted.State.DrawCount, actual.State.DrawCount);
            state = actual.State;
        }
        return new(true, null, null, $"Matched {expected.Count} ordered draws for {FftBiosRandom.AlgorithmId}.");
    }

    private static Report Mismatch<T>(int drawIndex, string field, T expected, T actual) =>
        new(false, drawIndex, field, $"draw {drawIndex + 1} (index {drawIndex}); {field}: expected {expected}, actual {actual}.");
}
