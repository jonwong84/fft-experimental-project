using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Evaluate one fresh potential origin/pass. No cross-origin consumption, dispatcher, lottery or commands.</summary>
/// <remarks>ZOD-439; evaluation.c at 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class DefaultAbilityAiOriginEvaluationQuery
{
    public static DefaultAbilityAiOriginEvaluationResult Evaluate(OrdinaryMagicBattleState state, DefaultAbilityAiCatalog catalog,
        DefaultAbilityAiOriginEvaluationRequest request, DefaultAbilityAiMovementProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        DefaultAbilityAiOriginEvaluationResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            if (!Enum.IsDefined(request.Pass)) return Reject("An explicit supported aim pass is required.");
            DefaultAbilityAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new DefaultAbilityAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            var origins = DefaultAbilityAiOriginQuery.Build(work, request.Scenario, profile, rng);
            if (!origins.Accepted) return Reject(origins.Rejection!);
            var pass = origins.Outcome!.Passes.SingleOrDefault(p => p.Potential.Kind == request.Pass);
            var origin = pass?.Origins.SingleOrDefault(o => o.CastOrigin == request.CastOrigin);
            if (pass is null || origin is null) return Reject("That cast origin is unavailable in the fresh selected pass.");
            var evaluated = DefaultAbilityAiPassEvaluation.OriginReachableEvaluate(work, origins.Outcome, pass,
                [origin], origins.RngState, consumeAims: false);
            var scan = evaluated.Outcome!;
            var trace = scan.Traces.Single();
            byte proximity = origins.Outcome.Movement.Cells.Single(c => c.Tile == origin.MovementDestination).Proximity;
            return new(true, null, new(origins.Outcome, pass, origin, proximity, trace.Evaluations,
                scan.Best?.Evaluation, trace.OriginStoppedUnresolved, scan.PassesBaseline), evaluated.RngState);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell origin evaluation: " + e.Message);
        }
    }

}
