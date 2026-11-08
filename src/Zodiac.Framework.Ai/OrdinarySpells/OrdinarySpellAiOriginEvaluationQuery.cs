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
            var evaluated = OrdinarySpellAiPassEvaluation.OriginReachableEvaluate(work, origins.Outcome, pass,
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
