using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ActionExperienceTests
{
    [Fact]
    public void Fourteen_literal_awards_cover_widths_floor_bonus_cap_and_ko_division()
    {
        using var fixture = ActionRewardTestData.Fixture();
        foreach (var row in fixture.RootElement.GetProperty("experience").EnumerateArray())
        {
            var d = row.GetProperty("input");
            Assert.Equal(row.GetProperty("expected").GetByte(), ActionExperience.Accumulate(d[0].GetByte(),
                d[1].GetByte(), d[2].GetByte(), d[3].GetByte(), (ActionTargetOutcome)d[4].GetInt32(), d[5].GetBoolean(), d[6].GetBoolean()));
        }
    }

    [Fact]
    public void Multi_target_and_reaction_sequence_keeps_maximum_instead_of_sum()
    {
        byte exp = ActionExperience.Accumulate(0, 10, 10, 0, ActionTargetOutcome.Changed, true, false);
        exp = ActionExperience.Accumulate(exp, 10, 14, 0, ActionTargetOutcome.Changed, true, false);
        exp = ActionExperience.Accumulate(exp, 10, 99, 0, ActionTargetOutcome.Changed, false, true);
        exp = ActionExperience.Accumulate(exp, 10, 10, 0, ActionTargetOutcome.Changed, true, false);
        Assert.Equal((byte)14, exp);
    }

    [Fact]
    public void Killed_uses_supplied_post_increment_ko_byte_including_wrap_to_zero()
    {
        Assert.Equal((byte)1, ActionExperience.Accumulate(0, 10, 10, 255, ActionTargetOutcome.Killed, true, false));
        Assert.Equal((byte)20, ActionExperience.Accumulate(0, 10, 10, 0, ActionTargetOutcome.Killed, true, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionExperience.Accumulate(0, 1, 1, 0, (ActionTargetOutcome)3, true, false));
    }
}
