using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One supplied initial aim at the actor's current origin. No move, target search or retained-plan authority.</summary>
public sealed record OrdinarySpellAiCandidateRequest(BattleUnitId ActorId, int PrimarySlot, TilePosition Aim, bool FollowUnit = true);
public enum OrdinarySpellAiCandidateDisposition { Rejected, NotValuable, Scored, ScoredUnresolved }
public sealed record OrdinarySpellAiCandidateOutcome(OrdinarySpellAiConsidered Ability, byte ActualChargeTime,
    StoredSpellAim? Aim, ImmutableArray<OrdinarySpellAiUsefulTarget> Usefulness, OrdinarySpellAiClockOutcome? Clock,
    RandomState? FormulaEntryRng, ImmutableArray<MagicCastTargetResult> Frames,
    OrdinarySpellAiAccountingOutcome Accounting, ImmutableArray<OrdinarySpellAiUnit> Units, OrdinarySpellAiScore? Score);
public sealed record OrdinarySpellAiCandidateResult(OrdinarySpellAiCandidateDisposition Disposition, string? Rejection,
    OrdinarySpellAiCandidateOutcome? Outcome, RandomState RngState)
{
    public bool Accepted => Disposition != OrdinarySpellAiCandidateDisposition.Rejected;
}

/// <summary>Fresh fixed-origin ordinary spell simulation through source clock/readiness/scoring. No live adoption.</summary>
public static class OrdinarySpellAiCandidateQuery
{
    public static OrdinarySpellAiCandidateResult Evaluate(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiCandidateRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(request);
        OrdinarySpellAiCandidateResult Reject(string reason) => new(OrdinarySpellAiCandidateDisposition.Rejected, reason, null, rng);
        if (state.ClockPhase != OrdinaryMagicClockPhase.UnitTurn || state.Units.Length > 21 ||
            state.Units.Any(u => u.Occupant.SourceOrder is < 0 or > 20))
            return Reject("The fixed-origin source clock profile is unavailable.");
        try
        {
            var work = new OrdinarySpellAiWork(state, catalog, request.ActorId, request.PrimarySlot, request.FollowUnit);
            byte counter = MagicCharge.CalculateSetup(work.Spell.RawChargeTime, false, false, work.Spell.Persevere).Ticks;
            OrdinarySpellAiCandidateResult NotValuable(StoredSpellAim? aim, OrdinarySpellAiClockOutcome? clock,
                RandomState? formulaEntry, OrdinarySpellAiHitOutcome? hit, RandomState exit) =>
                new(OrdinarySpellAiCandidateDisposition.NotValuable, null,
                    new(work.Ability, counter, aim, work.Usefulness, clock, formulaEntry, hit?.Frames ?? [],
                        hit?.Accounting ?? new([], 0, 0, false), work.Units, null), exit);
            if (!work.Usefulness.Any(u => u.Effect == OrdinarySpellAiEffect.Useful))
                return NotValuable(null, null, null, null, rng);
            var stored = work.Capture(request.Aim, request.FollowUnit);
            var units = work.Units;
            OrdinarySpellAiHitOutcome? hit;
            OrdinarySpellAiClockOutcome clock;
            RandomState formulaEntry;
            RandomState exitRng;
            if (counter == 0)
            {
                formulaEntry = rng;
                hit = OrdinarySpellAiHitWork.Resolve(work, units, stored, [], rng);
                if (!hit.Accounting.Valuable) return NotValuable(stored, null, formulaEntry, hit, hit.RngState);
                units = hit.Units;
                if (units.Single(u => u.Id == work.Actor.Id).Current.Statuses.Dead)
                    return Reject("Immediate caster KO/terminal clock simulation needs a separate profile.");
                var advanced = RunClock(work, units, counter, hit.RngState);
                if (!advanced.Accepted) return Reject("Simulation clock rejected: " + advanced.Rejection);
                clock = advanced.Outcome!; exitRng = advanced.RngState;
            }
            else
            {
                int index = units.IndexOf(units.Single(u => u.Id == work.Actor.Id));
                units = units.SetItem(index, units[index] with { Current = units[index].Current with { Statuses = new(Charging: true) } });
                var advanced = RunClock(work, units, counter, rng);
                if (!advanced.Accepted) return Reject("Simulation clock rejected: " + advanced.Rejection);
                clock = advanced.Outcome!;
                if (clock.EventLimitReached)
                {
                    var unresolved = OrdinarySpellAiScoring.CalculateUnresolved(work.ScoreUnits(units));
                    return new(OrdinarySpellAiCandidateDisposition.ScoredUnresolved, null,
                        new(work.Ability, counter, stored, work.Usefulness, clock, null, [], new([], 0, 255, false), units, unresolved), advanced.RngState);
                }
                // Own readiness bookkeeping before the shared ready formula/resource entry.
                units = units.SetItem(index, units[index] with { Current = units[index].Current with { Statuses = new() } });
                formulaEntry = advanced.RngState;
                hit = OrdinarySpellAiHitWork.Resolve(work, units, stored, clock.HadMoveOpportunity, formulaEntry);
                if (!hit.Accounting.Valuable) return NotValuable(stored, clock, formulaEntry, hit, hit.RngState);
                units = hit.Units; exitRng = hit.RngState;
            }
            var score = work.Score(units, hit.Accounting.HitPercent, hit.Accounting.PriorityDelta);
            return new(OrdinarySpellAiCandidateDisposition.Scored, null,
                new(work.Ability, counter, stored, work.Usefulness, clock, formulaEntry, hit.Frames, hit.Accounting, units, score), exitRng);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell simulation: " + e.Message);
        }
    }

    private static OrdinarySpellAiClockResult RunClock(OrdinarySpellAiWork work, ImmutableArray<OrdinarySpellAiUnit> units,
        byte counter, RandomState rng) => OrdinarySpellAiClock.Run(work.State.Turns.Units.Select(turn =>
        {
            var unit = units.Single(u => u.Id == turn.Id);
            return new OrdinarySpellAiClockUnit(unit.Id, unit.Occupant.SourceOrder, turn.Speed, turn.Ct,
                unit.Current.Statuses.Dead, turn.MovementTaken, turn.Id == work.Actor.Id || turn.ActionTaken);
        }).ToImmutableArray(), work.Actor.Id, counter, work.State.Turns.Ticks, rng);
}
