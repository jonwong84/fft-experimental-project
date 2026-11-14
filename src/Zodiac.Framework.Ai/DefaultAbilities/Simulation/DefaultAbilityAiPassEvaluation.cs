using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Shared fresh origin/aim scan after family admission. No public cached-work boundary or live publication.</summary>
/// <remarks>origin_reachable_evaluate, origin_aim_tiles_evaluate, ai_action_evaluate; reference 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
internal static class DefaultAbilityAiPassEvaluation
{
    internal static DefaultAbilityAiPassSearchResult OriginReachableEvaluate(DefaultAbilityAiWork work,
        DefaultAbilityAiOriginOutcome origins, DefaultAbilityAiOriginPass pass, ImmutableArray<DefaultAbilityAiOrigin> choices,
        RandomState rng, bool consumeAims, DefaultAbilityAiPassBest? bestAtEntry = null)
    {
        var remaining = pass.Potential.Aims.Select(a => a.Position).ToHashSet();
        var traces = ImmutableArray.CreateBuilder<DefaultAbilityAiOriginTrace>();
        // Only the fresh ability caller supplies this seed. Source unit/tile passes share best_action,
        // while ai_cast_aim_tiles_build clears and rebuilds the remaining aims for every pass.
        DefaultAbilityAiPassBest? best = bestAtEntry;
        var selfEffect = work.Usefulness.Single(u => u.UnitId == work.Actor.Id).Effect;
        if (selfEffect == DefaultAbilityAiEffect.Useful)
            throw new NotSupportedException("Useful-self origin consumption requires its separate source pathway.");
        ImmutableArray<DefaultAbilityAiAim> Remaining() => pass.Potential.Aims.Where(a => remaining.Contains(a.Position)).ToImmutableArray();
        foreach (var origin in choices)
        {
            // Ranked source scans stop before taking another origin once the global aim byte is zero.
            if (consumeAims && pass.RankedOrigins && remaining.Count == 0) break;
            traces.Add(OriginAimTilesEvaluate(origin));
        }
        var baseline = origins.Movement.BaselineRank;
        bool passes = best is not null && AiCandidateRank.Compare(best.Evaluation.Candidate.Outcome!.Score!.Priority,
            best.Evaluation.Candidate.Outcome.Score.HitPercent, baseline.Priority, baseline.HitPercent) == AiRankOrder.Above;
        return new(true, null, new(origins, pass, traces.ToImmutable(), best, Remaining(), passes), rng);

        DefaultAbilityAiOriginTrace OriginAimTilesEvaluate(DefaultAbilityAiOrigin origin)
        {
            var entryRng = rng;
            var entryAims = Remaining();
            var evaluations = ImmutableArray.CreateBuilder<DefaultAbilityAiAimEvaluation>();
            var consumed = ImmutableArray.CreateBuilder<DefaultAbilityAiAim>();
            var selectedWork = work.ForOrigin(pass.Potential, origins.Potential.Ability.Usage, origin);
            byte proximity = origins.Movement.Cells.Single(c => c.Tile == origin.MovementDestination).Proximity;
            bool stopped = false;
            foreach (var aim in origin.Aims)
            {
                if (!remaining.Contains(aim.Position)) continue;
                var candidate = DefaultAbilityAiActionSimulation.AiActionSimulateAndScore(selectedWork,
                    new(work.Actor.Id, work.Ability.Entry.Slot.Index, aim.Position, aim.UnitId is not null), rng);
                if (!candidate.Accepted) throw new NotSupportedException(candidate.Rejection);
                var score = candidate.Outcome!.Score;
                bool improves = score is not null && AiRankIsHigher(score, proximity, best);
                var evaluated = new DefaultAbilityAiAimEvaluation(aim, rng, candidate, improves);
                evaluations.Add(evaluated);
                rng = candidate.RngState; // Normal NotValuable attempts retain all their real draws.
                if (improves) best = new(origin, proximity, evaluated);
                // Comparison precedes processing_done. No aim consumption happens on an unresolved exit.
                if (candidate.Disposition == DefaultAbilityAiCandidateDisposition.ScoredUnresolved)
                {
                    stopped = true;
                    break;
                }
                // Source self_hit is set after exactly these accounting exclusions, regardless of Valuable.
                bool selfHit = candidate.Outcome.Accounting.IncludedTargets.Contains(work.Actor.Id);
                bool consume = pass.Potential.ConsumeEvaluatedAims ||
                    pass.RankedOrigins && selfEffect == DefaultAbilityAiEffect.Adverse && !selfHit;
                if (consumeAims && consume)
                {
                    remaining.Remove(aim.Position);
                    consumed.Add(aim);
                    if (remaining.Count == 0) break;
                }
            }
            return new(origin, entryRng, rng, entryAims, Remaining(), consumed.ToImmutable(), evaluations.ToImmutable(), stopped);
        }
    }

    private static bool AiRankIsHigher(DefaultAbilityAiScore score, byte proximity, DefaultAbilityAiPassBest? best)
    {
        if (best is null)
        {
            // The admitted <=16 bare-human score domain cannot equal the reset's stale-coordinate rank.
            return AiCandidateRank.Compare(score.Priority, score.HitPercent, -32767, 0) == AiRankOrder.Above;
        }
        var previous = best.Evaluation.Candidate.Outcome!.Score!;
        var order = AiCandidateRank.Compare(score.Priority, score.HitPercent, previous.Priority, previous.HitPercent);
        return order == AiRankOrder.Above || order == AiRankOrder.Equal && proximity < best.DestinationProximity;
    }
}
