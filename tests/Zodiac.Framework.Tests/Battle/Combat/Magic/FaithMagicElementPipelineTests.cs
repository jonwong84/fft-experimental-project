using Xunit;
using static Zodiac.Framework.Tests.MagicElementTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class FaithMagicElementPipelineTests
{
    private static FaithMagicAmountInput Basic => new(FaithMagicAmountKind.Damage,5,14,new(70),new(70));
    private static MagicDamageElementContext Plain => new(new ElementToggles { Dark = true },MagicDamageWeather.None,new(),new(true));

    [Fact]
    public void Twelve_source_projections_put_elements_before_Faith_and_skip_Faith_when_nullified()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("projections").EnumerateArray())
        {
            var e=row.GetProperty("expected");var r=FaithMagicAmountFormula.ProjectWithElementsAfterHit(Input(row.GetProperty("input")),Context(row.GetProperty("context")));
            Assert.Equal(e.GetProperty("product").GetInt16(),r.StoredProduct);Assert.Equal(e.GetProperty("nullified").GetBoolean(),r.Nullified);
            if (e.GetProperty("preFaith").ValueKind==System.Text.Json.JsonValueKind.Null) Assert.Null(r.Elements);
            else Assert.Equal(e.GetProperty("preFaith").GetInt16(),r.Elements!.Amount);
            if (e.GetProperty("amount").ValueKind==System.Text.Json.JsonValueKind.Null) Assert.Null(r.Amount);
            else Assert.Equal(e.GetProperty("amount").GetInt16(),r.Amount!.Amount);
            count++;
        }
        Assert.Equal(12,count);
    }

    [Fact]
    public void All_prior_amount_vectors_remain_equal_through_an_unchanged_element_step()
    {
        using var doc=FaithMagicArithmeticTests.Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("amounts").EnumerateArray())
        {
            var input=Input(row.GetProperty("input"));
            Assert.Equal(FaithMagicAmountFormula.ProjectAfterHit(input,new(true)),FaithMagicAmountFormula.ProjectWithElementsAfterHit(input,Plain).Amount);count++;
        }
        Assert.Equal(22,count);
    }

    [Fact]
    public void Healing_does_not_read_missing_or_unsupported_element_context()
    {
        var input=Basic with { Kind=FaithMagicAmountKind.Healing };
        Assert.Equal((short)34,FaithMagicAmountFormula.ProjectWithElementsAfterHit(input).Amount!.Amount);
        var result=FaithMagicAmountFormula.ProjectWithElementsAfterHit(input,new(OriginalElementEncoding.FromByte(255),(MagicDamageWeather)99,new(new ElementToggles { Fire = true },new ElementToggles { Fire = true }),new()));
        Assert.Null(result.Elements);Assert.Equal((short)34,result.Amount!.Amount);
    }

    [Fact]
    public void Damage_and_all_calls_require_their_relevant_inputs()
    {
        Assert.Throws<ArgumentNullException>(() => FaithMagicAmountFormula.ProjectWithElementsAfterHit(Basic));
        Assert.Throws<ArgumentNullException>(() => FaithMagicAmountFormula.ProjectWithElementsAfterHit(null!));
    }

    [Fact]
    public void Known_element_set_cannot_contradict_strengthening() =>
        Assert.Throws<ArgumentException>(() => FaithMagicAmountFormula.ProjectWithElementsAfterHit(Basic with { Modifiers=new(StrengthensElement:true) },Plain with { Elements=ElementToggles.None }));

    [Fact]
    public void Composed_damage_does_not_bypass_transform_scope() =>
        Assert.Throws<NotSupportedException>(() => FaithMagicAmountFormula.ProjectWithElementsAfterHit(Basic,Plain with { Elements=new ElementToggles { Fire = true } }));

    [Fact]
    public void Fourteen_routing_vectors_preserve_absorption_undead_masks_signed_bits_and_uncapped_values()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("routes").EnumerateArray())
        {
            var p=FaithMagicAmountFormula.ProjectWithElementsAfterHit(Input(row.GetProperty("input")),Context(row.GetProperty("context")));
            var r=FaithMagicHpRouting.Route(p,row.GetProperty("targetIsUndead").GetBoolean());var e=row.GetProperty("expected");
            Assert.Equal(Enum.Parse<FaithMagicHpRoute>(e.GetProperty("route").GetString()!),r.Route);
            Assert.Equal(e.GetProperty("hpDamage").GetInt16(),r.HpDamage);Assert.Equal(e.GetProperty("hpHealing").GetInt16(),r.HpHealing);
            Assert.Equal(e.GetProperty("clearFirstCanceledStatusByte").GetBoolean(),r.ClearFirstCanceledStatusByte);count++;
        }
        Assert.Equal(14,count);
    }

    [Fact]
    public void Routing_does_not_invent_a_missing_projection() => Assert.Throws<ArgumentNullException>(() => FaithMagicHpRouting.Route(null!,false));
}
