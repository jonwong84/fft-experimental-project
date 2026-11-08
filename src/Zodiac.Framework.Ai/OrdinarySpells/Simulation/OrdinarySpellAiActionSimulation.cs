using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>
/// Private candidate simulation: primary preparation, clock, ready strikes and absolute score.
/// Immutable inputs provide isolation; decision RNG is retained on ordinary NotValuable exits.
/// Unsupported work is rejected atomically by the query. No suspended-frame state is admitted.
/// </summary>
/// <remarks>
/// Bounded ai_action_simulate_and_score (0x8019D37C), fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.
/// Preparation, hit simulation, clock and scoring retain separate owners; no frame suspension or general reaction processing.
/// </remarks>
internal static class OrdinarySpellAiActionSimulation
{
    internal static OrdinarySpellAiCandidateResult AiActionSimulateAndScore(OrdinarySpellAiWork work,
        OrdinarySpellAiCandidateRequest request, RandomState rng)
    {
        OrdinarySpellAiCandidateResult Reject(string reason) =>
            new(OrdinarySpellAiCandidateDisposition.Rejected, reason, null, rng);
        byte counter = MagicCharge.CalculateSetup(work.Spell.RawChargeTime, false, false, work.Spell.Persevere).Ticks;
        OrdinarySpellAiCandidateResult NotValuable(StoredSpellAim? aim, OrdinarySpellAiClockOutcome? clock,
            RandomState? formulaEntry, OrdinarySpellAiHitOutcome? hit, RandomState exit) =>
            new(OrdinarySpellAiCandidateDisposition.NotValuable, null,
                new(work.Ability, counter, aim, work.Usefulness, clock, formulaEntry, hit?.Frames ?? [],
                    hit?.Accounting ?? new([], 0, 0, false), work.Units, null), exit);
        if (!work.Usefulness.Any(u => u.Effect == OrdinarySpellAiEffect.Useful))
            return NotValuable(null, null, null, null, rng);
        var units = work.Units;
        bool actFirst = work.SelectedOrigin?.ActBeforeMove ?? true;
        if (!actFirst) units = MovementPickupSimulate(work, units);
        var prepared = work.Prepare(request.Aim, request.FollowUnit);
        var stored = prepared.Plan.Aim;
        counter = prepared.Plan.Counter;
        int actorIndex = work.Units.IndexOf(work.Units.Single(u => u.Id == work.Actor.Id));
        units = units.SetItem(actorIndex, units[actorIndex] with { Current = prepared.Caster });
        OrdinarySpellAiHitOutcome? hit;
        OrdinarySpellAiClockOutcome clock;
        RandomState formulaEntry;
        RandomState exitRng;
        if (counter == 0)
        {
            formulaEntry = rng;
            bool valuable = OrdinarySpellAiHitSimulation.ActionHitsSimulate(work, units, stored, [], rng, out hit);
            if (!valuable) return NotValuable(stored, null, formulaEntry, hit, hit.RngState);
            units = hit.Units;
            if (units.Single(u => u.Id == work.Actor.Id).Current.Statuses.Dead)
                return Reject("Immediate caster KO/terminal clock simulation needs a separate profile.");
            if (actFirst) units = MovementPickupSimulate(work, units);
            var advanced = RunClock(work, units, counter, hit.RngState);
            if (!advanced.Accepted) return Reject("Simulation clock rejected: " + advanced.Rejection);
            clock = advanced.Outcome!; exitRng = advanced.RngState;
        }
        else
        {
            if (actFirst) units = MovementPickupSimulate(work, units);
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
            var caster = units[actorIndex].Current;
            units = units.SetItem(actorIndex, units[actorIndex] with
            {
                Current = caster with { Statuses = caster.Statuses with { Charging = false } }
            });
            formulaEntry = advanced.RngState;
            bool valuable = OrdinarySpellAiHitSimulation.ActionHitsSimulate(work, units, stored, clock.HadMoveOpportunity, formulaEntry, out hit);
            if (!valuable) return NotValuable(stored, clock, formulaEntry, hit, hit.RngState);
            units = hit.Units; exitRng = hit.RngState;
        }
        var score = work.Score(units, hit.Accounting.HitPercent, hit.Accounting.PriorityDelta);
        return new(OrdinarySpellAiCandidateDisposition.Scored, null,
            new(work.Ability, counter, stored, work.Usefulness, clock, formulaEntry, hit.Frames, hit.Accounting, units, score), exitRng);
    }

    /// <summary>Admitted movement_pickup_simulate: placement only; crystal/treasure effects remain gated.</summary>
    private static ImmutableArray<OrdinarySpellAiUnit> MovementPickupSimulate(OrdinarySpellAiWork work,
        ImmutableArray<OrdinarySpellAiUnit> units)
    {
        if (work.SelectedOrigin is not { } origin ||
            origin.MovementDestination == work.State.Units.Single(u => u.Id == work.Actor.Id).Position)
            return units;
        int actor = units.IndexOf(units.Single(u => u.Id == work.Actor.Id));
        return units.SetItem(actor, units[actor] with {
            Occupant = units[actor].Occupant with { Position = origin.MovementDestination }
        });
    }

    private static OrdinarySpellAiClockResult RunClock(OrdinarySpellAiWork work, ImmutableArray<OrdinarySpellAiUnit> units,
        byte counter, RandomState rng) => OrdinarySpellAiClock.Run(work.State.Turns.Units.Select(turn =>
        {
            var unit = units.Single(u => u.Id == turn.Id);
            return new OrdinarySpellAiClockUnit(unit.Id, unit.Occupant.SourceOrder, turn.Speed, turn.Ct,
                unit.Current.Statuses.Dead, turn.Id == work.Actor.Id ? work.MovementTaken : turn.MovementTaken,
                turn.Id == work.Actor.Id || turn.ActionTaken);
        }).ToImmutableArray(), work.Actor.Id, counter, work.State.Turns.Ticks, rng);
}
