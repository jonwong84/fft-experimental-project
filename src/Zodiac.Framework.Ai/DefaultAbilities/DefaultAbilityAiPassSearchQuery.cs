using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One fresh pass across cast origins, sharing remaining aims and RNG. No cross-pass dispatch or commands.</summary>
/// <remarks>ZOD-440; evaluation.c at 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class DefaultAbilityAiPassSearchQuery
{
    public static DefaultAbilityAiPassSearchResult Evaluate(OrdinaryMagicBattleState state, DefaultAbilityAiCatalog catalog,
        DefaultAbilityAiPassSearchRequest request, DefaultAbilityAiMovementProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        DefaultAbilityAiPassSearchResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            if (!Enum.IsDefined(request.Pass)) return Reject("An explicit supported caller pass is required.");
            DefaultAbilityAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new DefaultAbilityAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            var origins = DefaultAbilityAiOriginQuery.Build(work, request.Scenario, profile, rng);
            if (!origins.Accepted) return Reject(origins.Rejection!);
            var pass = origins.Outcome!.Passes.SingleOrDefault(p => p.Potential.Kind == request.Pass);
            if (pass is null) return Reject("That caller pass is unavailable in the fresh source grouping.");
            return DefaultAbilityAiPassEvaluation.OriginReachableEvaluate(work, origins.Outcome, pass,
                pass.Origins, origins.RngState, consumeAims: true);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell pass search: " + e.Message);
        }
    }
}
