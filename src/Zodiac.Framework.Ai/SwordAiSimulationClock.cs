using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

internal sealed record SwordAiClockOutcome(ImmutableArray<SwordAiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks,
    ImmutableArray<SwordAiUpkeepPick> UpkeepPicks, FftRandomState RngState);

/// <summary>Private clock shared by fresh, certified hypothetical evaluations; never accepts public cached results.</summary>
internal static class SwordAiSimulationClock
{
    // Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, turn/clock.c and ai/evaluation.c.
    // Mutates only the caller's private unit array. Rejection publishes no partial outcome or RNG.
    internal static SwordAiCandidateRejection Advance(SwordAiProjectedUnit[] units, ulong ticks,
        FftRandomState decisionRng, out SwordAiClockOutcome? outcome)
    {
        outcome = null;
        var rng = decisionRng;
        var random = new FftBiosRandom();
        ulong elapsed = 0;
        var picks = ImmutableArray.CreateBuilder<SwordAiUpkeepPick>();
        while (true)
        {
            int best = -1;
            for (int i = 0; i < units.Length; i++)
                if (units[i].Ct >= 100 && (best < 0 || units[i].Ct > units[best].Ct ||
                    units[i].Ct == units[best].Ct && units[i].TieKey < units[best].TieKey)) best = i;
            if (best >= 0)
            {
                var selected = units[best];
                int remainder = selected.Ct % 100;
                try
                {
                    var draw = random.Next(rng); rng = draw.State;
                    picks.Add(new(selected.UnitId, selected.Ct, remainder, selected.Status == AttackUnitStatus.Dead, draw.Value));
                }
                catch (OverflowException) { return SwordAiCandidateRejection.RandomDrawCountExhausted; }
                units[best] = selected with { Ct = remainder };
                // In AI mode a corpse gets upkeep draws/modulo, but no turn or death-counter decay.
                if (selected.Status == AttackUnitStatus.Dead) continue;
                units[best] = units[best] with { MovementTaken = false, ActionTaken = false };
                outcome = new(units.ToImmutableArray(), selected.UnitId, elapsed, ticks, picks.ToImmutable(), rng);
                return SwordAiCandidateRejection.None;
            }
            // End-turn phases are effect-free under the explicit certificate. Check PickUnit before AddCt.
            // A positive-speed living actor guarantees readiness within 100 additions, including after KO.
            if (ticks == ulong.MaxValue) return SwordAiCandidateRejection.TickCountExhausted;
            ticks++; elapsed++;
            for (int i = 0; i < units.Length; i++) units[i] = units[i] with { Ct = Math.Min(254, units[i].Ct + units[i].Speed) };
        }
    }
}
