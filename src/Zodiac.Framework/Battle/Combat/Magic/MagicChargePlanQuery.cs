namespace Zodiac.Framework;

/// <summary>Pure preparation/countdown proposals; no MP spend, source status bookkeeping, events or global clock.</summary>
public static class MagicChargePlanQuery
{
    public static MagicChargePlanResult Prepare(MagicChargePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Selection);
        FaithMagicInputs.ValidateCurrentUnit(request.Caster);
        MagicChargePlanResult Reject() => new(false, null);
        var s = request.Selection; var c = request.Caster;
        if (request.Profile is not { PreparationContextKnown: true, StableActorAndAnchorIdentityKnown: true } ||
            request.RosterProfile is not { CompleteRosterAndSourceOrderKnown: true } || request.Roster.IsDefaultOrEmpty ||
            c.UnitId != s.CasterId || c.IsMime || s.Definition.Persevere) return Reject();
        var byId = new Dictionary<BattleUnitId, SpellTargetOccupant>();
        foreach (var u in request.Roster)
            if (u is null || string.IsNullOrWhiteSpace(u.UnitId.Value) || !byId.TryAdd(u.UnitId, u)) return Reject();
        if (!byId.TryGetValue(s.CasterId, out var actor) || !actor.Active) return Reject();
        if (s.Aim.TargetingType == 6 && (s.Aim.TargetUnitId is not { } id || !byId.TryGetValue(id, out var anchor) || !anchor.Active)) return Reject();
        var setup = MagicCharge.CalculateSetup(s.Definition.RawChargeTime, c.Supports.ShortCharge, c.Supports.NonCharge, false);
        return new(true, new(s.Definition, s.CasterId, s.Aim, setup.Ticks));
    }

    public static MagicChargePlanStep Tick(PendingMagicCast plan, bool turnStatusBlocksWait)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var step = MagicCharge.Tick(plan.Counter, turnStatusBlocksWait);
        return new(step.Changed ? new(plan.Definition, plan.CasterId, plan.Aim, step.Counter) : plan, step.Ready, step.Changed);
    }
}
