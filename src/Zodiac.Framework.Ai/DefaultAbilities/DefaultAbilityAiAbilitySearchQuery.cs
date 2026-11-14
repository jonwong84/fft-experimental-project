using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh one-slot/scenario caller composition. No ability-list dispatch, ranked insertion, lottery or commands.</summary>
/// <remarks>ZOD-441/443; evaluation.c at 11464943bbdbd2a0af022d90cb7f10455cf911df. Context owns raw inputs only.</remarks>
public static class DefaultAbilityAiAbilitySearchQuery
{
    public static DefaultAbilityAiAbilitySearchResult Evaluate(
        DefaultAbilityAiContext context,
        DefaultAbilityAiAbilitySearchRequest request,
        RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Profile, nameof(request.Profile));

        var state = context.State;
        DefaultAbilityAiAbilitySearchResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            DefaultAbilityAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new DefaultAbilityAiWork(state, context.Catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            var origins = DefaultAbilityAiOriginQuery.Build(work, request.Scenario, request.Profile, rng);

            if (!origins.Accepted)
            {
                return Reject(origins.Rejection!);
            }

            return DefaultAbilityAiAbilityEvaluation.AiAbilityOutcomeEvaluate(work, origins.Outcome!, origins.RngState);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell ability search: " + e.Message);
        }
    }
}
