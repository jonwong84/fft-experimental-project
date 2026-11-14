using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiStatisticTests
{
    [Fact]
    public void Fire_initialized_shares_include_attack_and_disabled_frog_entry()
    {
        var list = DefaultAbilityAiLists.Build(new(11, new("test:skillset/black"), [DefaultAbilityAiListTests.Slot(0)]),
            null, 5, DefaultAbilityAiListTests.Rows(0), 75);
        var s = DefaultAbilityAiStatistics.ForOrdinaryHumanList(list, new());
        Assert.Equal(3, s.InitializedEntryCount); Assert.Equal(1, s.MpQuarterShare);
        Assert.Equal(1, s.SilenceQuarterShare); Assert.Equal(1, s.EvadeQuarterShare);
        Assert.Equal(6, s.LowestMpCost); Assert.Equal(6, s.HighestMpCost);
    }

    [Fact]
    public void Monster_skill_entries_count_only_in_denominator_and_zero_cost_does_not_replace_minimum()
    {
        var s = DefaultAbilityAiStatistics.Calculate([new(0, false, false), new(6, true, false), new(48, true, true), new(1, true, true, true)]);
        Assert.Equal(4, s.InitializedEntryCount); Assert.Equal(2, s.MpQuarterShare);
        Assert.Equal(2, s.SilenceQuarterShare); Assert.Equal(1, s.EvadeQuarterShare);
        Assert.Equal(6, s.LowestMpCost); Assert.Equal(48, s.HighestMpCost);
    }

    [Fact]
    public void No_cost_and_empty_lists_retain_source_255_sentinel()
    {
        var empty = DefaultAbilityAiStatistics.Calculate([]);
        var zero = DefaultAbilityAiStatistics.Calculate([new(0, false, true)]);
        Assert.Equal(255, empty.LowestMpCost); Assert.Equal(255, zero.LowestMpCost);
        Assert.Equal(0, empty.MpQuarterShare); Assert.Equal(4, zero.EvadeQuarterShare);
        Assert.Equal(0, zero.HighestMpCost);
    }

    [Fact]
    public void Repeated_slots_and_half_mp_keep_their_own_initialized_counts()
    {
        var list = DefaultAbilityAiLists.Build(new(11, new("test:skillset/black"),
            [DefaultAbilityAiListTests.Slot(0, cost: 7), DefaultAbilityAiListTests.Slot(1, cost: 7)]), null, 5,
            DefaultAbilityAiListTests.Rows(0, 1), 75, true);
        var s = DefaultAbilityAiStatistics.ForOrdinaryHumanList(list, new(HalfMp: true));
        Assert.Equal(4, s.InitializedEntryCount); Assert.Equal(2, s.MpQuarterShare);
        Assert.Equal(3, s.LowestMpCost); Assert.Equal(3, s.HighestMpCost);
    }
}
