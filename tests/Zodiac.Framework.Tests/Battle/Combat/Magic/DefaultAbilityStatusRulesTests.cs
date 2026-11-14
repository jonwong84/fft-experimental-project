using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class DefaultAbilityStatusRulesTests
{
    private static MagicCurrentUnit Current => MagicProjectionTestData.Snapshot().Units.Single(u => u.Occupant.UnitId.Value == "actor").Current!;
    private static MagicResultFrame Frame(short damage, short healing = 0, bool hit = true) =>
        new(hit, 100, default, damage, healing, new(HpDamage: true), default);

    [Fact]
    public void Crossing_critical_threshold_in_both_directions_updates_status_and_changed_outcome()
    {
        var hurt = DefaultAbilityStatusRules.Apply(Current, new(100, 100, 50, 70), 0, 3,
            AbilityOutcomeMarker.None, Frame(80));
        Assert.Equal((ushort)20, hurt.Vitals.Hp);
        Assert.True(hurt.Vitals.Critical);
        Assert.Equal(ActionTargetOutcome.Changed, hurt.Outcome);
        Assert.Equal(AbilityOutcomeMarker.Affected, hurt.Marker);
        var healed = DefaultAbilityStatusRules.Apply(hurt.Current, hurt.Vitals, 0, 3,
            hurt.Marker, Frame(0, 1));
        Assert.False(healed.Vitals.Critical);
        Assert.Equal((ushort)21, healed.Vitals.Hp);
    }

    [Fact]
    public void Knockout_clears_critical_and_charge_increments_byte_ko_and_keeps_targeted_marker()
    {
        var actor = Current with { Statuses = new(Charging: true) };
        var r = DefaultAbilityStatusRules.Apply(actor, new(20, 100, 50, 70, true), 255, 255,
            AbilityOutcomeMarker.Affected, Frame(30));
        Assert.True(r.Current.Statuses.Dead);
        Assert.False(r.Current.Statuses.Charging);
        Assert.False(r.Vitals.Critical);
        Assert.Equal((byte)0, r.KoCount);
        Assert.Equal((byte)3, r.DeathCounter);
        Assert.Equal(AbilityOutcomeMarker.Targeted, r.Marker);
        Assert.Equal(ActionTargetOutcome.Killed, r.Outcome);
        Assert.Equal((ushort)30, r.Current.Mp);
    }

    [Fact]
    public void Miss_does_not_change_vitals_charge_or_counters_but_assigns_targeted()
    {
        var actor = Current with { Statuses = new(Charging: true) };
        var v = new MagicUnitVitals(100, 100, 50, 70);
        var r = DefaultAbilityStatusRules.Apply(actor, v, 4, 9, AbilityOutcomeMarker.Affected, Frame(999, hit: false));
        Assert.Same(actor, r.Current);
        Assert.Same(v, r.Vitals);
        Assert.Equal((byte)4, r.KoCount);
        Assert.Equal((byte)9, r.DeathCounter);
        Assert.Equal(AbilityOutcomeMarker.Targeted, r.Marker);
        Assert.Equal(ActionTargetOutcome.Unchanged, r.Outcome);
    }

    [Fact]
    public void Numeric_fields_apply_independently_of_damage_type_and_negative_work_is_preserved()
    {
        var frame = Frame(-10) with { Types = default };
        var r = DefaultAbilityStatusRules.Apply(Current, new(50, 100, 50, 70), 0, 3, default, frame);
        Assert.Equal((ushort)60, r.Vitals.Hp);
        Assert.Equal(ActionTargetOutcome.Changed, r.Outcome);
    }

    [Fact]
    public void Unsupported_status_incoherent_critical_and_corpse_inputs_are_explicitly_rejected()
    {
        Assert.Throws<NotSupportedException>(() => DefaultAbilityStatusRules.Validate(Current with { Statuses = new(Sleep: true) }, new(100, 100, 50, 70)));
        Assert.Throws<NotSupportedException>(() => DefaultAbilityStatusRules.Validate(Current, new(20, 100, 50, 70)));
        Assert.Throws<NotSupportedException>(() => DefaultAbilityStatusRules.Validate(Current, new(100, 100, 50, 9)));
        Assert.Throws<NotSupportedException>(() => DefaultAbilityStatusRules.Apply(Current with { Statuses = new(Dead: true) }, new(0, 100, 50, 70), 0, 3, default, Frame(1)));
    }

    [Fact]
    public void Growth_refresh_critical_uses_new_maximum_and_replays_without_mutating_capture()
    {
        var before = new MagicUnitVitals(21, 100, 50, 70);
        var grown = before with { MaxHp = 110 };
        var refreshed = DefaultAbilityStatusRules.RefreshCritical(grown);
        Assert.True(refreshed.Critical);
        Assert.False(before.Critical);
        Assert.Equal(refreshed, DefaultAbilityStatusRules.RefreshCritical(grown));
    }
}
