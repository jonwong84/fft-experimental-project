namespace Zodiac.Framework;

// This kernel is used only by the admitted ordinary battle: no innate/immune/timed
// statuses, riders, reactions, Golem, Chicken, or other result work. It is not a
// substitute for the general status resolver.
internal static class OrdinaryMagicStatusRules
{
    internal static void Validate(MagicCurrentUnit current, MagicUnitVitals vitals)
    {
        FaithMagicInputs.ValidateCurrentUnit(current);
        ArgumentNullException.ThrowIfNull(vitals);
        var s = current.Statuses;
        if (s != new MagicCurrentStatuses(Dead: s.Dead, Charging: s.Charging) ||
            vitals.Brave < 10 || vitals.Brave > 100 || vitals.MaxHp == 0 ||
            vitals.Hp > vitals.MaxHp || current.Mp > vitals.MaxMp ||
            s.Dead != (vitals.Hp == 0) || s.Dead && s.Charging ||
            vitals.Critical != (!s.Dead && vitals.Hp <= vitals.MaxHp / 5))
            throw new NotSupportedException("Ordinary magic requires coherent Dead/Critical/Charging state and no other statuses.");
    }

    internal static OrdinaryMagicApplication Apply(MagicCurrentUnit current, MagicUnitVitals vitals,
        byte koCount, byte deathCounter, AbilityOutcomeMarker previous, MagicResultFrame frame)
    {
        Validate(current, vitals);
        ArgumentNullException.ThrowIfNull(frame);
        if (current.Statuses.Dead)
            throw new NotSupportedException("Corpse application is outside ordinary magic execution.");
        if (frame.HpDamage > 999 || frame.HpHealing > 999 || frame.ClearFirstCanceledStatusByte)
            throw new NotSupportedException("Only finalized ordinary empty-status HP work is admitted.");
        var marker = AbilityOutcomeMarker.Targeted;
        if (!frame.Hit)
            return new(current, vitals, koCount, deathCounter, marker, ActionTargetOutcome.Unchanged);

        var change = MagicVitalArithmetic.Apply(new(vitals.Hp, vitals.MaxHp, current.Mp, vitals.MaxMp,
            true, frame.HpDamage, frame.HpHealing));
        if (change.Hp == 0)
        {
            // The source HP-zero branch returns before the normal AFFECTED store.
            return new(current with { Statuses = new(Dead: true) },
                vitals with { Hp = 0, Critical = false }, unchecked((byte)(koCount + 1)), 3,
                marker, ActionTargetOutcome.Killed);
        }

        bool critical = change.Hp <= vitals.MaxHp / 5;
        bool changed = change.Changed || critical != vitals.Critical;
        marker = ActionOutcomeTracking.ApplyOrdinary(previous, true, true, changed);
        return new(current, vitals with { Hp = change.Hp, Critical = critical }, koCount, deathCounter,
            marker, changed ? ActionTargetOutcome.Changed : ActionTargetOutcome.Unchanged);
    }

    internal static MagicUnitVitals RefreshCritical(MagicUnitVitals vitals) =>
        vitals with { Critical = vitals.Hp != 0 && vitals.Hp <= vitals.MaxHp / 5 };
}

internal sealed record OrdinaryMagicApplication(MagicCurrentUnit Current, MagicUnitVitals Vitals,
    byte KoCount, byte DeathCounter, AbilityOutcomeMarker Marker, ActionTargetOutcome Outcome);
