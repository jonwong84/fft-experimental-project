using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Calculate one admitted ready plan using current facts; no publication, scheduling or replay authority.</summary>
public static class MagicReadyPlanQuery
{
    public static MagicReadyPlanResult Resolve(MagicReadyPlanRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Plan);
        MagicReadyPlanResult Reject(MagicMpBlock? block = null) => new(block,
            new(false, FaithMagicResolutionRejection.UnsupportedContext, null, rng));
        if (request.Plan.Counter != 0 || request.Profile is not { CurrentIdentityMappingKnown: true,
                CurrentTurnBlockKnown: true, TurnStatusBlocksWait: false } || request.ReadyProfile is null || request.Entry is null ||
            request.RosterProfile is not { CompleteRosterAndSourceOrderKnown: true } || request.Roster.IsDefaultOrEmpty) return Reject();
        foreach (var u in request.Roster) if (u is null || u.Occupant is null) return Reject();
        var actors = request.Roster.Where(u => u.Occupant.UnitId == request.Plan.CasterId).Take(2).ToArray();
        if (actors.Length != 1 || !actors[0].Occupant.Active || actors[0].Current is not { } caster || caster.UnitId != request.Plan.CasterId) return Reject();
        // Readiness is derived above; the caller still certifies all preceding action bookkeeping/reaction boundaries.
        var prepared = MagicCastQuery.PrepareReady(new(caster, request.Plan.Definition, request.Weather, request.Mode,
            request.ResultProfile, request.ReadyProfile with { ChargeReadyKnown = true }));
        if (!prepared.Accepted) return Reject();
        if (prepared.Cast is null) return Reject(prepared.Resource?.Block);
        var result = StoredSpellCastQuery.Resolve(new(prepared.Cast, request.Map, request.Plan.Aim, request.Roster,
            request.GeometryProfile, request.RosterProfile, request.AimProfile,
            request.Entry with { MapAimPreviouslyValidatedKnown = true }), rng);
        return new(null, result);
    }
}
