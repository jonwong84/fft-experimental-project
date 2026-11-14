using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiStoredHitTests
{
    [Fact]
    public void Ready_follow_anchor_can_leave_initial_blue_range_without_being_rejected()
    {
        var (_,catalog,state)=DefaultAbilityAiReadyTests.Setup();
        var work=new DefaultAbilityAiWork(state,catalog,new("mage"),0,true);
        var aim=work.Capture(new(4,2,0),true);
        var guard=work.Units[1];var units=work.Units.SetItem(1,guard with {Occupant=guard.Occupant with {Position=new(6,2,0)}});
        Assert.DoesNotContain(new TilePosition(6,2,0),DefaultAbilityBattle.Choice(state,new("mage"),work.Spell.Id).Outcome!.Range!.Panels);
        Assert.True(DefaultAbilityAiHitSimulation.ActionHitsSimulate(work,units,aim,[],new(57,0),out var hit));
        Assert.True(hit.Accounting.Valuable);Assert.Equal((ushort)146,hit.Units[1].Vitals.Hp);
        Assert.Equal(4UL,hit.RngState.DrawCount);
    }

    [Fact]
    public void Stored_tile_stays_put_while_a_followed_anchor_moves()
    {
        var (_,catalog,state)=DefaultAbilityAiReadyTests.Setup();
        var work=new DefaultAbilityAiWork(state,catalog,new("mage"),0,false);
        var aim=work.Capture(new(4,2,0),false);var guard=work.Units[1];
        var units=work.Units.SetItem(1,guard with {Occupant=guard.Occupant with {Position=new(6,2,0)}});
        Assert.False(DefaultAbilityAiHitSimulation.ActionHitsSimulate(work,units,aim,[],new(57,0),out var hit));
        Assert.False(hit.Accounting.Valuable);Assert.Empty(hit.Frames);Assert.Equal(new RandomState(57,0),hit.RngState);
        Assert.Equal((ushort)75,hit.Units[0].Current.Mp);
    }

    [Fact]
    public void Shared_hit_owner_retains_excluded_formula_draws_and_input_state()
    {
        var (_,catalog,state)=DefaultAbilityAiReadyTests.Setup(area:4);
        var work=new DefaultAbilityAiWork(state,catalog,new("mage"),0,true);
        var before=DefaultAbilityAiReadyTests.Receipt(state);
        Assert.True(DefaultAbilityAiHitSimulation.ActionHitsSimulate(work,work.Units,work.Capture(new(4,2,0),true),[new("guard")],new(57,0),out var hit));
        Assert.Equal(8UL,hit.RngState.DrawCount);Assert.Equal((ushort)100,hit.Units[0].Vitals.Hp);
        Assert.Equal((ushort)146,hit.Units[1].Vitals.Hp);Assert.Equal(before,DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Late_failure_never_mutates_private_input_or_publishes_partial_work()
    {
        var (_,catalog,state)=DefaultAbilityAiReadyTests.Setup(area:2);
        var work=new DefaultAbilityAiWork(state,catalog,new("mage"),0,false);
        Assert.Throws<NotSupportedException>(()=>DefaultAbilityAiHitSimulation.ActionHitsSimulate(work,work.Units,
            work.Capture(new(2,2,0),false),[],new(57,ulong.MaxValue-7),out _));
        Assert.Equal((ushort)75,work.Units[0].Current.Mp);Assert.Equal((ushort)200,work.Units[1].Vitals.Hp);
    }
}
