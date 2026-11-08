using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh one-slot, current-origin aim evaluation and strict best rank. No movement search or command authority.</summary>
public static class OrdinarySpellAiSearchQuery
{
    public static OrdinarySpellAiSearchResult Evaluate(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiSearchRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        OrdinarySpellAiSearchResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            OrdinarySpellAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new OrdinarySpellAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            var aims = OrdinarySpellAiAimEnumeration.Build(work);
            var passes = ImmutableArray.CreateBuilder<OrdinarySpellAiSearchPass>();
            OrdinarySpellAiAimEvaluation? best = null;
            short bestPriority = -32767; byte bestHit = 0; // ai_action_rank_reset, fresh marker0.
            var decisionRng = rng;
            foreach (var pass in aims.Passes)
            {
                var passWork = work.ForAimPass(pass, aims.Ability.Usage);
                var evaluations = ImmutableArray.CreateBuilder<OrdinarySpellAiAimEvaluation>();
                bool stopped = false;
                foreach (var aim in pass.Aims)
                {
                    var candidate = OrdinarySpellAiActionSimulation.AiActionSimulateAndScore(passWork,
                        new(request.ActorId, request.PrimarySlot, aim.Position, aim.UnitId is not null), decisionRng);
                    if (!candidate.Accepted) return Reject(candidate.Rejection!);
                    var score = candidate.Outcome!.Score;
                    // Origin/destination is constant here, so proximity cannot break an equal rank.
                    bool improves = score is not null &&
                        AiCandidateRank.Compare(score.Priority, score.HitPercent, bestPriority, bestHit) == AiRankOrder.Above;
                    var evaluated = new OrdinarySpellAiAimEvaluation(aim, decisionRng, candidate, improves);
                    evaluations.Add(evaluated);
                    decisionRng = candidate.RngState; // Also retain real draws from NotValuable candidates.
                    if (improves)
                    {
                        best = evaluated; bestPriority = score!.Priority; bestHit = score.HitPercent;
                    }
                    // action_hits_simulate sets processing_done even on a miss/nonvaluable result.
                    // Only an unresolved charge leaves it clear in this admitted simulation domain.
                    if (candidate.Disposition == OrdinarySpellAiCandidateDisposition.ScoredUnresolved)
                    {
                        stopped = true;
                        break; // End this origin's pass, after rank comparison; later caller passes still run.
                    }
                }
                passes.Add(new(pass, evaluations.ToImmutable(), stopped));
            }
            return new(true, null, new(aims, passes.ToImmutable(), best), decisionRng);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell aim search: " + e.Message);
        }
    }
}
