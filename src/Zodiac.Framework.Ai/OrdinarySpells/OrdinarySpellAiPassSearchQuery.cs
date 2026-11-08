using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One fresh pass across cast origins, sharing remaining aims and RNG. No cross-pass dispatch or commands.</summary>
/// <remarks>ZOD-440; evaluation.c at 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class OrdinarySpellAiPassSearchQuery
{
    public static OrdinarySpellAiPassSearchResult Evaluate(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiPassSearchRequest request, OrdinarySpellAiMovementProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        OrdinarySpellAiPassSearchResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            if (!Enum.IsDefined(request.Pass)) return Reject("An explicit supported caller pass is required.");
            OrdinarySpellAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new OrdinarySpellAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            var origins = OrdinarySpellAiOriginQuery.Build(work, request.Scenario, profile, rng);
            if (!origins.Accepted) return Reject(origins.Rejection!);
            var pass = origins.Outcome!.Passes.SingleOrDefault(p => p.Potential.Kind == request.Pass);
            if (pass is null) return Reject("That caller pass is unavailable in the fresh source grouping.");
            return OrdinarySpellAiPassEvaluation.OriginReachableEvaluate(work, origins.Outcome, pass,
                pass.Origins, origins.RngState, consumeAims: true);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell pass search: " + e.Message);
        }
    }
}
