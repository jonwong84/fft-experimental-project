using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ActionRewardCalculationTests
{
    [Fact]
    public void Eight_literal_amounts_preserve_pre_bonus_spillover_and_monster_bypass()
    {
        using var fixture = ActionRewardTestData.Fixture();
        foreach (var row in fixture.RootElement.GetProperty("amounts").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected");
            Assert.Equal(new(e[0].GetByte(), e[1].GetInt32(), e[2].GetInt32(), e[3].GetInt32()),
                ActionRewardCalculation.Calculate(d[0].GetByte(), d[1].GetByte(), d[2].GetByte(), d[3].GetBoolean(), d[4].GetBoolean()));
        }
    }

    [Fact]
    public void Eight_literal_balances_preserve_byte_cap_one_level_and_lost_excess()
    {
        using var fixture = ActionRewardTestData.Fixture();
        foreach (var row in fixture.RootElement.GetProperty("balances").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected");
            Assert.Equal(new(e[0].GetByte(), e[1].GetByte(), e[2].GetBoolean()),
                ActionRewardCalculation.ProjectExperience(d[0].GetByte(), d[1].GetByte(), d[2].GetByte()));
        }
    }

    [Fact]
    public void Jp_uses_level_before_growth_instead_of_new_level()
    {
        var balance = ActionRewardCalculation.ProjectExperience(95, 10, 3);
        var amounts = ActionRewardCalculation.Calculate(3, 1, 10, false, true);
        Assert.Equal(new(0, 4, true), balance);
        Assert.Equal(new(10, 10, 15, 2), amounts);
    }

    [Fact]
    public void Monster_never_reads_job_level_but_nonmonster_requires_packed_nibble_domain()
    {
        Assert.Equal(new(10, 0, 0, 0), ActionRewardCalculation.Calculate(10, 255, 10, true, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionRewardCalculation.Calculate(10, 16, 10, false, false));
    }
}
