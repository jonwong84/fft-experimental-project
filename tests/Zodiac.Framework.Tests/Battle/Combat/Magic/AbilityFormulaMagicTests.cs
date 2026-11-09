using System.Text.Json;
using Xunit;
using Zodiac.Framework.Formulas;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AbilityFormulaMagicTests
{
    public static IEnumerable<object[]> AmountCases()
    {
        using var doc = FaithMagicArithmeticTests.Fixture();
        return doc.RootElement.GetProperty("amounts").EnumerateArray()
            .Select(row => new object[] { row.GetProperty("id").GetString()! }).ToArray();
    }

    public static IEnumerable<object[]> RouteCases()
    {
        using var doc = MagicElementTestData.Fixture();
        return doc.RootElement.GetProperty("routes").EnumerateArray()
            .Select(row => new object[] { row.GetProperty("id").GetString()! }).ToArray();
    }

    private static AbilityFormulaId FormulaId(FaithMagicAmountKind kind) => kind == FaithMagicAmountKind.Damage
        ? AbilityFormulaId.FaithMagicDamage : AbilityFormulaId.FaithMagicHealing;

    private static AbilityFormulaContext Context(FaithMagicAmountInput input, bool undead,
        MagicDamageElementContext? elements, AbilityFormulaMode mode) => new(
            new AbilityFormulaMagicContext(input.MagicAttack, input.CasterFaith, input.TargetFaith,
                undead, input.Modifiers, elements), mode);

    [Theory]
    [MemberData(nameof(AmountCases))]
    public void Dispatcher_preserves_existing_literal_MA_modifier_Faith_and_signed_store_vectors(string id)
    {
        using var doc = FaithMagicArithmeticTests.Fixture();
        var row = doc.RootElement.GetProperty("amounts").EnumerateArray().Single(row => row.GetProperty("id").GetString() == id);
        var input = MagicElementTestData.Input(row.GetProperty("input"));
        var expected = row.GetProperty("expected");
        var formula = new AbilityFormula(FormulaId(input.Kind), new(0, input.Power));
        var context = Context(input, false, new(MagicElement.Dark, MagicDamageWeather.None, new(), new(true, true, true)), AbilityFormulaMode.Execution);
        var rng = new RandomState(57, ulong.MaxValue);
        var result = formula.Evaluate(context, rng);
        var projection = result.MagicProjection!;
        Assert.Equal(expected.GetProperty("xa").GetInt16(), projection.ModifiedXa);
        Assert.Equal(expected.GetProperty("product").GetInt16(), projection.StoredProduct);
        Assert.Equal(expected.GetProperty("amount").GetInt16(), projection.Amount!.Amount);
        Assert.Equal(input.MagicAttack, context.MagicalAttack);
        Assert.Equal(input.CasterFaith, context.AttackerFaith);
        Assert.Equal(input.TargetFaith, context.TargetFaith);
        Assert.Null(context.PhysicalAttack);
        Assert.Null(context.Physical);
        Assert.Equal(rng, result.RngState);
    }

    [Theory]
    [MemberData(nameof(RouteCases))]
    public void Damage_healing_absorption_nullification_and_undead_use_existing_literal_routes_in_every_mode(string id)
    {
        using var doc = MagicElementTestData.Fixture();
        var row = doc.RootElement.GetProperty("routes").EnumerateArray().Single(row => row.GetProperty("id").GetString() == id);
        var input = MagicElementTestData.Input(row.GetProperty("input"));
        var expected = row.GetProperty("expected");
        var formula = new AbilityFormula(FormulaId(input.Kind), new(0, input.Power));
        foreach (var mode in Enum.GetValues<AbilityFormulaMode>())
        {
            var context = Context(input, row.GetProperty("targetIsUndead").GetBoolean(), MagicElementTestData.Context(row.GetProperty("context")), mode);
            var entry = new RandomState(1, ulong.MaxValue);
            var result = formula.Evaluate(context, entry);
            Assert.Equal(expected.GetProperty("hpDamage").GetInt16(), result.Damage);
            Assert.Equal(expected.GetProperty("hpHealing").GetInt16(), result.Healing);
            Assert.False(result.Critical);
            Assert.False(result.KnockbackAttempted);
            Assert.Equal(entry, result.RngState);
        }
    }

    [Fact]
    public void Formula_assignment_selects_damage_or_healing_from_the_same_magic_context()
    {
        var context = new AbilityFormulaContext(new AbilityFormulaMagicContext(10, new(70), new(60), false,
            Elements: new(MagicElement.None, MagicDamageWeather.None, new(), new(true))), AbilityFormulaMode.Preview);
        var entry = new RandomState(1, 0);
        var damage = new AbilityFormula(AbilityFormulaId.FaithMagicDamage, new(0, 20)).Evaluate(context, entry);
        var healing = new AbilityFormula(AbilityFormulaId.FaithMagicHealing, new(0, 20)).Evaluate(context, entry);
        Assert.Equal(84, damage.Damage);
        Assert.Equal(0, damage.Healing);
        Assert.Equal(0, healing.Damage);
        Assert.Equal(84, healing.Healing);
        Assert.Equal(entry, damage.RngState);
        Assert.Equal(entry, healing.RngState);
    }

    [Fact]
    public void Wrong_or_missing_family_inputs_fail_instead_of_using_zero_stats()
    {
        var physical = new AbilityFormulaContext(new AbilityFormulaPhysicalContext(5, 70, 70), AbilityFormulaMode.Execution);
        var magic = new AbilityFormulaContext(new AbilityFormulaMagicContext(5, new(70), new(70), false), AbilityFormulaMode.Execution);
        var entry = new RandomState(1, 0);
        Assert.Throws<ArgumentException>(() => new AbilityFormula(AbilityFormulaId.FaithMagicHealing, new(0, 14)).Evaluate(physical, entry));
        Assert.Throws<ArgumentException>(() => new AbilityFormula(AbilityFormulaId.PummelFormula, new(9, 0)).Evaluate(magic, entry));
        Assert.Throws<ArgumentNullException>(() => new AbilityFormula(AbilityFormulaId.FaithMagicDamage, new(0, 14)).Evaluate(magic, entry));
        Assert.Throws<NotSupportedException>(() => new AbilityFormula((AbilityFormulaId)0x09, new(0, 14)));
    }

    [Theory]
    [InlineData(AbilityFormulaId.FaithMagicDamage)]
    [InlineData(AbilityFormulaId.FaithMagicHealing)]
    public void Physical_delivery_profiles_do_not_implicitly_admit_magic_ids(AbilityFormulaId id)
    {
        var formula = new AbilityFormula(id, new(0, 14));
        Assert.Throws<NotSupportedException>(() => new PhysicalAbilityDefinition(new("test:magic"), PhysicalAbilityDelivery.AdjacentUnit, formula));
    }
}
