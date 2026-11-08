using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiClockTests
{
    public static IEnumerable<object[]> Cases
    {
        get
        {
            using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","ordinary-spell-ai-clock-v1.json")));
            return fixture.RootElement.GetProperty("cases").EnumerateArray().Select(c=>new object[]{c.GetRawText()}).ToArray();
        }
    }

    [Theory] [MemberData(nameof(Cases))]
    public void Source_vectors_preserve_phase_refund_opportunities_and_event_limit(string json)
    {
        using var d=JsonDocument.Parse(json);var row=d.RootElement;var input=row.GetProperty("input");var expected=row.GetProperty("expected");
        var units=input.GetProperty("units").EnumerateArray().Select(u=>new DefaultAbilityAiClockUnit(new(u.GetProperty("id").GetString()!),
            u.GetProperty("order").GetInt32(),u.GetProperty("speed").GetInt32(),u.GetProperty("ct").GetInt32(),
            u.GetProperty("dead").GetBoolean(),u.GetProperty("moved").GetBoolean(),u.GetProperty("acted").GetBoolean())).ToImmutableArray();
        var rng=new RandomState(57,9);
        var r=DefaultAbilityAiClock.Run(units,new(input.GetProperty("actor").GetString()!),input.GetProperty("charge").GetByte(),input.GetProperty("ticks").GetUInt64(),rng);
        Assert.True(r.Accepted,r.Rejection.ToString());var o=r.Outcome!;
        Assert.Equal(expected.GetProperty("ticks").GetUInt64(),o.Ticks);
        Assert.Equal(expected.GetProperty("remaining").GetByte(),o.RemainingCharge);
        Assert.Equal(expected.GetProperty("cts").EnumerateArray().Select(v=>v.GetInt32()),o.Units.Select(u=>u.Ct));
        Assert.True(o.HadMoveOpportunity.SetEquals(expected.GetProperty("moved").EnumerateArray().Select(v=>new BattleUnitId(v.GetString()!))));
        Assert.Equal(expected.GetProperty("upkeepDraws").GetInt32(),o.UpkeepPicks.Length);
        Assert.Equal(expected.GetProperty("eventCount").GetInt32(),o.Events.Length);
        Assert.Equal(expected.GetProperty("limit").GetBoolean(),o.EventLimitReached);
        Assert.Equal(9UL+(ulong)o.UpkeepPicks.Length,r.RngState.DrawCount);
        uint seed=57;foreach(var p in o.UpkeepPicks){seed=unchecked(seed*1103515245+12345);Assert.Equal((seed>>16)&32767,p.RawRoll);}
        Assert.Equal(seed,r.RngState.Value);
        Assert.Equal(0,units[0].Ct); // Pure copied input remains untouched.
    }

    private static ImmutableArray<DefaultAbilityAiClockUnit> Units=>[new(new("mage"),0,10,0,ActionTaken:true),new(new("guard"),1,8,100)];

    [Fact]
    public void Exhaustion_after_private_upkeep_rolls_back_every_event_and_tick()
    {
        var rng=new RandomState(57,ulong.MaxValue-1);
        var units=Units.Add(new(new("third"),2,8,100));
        var r=DefaultAbilityAiClock.Run(units,new("mage"),4,10,rng);
        Assert.False(r.Accepted);Assert.Null(r.Outcome);Assert.Equal(rng,r.RngState);
        Assert.Equal(100,units[1].Ct);
    }

    [Fact]
    public void Tick_exhaustion_and_zero_draw_readiness_use_the_exact_budget()
    {
        var r=DefaultAbilityAiClock.Run(Units.SetItem(1,Units[1] with {Ct=0}),new("mage"),1,ulong.MaxValue,new(57,ulong.MaxValue));
        Assert.True(r.Accepted);Assert.Equal(0UL,r.Outcome!.ElapsedTicks);Assert.Equal(ulong.MaxValue,r.RngState.DrawCount);
        r=DefaultAbilityAiClock.Run(Units,new("mage"),4,ulong.MaxValue,new(57,0));
        Assert.Equal(DefaultAbilityAiClockRejection.TickCountExhausted,r.Rejection);Assert.Equal(new RandomState(57,0),r.RngState);
    }

    [Fact]
    public void Unsupported_quick_speed_dead_caster_or_unconsumed_act_cannot_enter_clock()
    {
        foreach(var actor in new[]{Units[0] with {Ct=255},Units[0] with {Speed=0},Units[0] with {Dead=true},Units[0] with {ActionTaken=false}})
            Assert.False(DefaultAbilityAiClock.Run(Units.SetItem(0,actor),new("mage"),4,10,new(57,0)).Accepted);
    }
}
