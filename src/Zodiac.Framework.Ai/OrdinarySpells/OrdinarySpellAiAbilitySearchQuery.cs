using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh one-slot/scenario caller composition. No ability-list dispatch, ranked insertion, lottery or commands.</summary>
/// <remarks>ZOD-441; evaluation.c at 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class OrdinarySpellAiAbilitySearchQuery
{
    public static OrdinarySpellAiAbilitySearchResult Evaluate(
        OrdinaryMagicBattleState state,
        OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiAbilitySearchRequest request,
        OrdinarySpellAiMovementProfile profile,
        RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);

        OrdinarySpellAiAbilitySearchResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            OrdinarySpellAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new OrdinarySpellAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            var origins = OrdinarySpellAiOriginQuery.Build(work, request.Scenario, profile, rng);

            if (!origins.Accepted)
            {
                return Reject(origins.Rejection!);
            }

            return OrdinarySpellAiAbilityEvaluation.AiAbilityOutcomeEvaluate(work, origins.Outcome!, origins.RngState);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell ability search: " + e.Message);
        }
    }
}
