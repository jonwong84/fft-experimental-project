using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class OrdinarySpellAiParameterTests
{
    internal static OrdinarySpellAiListEntry Entry(byte ct = 5) => new(11, new("test:skillset/black"),
        new(0, OrdinarySpellAiListTests.Spell(cost: 7, charge: ct), OrdinarySpellAiListTests.FireBehavior, true));

    [Theory]
    [InlineData(false, false, false, false, false, 5)]
    [InlineData(false, false, false, true, true, 5)]
    [InlineData(true, true, false, false, false, 2)]
    [InlineData(false, true, false, false, false, 0)]
    [InlineData(true, true, true, false, false, 5)]
    public void Loader_preserves_source_support_quirk_and_persevere(bool monster, bool defend, bool persevere,
        bool shortCharge, bool nonCharge, byte expected)
    {
        var e = Entry(); e = e with { Slot = e.Slot with { Spell = e.Slot.Spell with { Persevere = persevere } } };
        var usage = new OrdinarySpellAiUsage(Usable: true, AlliesOnly: true);
        var result = OrdinarySpellAiParameters.Load(e, new(true, monster, defend, shortCharge, nonCharge), usage);
        Assert.Equal(expected, result.ChargeTime); Assert.Equal(3, result.MpCost);
        Assert.Same(e.Slot.Behavior, result.Behavior); Assert.Same(usage, result.Usage);
    }

    [Fact]
    public void Initial_usage_clears_low_policy_bits_but_preserves_restrictions_and_usability()
    {
        var retained = new OrdinarySpellAiUsage(true, true, true, true, true, false, true, true);
        var loaded = OrdinarySpellAiParameters.Load(Entry(), new(), retained);
        var usage = OrdinarySpellAiParameters.InitializeUsage(loaded);
        Assert.Equal(retained with { ConsumeAim = false, ForceConsumeAim = false, ExhaustiveOrigins = false }, usage);
        Assert.False(usage.ConsumeAim); // Static evade-with-motion never enters this API.
    }

    [Fact]
    public void Source_usage_branch_order_keeps_trajectory_ahead_of_area_and_caster_exclusion()
    {
        var a = OrdinarySpellAiParameters.Load(Entry(), new(), new());
        Assert.True(OrdinarySpellAiParameters.InitializeUsage(a with { Geometry = a.Geometry with { CannotHitCaster = true } }).ForceConsumeAim);
        Assert.True(OrdinarySpellAiParameters.InitializeUsage(a with { Geometry = a.Geometry with { Area = 0 } }).ForceConsumeAim);
        Assert.True(OrdinarySpellAiParameters.InitializeUsage(a with { Geometry = a.Geometry with { Range = 1, Area = 1 } }).ExhaustiveOrigins);
        var shaped = a with { Behavior = a.Behavior with { AreaShape = AiAbilityAreaShape.Cone } };
        Assert.True(OrdinarySpellAiParameters.InitializeUsage(shaped).ExhaustiveOrigins);
        Assert.False(OrdinarySpellAiParameters.InitializeUsage(shaped with { Behavior = shaped.Behavior with { DirectTrajectory = true } }).ExhaustiveOrigins);
    }
}
