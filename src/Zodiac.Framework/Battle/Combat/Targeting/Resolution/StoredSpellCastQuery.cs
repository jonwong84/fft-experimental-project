using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Resolve a stored ordinary aim from current metadata, then reuse the existing geometry/one-cost query.</summary>
public static class StoredSpellCastQuery
{
    public static MagicCastResolutionResult Resolve(StoredSpellCastRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Cast);
        ArgumentNullException.ThrowIfNull(request.Aim);
        MagicCastResolutionResult Reject() => new(false, FaithMagicResolutionRejection.UnsupportedContext, null, rng);
        if (request.Roster.IsDefaultOrEmpty || request.RosterProfile is not { CompleteRosterAndSourceOrderKnown: true }) return Reject();
        foreach (var u in request.Roster) if (u is null || u.Occupant is null) return Reject();
        SpellAimAnchor? anchor = null;
        if (request.Aim.TargetingType == 6)
        {
            var matches = request.Roster.Where(u => u.Occupant.UnitId == request.Aim.TargetUnitId).Take(2).ToArray();
            if (matches.Length != 1) return Reject();
            anchor = new(matches[0].Occupant.UnitId, matches[0].Occupant.Position);
        }
        var aim = SpellAimResolver.Resolve(request.Map, request.Aim, anchor, request.AimProfile);
        if (!aim.Accepted) return Reject();
        return OrdinarySpellTargeting.ResolveCast(new(request.Cast, request.Map, aim.Position!.Value, request.Roster,
            request.GeometryProfile, request.RosterProfile, request.Entry), rng);
    }
}
