namespace Zodiac.Framework;

/// <summary>Living HP-only projection with stable statuses; never complete source action application.</summary>
public static class MagicLivingApplication
{
    public static MagicLivingApplicationResult Project(MagicCurrentUnit current, MagicUnitVitals vitals,
        MagicResultFrame frame, MagicLivingApplicationProfile? profile)
    {
        FaithMagicInputs.ValidateCurrentUnit(current); ArgumentNullException.ThrowIfNull(vitals); ArgumentNullException.ThrowIfNull(frame);
        MagicLivingApplicationResult Reject(MagicLivingApplicationRejection reason) => new(false, reason, null);
        if (profile is not { ActiveUnitKnown: true, TargetReactionsKnownAbsent: true }) return Reject(MagicLivingApplicationRejection.UnsupportedContext);
        if (vitals.Hp == 0 || vitals.Hp > vitals.MaxHp || current.Mp > vitals.MaxMp || current.Statuses.Dead || current.Statuses.Petrify || current.Statuses.Wall)
            return Reject(MagicLivingApplicationRejection.UnsupportedState);
        // Targeted reactions already required above; downstream work is not entered on a miss.
        if (!frame.Hit) return new(true, MagicLivingApplicationRejection.None, new(current, vitals, false));
        if (profile is not { GolemAndDamageSharingKnownAbsent: true, OtherResultWorkKnownAbsent: true, OrdinaryStatusRulesKnown: true })
            return Reject(MagicLivingApplicationRejection.UnsupportedContext);
        if (frame.HpDamage > 999 || frame.HpHealing > 999) return Reject(MagicLivingApplicationRejection.UnsupportedFrame);
        if (vitals.Critical != (vitals.Hp <= vitals.MaxHp / 5) || current.Statuses.Chicken != (vitals.Brave < 10))
            return Reject(MagicLivingApplicationRejection.UnsupportedState);
        if (frame.Types.HpDamage)
        {
            if (!profile.CharmKnownAbsent) return Reject(MagicLivingApplicationRejection.UnsupportedContext);
            if (current.Statuses.Sleep || current.Statuses.Confusion || current.Statuses.Transparent)
                return Reject(MagicLivingApplicationRejection.WouldChangeStatus);
        }
        var change = MagicVitalArithmetic.Apply(new(vitals.Hp, vitals.MaxHp, current.Mp, vitals.MaxMp, true, frame.HpDamage, frame.HpHealing));
        if (change.Hp == 0) return Reject(MagicLivingApplicationRejection.WouldKnockOut);
        if ((change.Hp <= vitals.MaxHp / 5) != vitals.Critical) return Reject(MagicLivingApplicationRejection.WouldChangeStatus);
        return new(true, MagicLivingApplicationRejection.None,
            new(current with { Mp = change.Mp }, vitals with { Hp = change.Hp }, change.Changed));
    }
}
