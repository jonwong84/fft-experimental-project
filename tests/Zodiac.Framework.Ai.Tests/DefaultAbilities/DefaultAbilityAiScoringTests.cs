using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiScoringTests
{
    private static DefaultAbilityAiStatistics Stats(int minimum = 6) => DefaultAbilityAiStatistics.Calculate(
        [new(0,false,true),new(0,false,false),new(minimum,true,false)]);

    [Theory]
    [InlineData(0,0)] [InlineData(5,0)] [InlineData(6,1)] [InlineData(69,14)] [InlineData(75,16)]
    public void Mp_source_floor_and_below_cheapest_order(ushort mp,int expected) =>
        Assert.Equal(expected,DefaultAbilityAiScoring.MpTerm(mp,75,Stats()));

    [Fact]
    public void Fire_ready_absolute_score_includes_actor_remaining_mp_not_only_damage_delta()
    {
        var none=DefaultAbilityAiStatistics.Calculate([new(0,false,true),new(0,false,false)]);
        var score=DefaultAbilityAiScoring.Calculate([
            new(new("enemy"),1,new(146,200,50,70),50,false,true,none),
            new(new("actor"),0,new(100,100,75,70),69,false,false,Stats())],100);
        Assert.Equal((short)49,score.Priority); Assert.Equal(14,score.Contributions[0].MpTerm);
        Assert.Equal(357,score.Contributions[0].EquipmentTerm); Assert.Equal(0,score.Contributions[1].MpTerm);
        Assert.Equal("actor",score.Contributions[0].UnitId.Value);
    }

    [Fact]
    public void Odd_mp_share_halves_before_multiplication_and_zero_maximum_scores_zero()
    {
        var s=DefaultAbilityAiStatistics.Calculate([new(0,false,false),new(48,true,false),new(48,true,false),new(48,true,false)]);
        Assert.Equal(15,DefaultAbilityAiScoring.MpTerm(47,75,s));
        Assert.Equal(30,DefaultAbilityAiScoring.MpTerm(48,75,s));
        Assert.Equal(0,DefaultAbilityAiScoring.MpTerm(0,0,s));
    }

    [Theory]
    [InlineData(20,true,false,350)] [InlineData(0,false,true,165)]
    public void Critical_and_dead_keep_equipment_terms(ushort hp,bool critical,bool dead,int expected)
    {
        var s=DefaultAbilityAiScoring.Calculate([new(new("unit"),0,new(hp,100,0,70,critical),0,dead,false,Stats())],100);
        Assert.Equal(expected,s.Priority);
    }

    [Fact]
    public void Running_priority_narrows_after_each_source_unit_and_retains_strike_credit()
    {
        var s=DefaultAbilityAiScoring.Calculate([new(new("unit"),0,new(100,100,0,70),0,false,false,Stats())],87,short.MaxValue);
        Assert.Equal(unchecked((short)(32767+485)),s.Priority); Assert.Equal((byte)87,s.HitPercent);
        Assert.Throws<ArgumentException>(()=>DefaultAbilityAiScoring.Calculate([new(new("bad"),0,new(0,100,0,70),0,false,false,Stats())],100));
    }
}
