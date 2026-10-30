using Tessera.Engine;

namespace Zodiac.Framework;

/// <summary>The full unsigned BIOS RNG word and the number of outputs consumed since seeding.</summary>
public readonly record struct FftRandomState(uint Value, ulong DrawCount);

/// <summary>The FFT-owned generator verified against the supplied US v3.0 BIOS, without a runtime BIOS dependency.</summary>
/// <remarks>
/// Evidence: docs/behavior/bios-rng-verification.md and docs/fixtures/rng/scph5501-rng-vectors.json;
/// FFT decomp revision 611b504d25d4a9b03fab8023d7dbea75fe381d5d. This does not establish gameplay draw ordering.
/// </remarks>
public sealed class FftBiosRandom : IDeterministicRandom<FftRandomState>
{
    private const uint Multiplier = 0x41c64e6du, Increment = 0x3039u;
    public const string AlgorithmId = "fft.ps1:rng/bios-v3-us";

    /// <summary>Preserves all 32 seed bits and consumes no random output.</summary>
    public static FftRandomState Seed(uint seed) => new(seed, 0);

    /// <summary>Advances modulo 2^32, returning bits 16..30 of the updated word and one counted draw.</summary>
    /// <exception cref="OverflowException">The explicit draw counter cannot represent another output.</exception>
    public RandomDraw<FftRandomState> Next(FftRandomState state)
    {
        ulong drawCount = checked(state.DrawCount + 1);
        uint value = unchecked(state.Value * Multiplier + Increment);
        uint output = (value >> 16) & 0x7fffu;
        return new RandomDraw<FftRandomState>(new FftRandomState(value, drawCount), output);
    }

    // Compose affine UInt32 transitions by exponentiation, without enumerating reported draws.
    internal static bool IsForwardCheckpoint(FftRandomState before, FftRandomState after)
    {
        if (after.DrawCount < before.DrawCount) return false;
        ulong remaining = after.DrawCount - before.DrawCount;
        uint multiplier = Multiplier, increment = Increment, accumulatedMultiplier = 1, accumulatedIncrement = 0;
        while (remaining != 0)
        {
            if ((remaining & 1) != 0)
            {
                accumulatedMultiplier = unchecked(accumulatedMultiplier * multiplier);
                accumulatedIncrement = unchecked(accumulatedIncrement * multiplier + increment);
            }
            increment = unchecked((multiplier + 1) * increment);
            multiplier = unchecked(multiplier * multiplier);
            remaining >>= 1;
        }
        return unchecked(before.Value * accumulatedMultiplier + accumulatedIncrement) == after.Value;
    }
}
