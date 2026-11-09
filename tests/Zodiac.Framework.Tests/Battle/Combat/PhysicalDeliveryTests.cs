using Xunit;
using Zodiac.Framework.Formulas;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

public sealed class PhysicalDeliveryTests
{
    private static readonly AssetId Custom = new("test:physical/custom");
    private static PhysicalAbilityBattleProfile Profile(AttackBattleSnapshot state, PhysicalAbilityDelivery delivery,
        bool grant = true) => new(Ground, delivery,
            [new(Custom, delivery, new(AbilityFormulaId.RandomPaPlusHalfPa, new(9, 0)))],
            state.Combatants.Select(u => new TargetedAbilityUnitGrant(u.UnitId, u.UnitId == A && grant ? [Custom] : [], 0)));

    [Theory]
    [InlineData(PhysicalAbilityDelivery.AdjacentUnit)]
    [InlineData(PhysicalAbilityDelivery.DirectProjectile)]
    public void Arbitrary_identity_uses_selected_delivery_and_retains_legacy_events_and_interception(PhysicalAbilityDelivery delivery)
    {
        var setup = Make([new("a", new(1, 1, 0)), new("b", new(2, 1, 0), 1), new("c", new(4, 1, 0), 1)], seed: 1);
        var before = Snapshot(setup.State);
        var profile = Profile(setup.State, delivery);
        var heights = RosterThrowStoneTests.Heights(setup.State);
        var command = new UseTargetedAbilityCommand(A, Custom, delivery == PhysicalAbilityDelivery.AdjacentUnit ? B : C);
        var result = PhysicalAbilityBattleSimulator.ApplyRoster(setup.State, command, profile, setup.Profile, heights);
        Assert.True(result.Accepted);
        AttackBattleResult legacy;
        if (delivery == PhysicalAbilityDelivery.AdjacentUnit)
        {
            var view = DashAbilityBattleProfile.FromPhysicalProfile(profile);
            Assert.Same(profile, view.PhysicalProfile);
            Assert.Same(profile.Abilities[Custom], view.Abilities[Custom].Definition);
            legacy = DashAbilityBattleSimulator.ApplyRoster(setup.State, command, view, setup.Profile);
            Assert.Equal(14, Assert.Single(result.Events.OfType<DashResolvedEvent>()).Damage);
        }
        else
        {
            var view = TargetedAbilityBattleProfile.FromPhysicalProfile(profile);
            Assert.Same(profile, view.PhysicalProfile);
            Assert.Same(profile.Abilities[Custom], view.Abilities[Custom].Definition);
            legacy = TargetedAbilityBattleSimulator.ApplyRoster(setup.State, command, view, setup.Profile, heights);
            var interception = Assert.Single(result.Events.OfType<ThrowStoneTargetInterceptedEvent>());
            Assert.Equal(B, Assert.Single(result.Events.OfType<ThrowStoneResolvedEvent>()).TargetId);
            Assert.Equal(C, interception.IntendedTargetId);
            Assert.Equal(B, interception.ActualTargetId);
        }
        Assert.Equal(Snapshot(legacy.State), Snapshot(result.State));
        Assert.Equal(SerializeEvents(legacy.Events), SerializeEvents(result.Events));
        Assert.Equal(before, Snapshot(setup.State));
    }

    [Theory]
    [InlineData(PhysicalAbilityDelivery.AdjacentUnit)]
    [InlineData(PhysicalAbilityDelivery.DirectProjectile)]
    public void Generic_delivery_requires_the_actual_grant_before_any_draw_or_ACT(PhysicalAbilityDelivery delivery)
    {
        var setup = Make();
        var result = PhysicalAbilityBattleSimulator.ApplyRoster(setup.State, new(A, Custom, B),
            Profile(setup.State, delivery, false), setup.Profile, RosterThrowStoneTests.Heights(setup.State));
        Assert.False(result.Accepted);
        Assert.Equal(AttackCommandRejection.AbilityNotGranted, result.AttackRejection);
        Assert.Same(setup.State, result.State);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Binding_preserves_definition_identity_and_rejects_mismatched_delivery_and_unresolved_grants()
    {
        var formula = new AbilityFormula(AbilityFormulaId.RandomPa, new(0, 4));
        var definition = new PhysicalAbilityDefinition(Custom, PhysicalAbilityDelivery.AdjacentUnit, formula);
        var abilities = new List<PhysicalAbilityDefinition> { definition };
        var grants = new List<TargetedAbilityUnitGrant> { new(A, [Custom], 0) };
        var profile = new PhysicalAbilityBattleProfile(Ground, PhysicalAbilityDelivery.AdjacentUnit, abilities, grants);
        abilities.Clear(); grants.Clear();
        Assert.Same(definition, profile.Abilities[Custom]);
        Assert.Same(formula, profile.Abilities[Custom].Formula);
        Assert.Contains(Custom, profile.Units[A].Abilities);
        Assert.Throws<NotSupportedException>(() => TargetedAbilityBattleProfile.FromPhysicalProfile(profile));
        Assert.Throws<NotSupportedException>(() => new PhysicalAbilityBattleProfile(Ground,
            PhysicalAbilityDelivery.DirectProjectile, [definition], []));
        Assert.Throws<NotSupportedException>(() => new PhysicalAbilityBattleProfile(Ground,
            (PhysicalAbilityDelivery)99, [], []));
        Assert.Throws<ArgumentException>(() => new PhysicalAbilityBattleProfile(Ground,
            PhysicalAbilityDelivery.AdjacentUnit, [], [new(A, [Custom], 0)]));
    }
}
