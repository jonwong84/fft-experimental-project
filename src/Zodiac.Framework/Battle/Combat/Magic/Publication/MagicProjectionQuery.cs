using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Recompute one Execute-mode ready plan and its private living vital/RNG projection, never publish game state.</summary>
public static class MagicProjectionQuery
{
    public static MagicProjectionResult Calculate(MagicProjectionSnapshot before, MagicReadyPlanRequest request,
        MagicCastProjectionProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(before); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Plan);
        MagicProjectionResult Reject(MagicProjectionRejection reason, MagicLivingApplicationRejection? app = null,
            MagicMpBlock? block = null, FaithMagicResolutionRejection? calculation = null) =>
            new(false, reason, null, before.RngState, app, block, calculation);
        if (request.Mode != MagicFormulaEvaluationMode.Execute) return Reject(MagicProjectionRejection.UnsupportedMode);
        if (profile is not { NoActorVitalWorkKnown: true }) return Reject(MagicProjectionRejection.UnsupportedContext);
        var roster = before.Units.Select(u => new SpellTargetCombatant(u.Occupant, u.Current)).ToImmutableArray();
        if (!ReferenceEquals(request.Map, before.Map) || request.Roster.IsDefault || !request.Roster.SequenceEqual(roster))
            return Reject(MagicProjectionRejection.SnapshotMismatch);
        var ready = MagicReadyPlanQuery.Resolve(request, before.RngState);
        if (!ready.Accepted) return Reject(MagicProjectionRejection.CalculationRejected, block: ready.ResourceBlock,
            calculation: ready.Resolution.Rejection);
        var cast = ready.Resolution.Outcome!;
        var projected = MagicCastProjection.Project(before, request.Plan.CasterId, cast, profile with { EligibleSourceOrderKnown = true });
        if (!projected.Accepted) return Reject(projected.Rejection, projected.ApplicationRejection);
        var candidate = new MagicProjectionSnapshot(before.Map, projected.Candidate!.Units, ready.Resolution.RngState);
        return new(true, MagicProjectionRejection.None, new(before, candidate, cast), candidate.RngState);
    }
}
