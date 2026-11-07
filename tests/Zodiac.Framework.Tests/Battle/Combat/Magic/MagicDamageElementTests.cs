using Xunit;
using static Zodiac.Framework.Tests.MagicElementTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class MagicDamageElementTests
{
    private static MagicDamageElementContext Plain(MagicElement elements = MagicElement.Dark) => new(elements,MagicDamageWeather.None,new(),new(true));

    [Fact]
    public void Twenty_four_source_transforms_preserve_weather_stores_affinity_order_markers_and_signed_amounts()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("transforms").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var r=MagicDamageElements.Transform(d.GetProperty("amount").GetInt16(),Context(d));
            Assert.Equal(e.GetProperty("weatherAmount").GetInt16(),r.WeatherAmount);Assert.Equal(e.GetProperty("amount").GetInt16(),r.Amount);
            Assert.Equal(e.GetProperty("nullified").GetBoolean(),r.Nullified);Assert.Equal(e.GetProperty("absorbed").GetBoolean(),r.Absorbed);
            Assert.Equal(e.GetProperty("weaknessApplied").GetBoolean(),r.WeaknessApplied);count++;
        }
        Assert.Equal(24,count);
    }

    [Theory] [InlineData(-1)] [InlineData(8)]
    public void Unknown_effective_weather_cannot_be_assumed_clear(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicDamageElements.Transform(5,Plain() with { EffectiveWeather=(MagicDamageWeather)value }));

    [Fact]
    public void Fresh_work_is_required_even_when_element_set_is_empty() =>
        Assert.Throws<NotSupportedException>(() => MagicDamageElements.Transform(5,Plain(MagicElement.None) with { Profile=new() }));

    [Theory] [InlineData(MagicElement.Fire)] [InlineData(MagicElement.Earth)]
    public void Relevant_Oil_Float_or_rider_facts_must_be_certified(MagicElement elements) =>
        Assert.Throws<NotSupportedException>(() => MagicDamageElements.Transform(5,Plain(elements)));

    [Fact]
    public void Unrelated_status_certificates_are_not_required()
    {
        Assert.Equal((short)5,MagicDamageElements.Transform(5,Plain()).Amount);
        Assert.Equal((short)5,MagicDamageElements.Transform(5,Plain(MagicElement.None)).Amount);
        Assert.Equal((short)5,MagicDamageElements.Transform(5,Plain(MagicElement.Fire) with { Profile=new(true,true,false) }).Amount);
        Assert.Equal((short)5,MagicDamageElements.Transform(5,Plain(MagicElement.Earth) with { Profile=new(true,false,true) }).Amount);
    }

    [Fact]
    public void Missing_context_and_scope_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => MagicDamageElements.Transform(5,null!));
        Assert.Throws<ArgumentNullException>(() => MagicDamageElements.Transform(5,Plain() with { Profile=null! }));
    }
}
