using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Admitted ordinary part of ai_ability_outcome_evaluate; reference 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
internal static class OrdinarySpellAiAbilityEvaluation
{
    internal static OrdinarySpellAiAbilitySearchResult AiAbilityOutcomeEvaluate(OrdinarySpellAiWork work,
        OrdinarySpellAiOriginOutcome origins, RandomState rng)
    {
        var passes = ImmutableArray.CreateBuilder<OrdinarySpellAiAbilityPass>();
        OrdinarySpellAiAbilityBest? best = null;
        bool passesBaseline = false;
        // BuildPotentialPasses owns source grouping/restoration: moving then still unit targets, then tiles;
        // ConsumeAim uses one unit group, no FollowTarget uses tiles only, and zero-area FollowTarget returns early.
        foreach (var pass in origins.Passes)
        {
            var entryRng = rng;
            var entryBest = best;
            var result = OrdinarySpellAiPassEvaluation.OriginReachableEvaluate(work, origins, pass,
                pass.Origins, rng, consumeAims: true, bestAtEntry: best?.Candidate);
            var scan = result.Outcome!;
            rng = result.RngState;
            if (!ReferenceEquals(scan.Best, entryBest?.Candidate))
            {
                // A later pass owns best only when ai_rank_is_higher actually replaces the prior action.
                if (scan.Best is not null) best = new(pass.Potential.Kind, scan.Best);
            }
            passes.Add(new(pass, entryRng, rng, scan.Traces, scan.RemainingAims, entryBest, best));
            passesBaseline = scan.PassesBaseline;
        }
        // ai_best_rank_insert follows all admitted ordinary passes, or the zero-area unit return.
        // Report its rank-only movement gate; ranked-list admission, insertion/reset and lottery remain separate.
        return new(true, null, new(origins, passes.ToImmutable(), best, passesBaseline), rng);
    }
}
