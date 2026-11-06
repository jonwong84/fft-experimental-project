using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BattleRawStatGrowthTests
{
    [Fact]
    public void Fifteen_source_literals_match_raw_arithmetic_and_complete_rng_continuation()
    {
        using var f = GrowthUnlockTestData.Fixture();
        foreach (var row in f.RootElement.GetProperty("growth").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected"); var g = d.GetProperty("growth");
            var r = BattleRawStatGrowth.Calculate(GrowthUnlockTestData.Raw(d.GetProperty("raw")),
                new(g[0].GetByte(), g[1].GetByte(), g[2].GetByte(), g[3].GetByte(), g[4].GetByte()),
                d.GetProperty("level").GetByte(), d.GetProperty("down").GetBoolean(), new(d.GetProperty("word").GetUInt32(), d.GetProperty("count").GetUInt64()));
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), r.Accepted);
            Assert.Equal(new(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), r.RngState);
            if (r.Accepted) Assert.Equal(GrowthUnlockTestData.Raw(e.GetProperty("raw")), r.Stats); else Assert.Null(r.Stats);
        }
    }

    [Fact]
    public void Reward_then_growth_uses_old_level_and_preserves_the_published_input_rng()
    {
        var reward = ActionRewardCalculation.ProjectExperience(95, 10, 10);
        var raw = new BattleRawStats(1000, 1000, 1000, 1000, 1000); var rates = new BattleGrowthRates(11, 11, 11, 11, 11);
        var rng = new RandomState(57, 9);
        Assert.True(reward.RequiresStatGrowth); Assert.Equal((byte)11, reward.Level);
        var grown = BattleRawStatGrowth.Calculate(raw, rates, 10, false, rng);
        var special = BattleRawStatGrowth.Calculate(raw, rates, reward.Level, false, rng);
        Assert.Equal(1047u, grown.Stats!.Hp); Assert.Equal(1045u, special.Stats!.Hp);
        Assert.Equal(new(2224270474, 14), grown.RngState); Assert.Equal(new(57, 9), rng); Assert.Equal(1000u, raw.Hp);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Zero_values_still_consume_all_five_draws_and_replay_is_equal(bool down)
    {
        var raw = new BattleRawStats(0, 0, 0, 0, 0); var rates = new BattleGrowthRates(0, 0, 0, 0, 0);
        var a = BattleRawStatGrowth.Calculate(raw, rates, 0, down, new(0, 0));
        var b = BattleRawStatGrowth.Calculate(raw, rates, 0, down, new(0, 0));
        Assert.Equal(a, b); Assert.Equal(raw, a.Stats); Assert.Equal(new(229283573, 5), a.RngState);
    }

    [Fact]
    public void Invalid_late_raw_field_and_counter_exhaustion_return_no_partial_output()
    {
        var rates = new BattleGrowthRates(11, 15, 100, 60, 50);
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleRawStatGrowth.Calculate(new(1, 2, 3, 4, 0x1000000), rates, 10, false, new(57, 9)));
        Assert.Throws<ArgumentNullException>(() => BattleRawStatGrowth.Calculate(null!, rates, 10, false, new(57, 9)));
        Assert.Throws<ArgumentNullException>(() => BattleRawStatGrowth.Calculate(new(1, 2, 3, 4, 5), null!, 10, false, new(57, 9)));
        var rng = new RandomState(57, ulong.MaxValue - 4);
        var r = BattleRawStatGrowth.Calculate(new(1, 2, 3, 4, 5), rates, 10, false, rng);
        Assert.False(r.Accepted); Assert.Null(r.Stats); Assert.Equal(rng, r.RngState);
    }
}
