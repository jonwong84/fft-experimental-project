using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public enum AiLotteryReason { FirstRoll, AllRollsFailedUseFirst, MovementFallback }
public sealed record AiLotteryRoll(int Slot, uint Raw, int Modulo, int HitPercent, bool Passed);
internal sealed record AiRankedLotteryOutcome(ImmutableArray<AiLotteryRoll> Rolls, int? SelectedIndex, AiLotteryReason Reason);
internal sealed record AiRankedLotteryResult(bool Accepted, int? FailedIndex, AiRankedLotteryOutcome? Outcome, FftRandomState RngState);

/// <summary>Source ranked_select arithmetic for privately owned fresh ranks; no cached-rank public API.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-139/192/194.</remarks>
internal static class AiRankedLottery
{
    internal static AiRankedLotteryResult Evaluate(ImmutableArray<byte> hitPercentages, FftRandomState entryRng)
    {
        var rng = entryRng; var random = new FftBiosRandom(); bool any = false;
        var rolls = ImmutableArray.CreateBuilder<AiLotteryRoll>();
        for (int i = 0; i < hitPercentages.Length; i++)
        {
            int chance = hitPercentages[i]; any |= chance != 0;
            uint raw;
            try { var draw = random.Next(rng); rng = draw.State; raw = draw.Value; }
            catch (OverflowException) { return new(false, i, null, entryRng); }
            int modulo = (int)(raw % 100); bool passed = modulo < chance;
            rolls.Add(new(i, raw, modulo, chance, passed));
            if (passed) return new(true, null, new(rolls.ToImmutable(), i, AiLotteryReason.FirstRoll), rng);
        }
        return new(true, null, new(rolls.ToImmutable(), any ? 0 : null,
            any ? AiLotteryReason.AllRollsFailedUseFirst : AiLotteryReason.MovementFallback), rng);
    }
}
