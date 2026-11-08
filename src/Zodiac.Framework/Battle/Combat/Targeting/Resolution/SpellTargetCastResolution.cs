using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Stored-area resolution through recipient collection and one-cost formula work; no live publication.</summary>
internal static class SpellTargetCastResolution
{
    /// <summary>Derive ordinary resolution targets and delegate one-cost calculation, with whole-query rollback.</summary>
    internal static MagicCastResolutionResult Resolve(SpellGeometryCastRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Cast);
        MagicCastResolutionResult Reject() => new(false, FaithMagicResolutionRejection.UnsupportedContext, null, rng);
        var cast = request.Cast;
        if (request.Entry is not { MapAimPreviouslyValidatedKnown: true, NoEarlierTargetEffectsKnown: true } ||
            cast.Definition.Targeting is not { } geometry || cast.Definition.OriginalAbilityId == 329 || request.Roster.IsDefaultOrEmpty)
            return Reject();
        foreach (var u in request.Roster)
            if (u is null || u.Occupant is null || u.Current is { } c && c.UnitId != u.Occupant.UnitId) return Reject();
        var actor = request.Roster.FirstOrDefault(u => u.Occupant.UnitId == cast.Caster.UnitId);
        if (actor is null || actor.Current != cast.Caster && actor.Current != cast.Caster with { Mp = cast.Resource.CandidateMp }) return Reject();
        var area = DefaultAbilityTargeting.Area(request.Map, actor.Occupant.Position, request.SelectedMapAim, geometry, request.GeometryProfile);
        if (!area.Accepted) return Reject();
        // Ordinary action resolution resets source nearest-first. Directional handlers are outside this slice.
        var selection = DefaultAbilityTargeting.SelectTargets(area.Outcome!, cast.Caster.UnitId,
            request.Roster.Select(u => u.Occupant).ToImmutableArray(), request.RosterProfile);
        if (!selection.Accepted) return Reject();
        var byId = request.Roster.ToDictionary(u => u.Occupant.UnitId);
        var targets = ImmutableArray.CreateBuilder<MagicCurrentUnit>();
        foreach (var id in selection.Outcome!.UnitIds)
        {
            if (byId[id].Current is not { } current) return Reject();
            targets.Add(current);
        }
        return MagicCastQuery.ResolveTargets(cast, targets.ToImmutable(), new(true, true), rng);
    }

}
