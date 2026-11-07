namespace Zodiac.Framework;

/// <summary>Reduce certified cast data into private vital fields; keeps the input RNG and grants no adoption authority.</summary>
public static class MagicCastProjection
{
    public static MagicCastProjectionResult Project(MagicProjectionSnapshot before, BattleUnitId casterId, MagicCastOutcome cast,
        MagicCastProjectionProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(before); ArgumentNullException.ThrowIfNull(cast);
        MagicCastProjectionResult Reject(MagicProjectionRejection reason, MagicLivingApplicationRejection? app = null) => new(false, reason, null, app);
        if (profile is not { EligibleSourceOrderKnown: true, NoActorVitalWorkKnown: true }) return Reject(MagicProjectionRejection.UnsupportedContext);
        var indices = before.Units.Select((u, i) => (u.Occupant.UnitId, i)).ToDictionary(p => p.UnitId, p => p.i);
        if (!indices.TryGetValue(casterId, out int casterIndex) || before.Units[casterIndex] is not { Current: { } caster, Vitals: not null, Occupant.Active: true })
            return Reject(MagicProjectionRejection.UnknownUnit);
        if (cast.Targets.IsDefaultOrEmpty || cast.RecordedCost < 0 || cast.CandidateMp > caster.Mp || caster.Mp - cast.CandidateMp != cast.RecordedCost)
            return Reject(MagicProjectionRejection.InvalidCast);
        var ids = new HashSet<BattleUnitId>();
        foreach (var t in cast.Targets)
        {
            if (t is null || t.Result is null || !ids.Add(t.TargetId)) return Reject(MagicProjectionRejection.InvalidCast);
            if (!indices.TryGetValue(t.TargetId, out int i) || before.Units[i] is not { Current: not null, Vitals: not null, Occupant.Active: true })
                return Reject(MagicProjectionRejection.UnknownUnit);
        }
        var units = before.Units.ToBuilder();
        units[casterIndex] = units[casterIndex] with { Current = caster with { Mp = cast.CandidateMp } };
        foreach (var t in cast.Targets)
        {
            int i = indices[t.TargetId]; var unit = units[i];
            var applied = MagicLivingApplication.Project(unit.Current!, unit.Vitals!, t.Result,
                profile.Application is { } application ? application with { ActiveUnitKnown = true } : null);
            if (!applied.Accepted) return Reject(MagicProjectionRejection.LivingApplicationRejected, applied.Rejection);
            units[i] = unit with { Current = applied.Outcome!.Current, Vitals = applied.Outcome.Vitals };
        }
        return new(true, MagicProjectionRejection.None, new(before.Map, units.ToImmutable(), before.RngState));
    }
}
