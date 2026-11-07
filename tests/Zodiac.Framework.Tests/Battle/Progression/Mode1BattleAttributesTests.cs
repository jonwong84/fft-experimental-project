using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class Mode1BattleAttributesTests
{
    [Fact]
    public void Fourteen_literals_preserve_integer_widths_caps_egg_rounding_and_base_attributes()
    {
        using var fixture = DerivedAttributeTestData.Fixture();
        foreach (var row in fixture.RootElement.GetProperty("mode1").EnumerateArray())
        {
            var d = row.GetProperty("input");
            var r = Mode1BattleAttributes.CalculateWithoutEquipment(GrowthUnlockTestData.Raw(d.GetProperty("raw")),
                DerivedAttributeTestData.Multipliers(d), d.GetProperty("hp").GetUInt16(), d.GetProperty("mp").GetUInt16(),
                DerivedAttributeTestData.Triple(d.GetProperty("base")), d.GetProperty("egg").GetBoolean());
            Assert.Equal(DerivedAttributeTestData.Expected(row.GetProperty("expected")), r);
        }
    }

    [Fact]
    public void Reward_growth_feeds_mode1_maxima_without_refilling_or_rederiving_base_stats()
    {
        var raw = new BattleRawStats(1638400, 1638400, 1638400, 1638400, 1638400);
        var reward = ActionRewardCalculation.ProjectExperience(95, 10, 10);
        Assert.True(reward.RequiresStatGrowth);
        var growth = BattleRawStatGrowth.Calculate(raw, new(11, 15, 100, 60, 50), 10, false, new(57, 9));
        Assert.True(growth.Accepted);
        var result = Mode1BattleAttributes.CalculateWithoutEquipment(growth.Stats!, new(100, 75, 100, 90, 80), 100, 75, new(7, 8, 9));
        Assert.Equal(new(100, 104, 75, 78, new(7, 8, 9)), result);
        Assert.Equal(new RandomState(2224270474, 14), growth.RngState);
        Assert.Equal(1638400u, raw.Hp); Assert.Equal((byte)11, reward.Level);
    }

    [Fact]
    public void Invalid_raw_captures_are_rejected_and_repeated_projection_preserves_input_values()
    {
        var raw = new BattleRawStats(1638400, 1638400, 1, 2, 3); var copy = raw with { };
        var multipliers = new BattleStatMultipliers(100, 75, 255, 255, 255);
        var a = Mode1BattleAttributes.CalculateWithoutEquipment(raw, multipliers, 200, 10, new(255, 0, 255));
        var b = Mode1BattleAttributes.CalculateWithoutEquipment(raw, multipliers, 200, 10, new(255, 0, 255));
        Assert.Equal(a, b); Assert.Equal(copy, raw); Assert.Equal(new BattleAttributeValues(255, 0, 255), a.BaseAttributes);
        Assert.Throws<ArgumentNullException>(() => Mode1BattleAttributes.CalculateWithoutEquipment(null!, multipliers, 0, 0, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mode1BattleAttributes.CalculateWithoutEquipment(raw with { MagicAttack = 0x1000000 }, multipliers, 0, 0, default));
    }
}
