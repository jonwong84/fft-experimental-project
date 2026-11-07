namespace Zodiac.Framework.CombatRules;

/// <summary>
/// Shared ordinary primary-action preparation for execution and AI. Proposes immutable state;
/// no MP spend, RNG, geometry recheck, live adoption or general command-menu dispatch.
/// </summary>
public static class OrdinaryMagicPreparation
{
    public static OrdinaryMagicPreparationResult Prepare(MagicChargePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Selection);
        FaithMagicInputs.ValidateCurrentUnit(request.Caster);
        OrdinaryMagicPreparationResult Reject() => new(false, null);
        var selection = request.Selection;
        var caster = request.Caster;
        if (request.Profile is not { PreparationContextKnown: true, StableActorAndAnchorIdentityKnown: true } ||
            request.RosterProfile is not { CompleteRosterAndSourceOrderKnown: true } || request.Roster.IsDefaultOrEmpty ||
            caster.UnitId != selection.CasterId || caster.IsMime || selection.Definition.Persevere)
            return Reject();
        var byId = new Dictionary<BattleUnitId, SpellTargetOccupant>();
        foreach (var unit in request.Roster)
            if (unit is null || string.IsNullOrWhiteSpace(unit.UnitId.Value) || !byId.TryAdd(unit.UnitId, unit))
                return Reject();
        if (!byId.TryGetValue(selection.CasterId, out var actor) || !actor.Active)
            return Reject();
        // Only followed-unit commands validate an anchor here. Blue-area validation happened at selection.
        if (selection.Aim.TargetingType == 6 && (selection.Aim.TargetUnitId is not { } id ||
            !byId.TryGetValue(id, out var anchor) || !anchor.Active))
            return Reject();
        var setup = MagicCharge.CalculateSetup(selection.Definition.RawChargeTime,
            caster.Supports.ShortCharge, caster.Supports.NonCharge, false);
        var plan = new PendingMagicCast(selection.Definition, selection.CasterId, selection.Aim, setup.Ticks);
        // action_prepare starts Charging for delayed ordinary actions; action_commit_preparation
        // clears action states for immediate primary actions. Jump/Performing are outside this profile.
        var statuses = caster.Statuses with { Charging = !setup.Immediate, Performing = false, Defending = false };
        return new(true, new(plan, caster with { Statuses = statuses }));
    }
}
