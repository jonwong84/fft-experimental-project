using System.Text.Json;
using Xunit;
using static Zodiac.Framework.Tests.FaithMagicArithmeticTests;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class FaithMagicAmountTests
{
    private static FaithMagicAmountInput Input => new(FaithMagicAmountKind.Damage,5,14,new(70),new(70));
    private static bool Flag(JsonElement d,string key) => d.TryGetProperty(key,out var v) && v.GetBoolean();

    [Fact]
    public void Twenty_two_shared_amount_vectors_cover_tiers_modifier_order_defense_bypass_floors_and_overflow()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("amounts").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var m=d.GetProperty("modifiers");
            var modifiers=new FaithMagicAmountModifiers(Flag(m,"strengthensElement"),Flag(m,"magicAttackUp"),Flag(m,"magicDefenseUp"),Flag(m,"targetShell"),Flag(m,"targetChickenOrFrog"),
                m.TryGetProperty("compatibility",out var c) ? Enum.Parse<MagicCompatibility>(c.GetString()!) : MagicCompatibility.Neutral);
            var input=new FaithMagicAmountInput(Enum.Parse<FaithMagicAmountKind>(d.GetProperty("kind").GetString()!),d.GetProperty("magicAttack").GetByte(),d.GetProperty("power").GetByte(),Faith(d.GetProperty("caster")),Faith(d.GetProperty("target")),modifiers);
            var actual=FaithMagicAmountFormula.ProjectAfterHit(input,new(true));
            Assert.Equal(e.GetProperty("xa").GetInt16(),actual.ModifiedXa);Assert.Equal(e.GetProperty("product").GetInt16(),actual.StoredProduct);
            Assert.Equal(e.GetProperty("amount").GetInt16(),actual.Amount);
            Assert.Equal(input.CasterFaith.Effective,actual.EffectiveCasterFaith);Assert.Equal(input.TargetFaith.Effective,actual.EffectiveTargetFaith);count++;
        }
        Assert.Equal(22,count);
    }

    [Theory] [InlineData(-1)] [InlineData(0)] [InlineData(1)] [InlineData(13)]
    public void Only_the_two_admitted_amount_branches_are_exposed(int kind) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicAmountFormula.ProjectAfterHit(Input with { Kind=(FaithMagicAmountKind)kind },new(true)));

    [Theory] [InlineData(-1)] [InlineData(6)]
    public void Compatibility_must_be_explicitly_classified(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FaithMagicAmountFormula.ProjectAfterHit(Input with { Modifiers=new(Compatibility:(MagicCompatibility)value) },new(true)));

    [Fact]
    public void Damage_requires_neutral_intervening_steps_while_healing_does_not_run_them()
    {
        Assert.Throws<NotSupportedException>(() => FaithMagicAmountFormula.ProjectAfterHit(Input,new()));
        Assert.Equal((short)34,FaithMagicAmountFormula.ProjectAfterHit(Input with { Kind=FaithMagicAmountKind.Healing },new()).Amount);
    }

    [Fact]
    public void Missing_inputs_are_not_interpreted_as_neutral_facts()
    {
        Assert.Throws<ArgumentNullException>(() => FaithMagicAmountFormula.ProjectAfterHit(null!,new(true)));
        Assert.Throws<ArgumentNullException>(() => FaithMagicAmountFormula.ProjectAfterHit(Input,null!));
    }
}
