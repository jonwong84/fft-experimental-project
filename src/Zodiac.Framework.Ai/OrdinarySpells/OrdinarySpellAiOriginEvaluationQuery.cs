using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Evaluate one fresh potential origin/pass. No cross-origin consumption, dispatcher, lottery or commands.</summary>
/// <remarks>ZOD-439; evaluation.c at 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class OrdinarySpellAiOriginEvaluationQuery
{
    public static OrdinarySpellAiOriginEvaluationResult Evaluate(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiOriginEvaluationRequest request, OrdinarySpellAiMovementProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        OrdinarySpellAiOriginEvaluationResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            if (!Enum.IsDefined(request.Pass)) return Reject("An explicit supported aim pass is required.");
            OrdinarySpellAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new OrdinarySpellAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            var origins = OrdinarySpellAiOriginQuery.Build(work, request.Scenario, profile, rng);
            if (!origins.Accepted) return Reject(origins.Rejection!);
            var pass = origins.Outcome!.Passes.SingleOrDefault(p => p.Potential.Kind == request.Pass);
            var origin = pass?.Origins.SingleOrDefault(o => o.CastOrigin == request.CastOrigin);
            if (pass is null || origin is null) return Reject("That cast origin is unavailable in the fresh selected pass.");
            var selectedWork = work.ForOrigin(pass.Potential, origins.Outcome.Potential.Ability.Usage, origin);
            return OriginAimTilesEvaluate(selectedWork, origins.Outcome, pass, origin, origins.RngState);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell origin evaluation: " + e.Message);
        }
    }

    private static OrdinarySpellAiOriginEvaluationResult OriginAimTilesEvaluate(OrdinarySpellAiWork work,
        OrdinarySpellAiOriginOutcome origins, OrdinarySpellAiOriginPass pass, OrdinarySpellAiOrigin origin, RandomState rng)
    {
        var evaluations = ImmutableArray.CreateBuilder<OrdinarySpellAiAimEvaluation>();
        OrdinarySpellAiAimEvaluation? best = null;
        short bestPriority = -32767;
        byte bestHit = 0;
        bool stopped = false;
        byte proximity = origins.Movement.Cells.Single(c => c.Tile == origin.MovementDestination).Proximity;
        foreach (var aim in origin.Aims)
        {
            var candidate = OrdinarySpellAiActionSimulation.AiActionSimulateAndScore(work,
                new(work.Actor.Id, work.Ability.Entry.Slot.Index, aim.Position, aim.UnitId is not null), rng);
            if (!candidate.Accepted) throw new NotSupportedException(candidate.Rejection);
            var score = candidate.Outcome!.Score;
            // Every aim here has this same final destination/proximity. Equal rank keeps the first aim.
            bool improves = score is not null &&
                AiCandidateRank.Compare(score.Priority, score.HitPercent, bestPriority, bestHit) == AiRankOrder.Above;
            var evaluated = new OrdinarySpellAiAimEvaluation(aim, rng, candidate, improves);
            evaluations.Add(evaluated);
            rng = candidate.RngState;
            if (improves)
            {
                best = evaluated;
                bestPriority = score!.Priority;
                bestHit = score.HitPercent;
            }
            // Source compares first, then tests processing_done before consuming the aim or considering another.
            if (candidate.Disposition == OrdinarySpellAiCandidateDisposition.ScoredUnresolved)
            {
                stopped = true;
                break;
            }
        }
        var baseline = origins.Movement.BaselineRank;
        bool passesBaseline = best is not null &&
            AiCandidateRank.Compare(bestPriority, bestHit, baseline.Priority, baseline.HitPercent) == AiRankOrder.Above;
        return new(true, null, new(origins, pass, origin, proximity, evaluations.ToImmutable(), best, stopped, passesBaseline), rng);
    }
}
