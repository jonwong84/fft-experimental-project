using System.Text.Json;
using Xunit;
using Zodiac.Framework.CombatRules;
using Zodiac.Framework.Formulas;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

public sealed class AbilityFormulaTests
{
    public static IEnumerable<object[]> FormulaCases()
    {
        using var fixture = ReadFixture();
        return fixture.RootElement.GetProperty("cases").EnumerateArray()
            .Where(row => !row.GetProperty("input").GetProperty("evadeable").GetBoolean())
            .Select(row => new object[] { row.GetProperty("id").GetString()! }).ToArray();
    }

    private static JsonDocument ReadFixture() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "physical-formula-assignment-v2.json")));

    [Theory]
    [MemberData(nameof(FormulaCases))]
    public void Dispatcher_preserves_existing_source_vectors_and_formula_only_rng(string id)
    {
        // Reuse the unchanged source vectors. Non-evadeable rows enter the formula without prior draws.
        using var fixture = ReadFixture();
        var row = fixture.RootElement.GetProperty("cases").EnumerateArray()
            .Single(row => row.GetProperty("id").GetString() == id);
        var input = row.GetProperty("input");
        var expected = row.GetProperty("expected");
        var parameters = new AbilityFormulaParameters(input.GetProperty("x").GetInt32(), input.GetProperty("y").GetInt32());
        var formula = new AbilityFormula((AbilityFormulaId)input.GetProperty("formula").GetInt32(), parameters);
        var context = new AbilityFormulaContext(input.GetProperty("pa").GetInt32(),
            input.GetProperty("attackerBrave").GetInt32(), input.GetProperty("targetBrave").GetInt32(),
            Enum.Parse<AbilityFormulaMode>(input.GetProperty("mode").GetString()!));
        var rng = new RandomState(input.GetProperty("word").GetUInt32(), input.GetProperty("count").GetUInt64());

        if (!expected.GetProperty("accepted").GetBoolean())
        {
            Assert.Throws<OverflowException>(() => formula.Evaluate(context, rng));
            return;
        }

        var result = formula.Evaluate(context, rng);
        Assert.Equal(expected.GetProperty("damage").GetInt32(), Math.Min(999, result.Damage));
        Assert.Equal(expected.GetProperty("critical").GetBoolean(), result.Critical);
        Assert.Equal(expected.GetProperty("attempted").GetBoolean(), result.KnockbackAttempted);
        Assert.Equal(new RandomState(expected.GetProperty("word").GetUInt32(), expected.GetProperty("count").GetUInt64()), result.RngState);
        Assert.Equal(result, formula.Evaluate(context, rng));
        Assert.Same(parameters, formula.Parameters);
    }

    [Theory]
    [InlineData(AbilityFormulaId.RandomPa)]
    [InlineData(AbilityFormulaId.RandomPaPlusHalfPa)]
    public void Unused_Z_is_retained_in_definition_without_changing_current_handler_results(AbilityFormulaId id)
    {
        var parameters = new AbilityFormulaParameters(0, 0, 255);
        var formula = new AbilityFormula(id, parameters);
        var original = new AbilityFormula(id, new(0, 0));
        var context = new AbilityFormulaContext(5, 70, 70, AbilityFormulaMode.Execution);
        var rng = new RandomState(1, 0);
        Assert.Same(parameters, formula.Parameters);
        Assert.Equal(255, formula.Parameters.Z);
        Assert.Equal(original.Evaluate(context, rng), formula.Evaluate(context, rng));
        Assert.Equal(original.CalculateDamage(5, 32767), formula.CalculateDamage(5, 32767));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityFormulaParameters(0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityFormulaParameters(0, 0, 256));
    }

    [Fact]
    public void Missing_or_unsupported_evaluation_inputs_fail_before_consuming_rng()
    {
        Assert.Throws<ArgumentNullException>(() => new AbilityFormula(AbilityFormulaId.RandomPa, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityFormulaContext(0, 70, 70, AbilityFormulaMode.Execution));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityFormulaContext(5, 9, 70, AbilityFormulaMode.Execution));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityFormulaContext(5, 70, 101, AbilityFormulaMode.Execution));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityFormulaContext(5, 70, 70, (AbilityFormulaMode)99));
        var formula = new AbilityFormula(AbilityFormulaId.RandomPa, new(0, 4));
        Assert.Throws<ArgumentNullException>(() => formula.Evaluate(null!, new(0, ulong.MaxValue)));
    }

    [Theory]
    [InlineData(AbilityFormulaMode.Preview)]
    [InlineData(AbilityFormulaMode.Ai)]
    public void Prediction_uses_midpoint_and_skips_execution_only_effect_draws_even_with_exhausted_rng(AbilityFormulaMode mode)
    {
        var context = new AbilityFormulaContext(5, 70, 70, mode);
        var entry = new RandomState(0, ulong.MaxValue);
        var randomPa = new AbilityFormula(AbilityFormulaId.RandomPa, new(0, 4));
        var sum = new AbilityFormula(AbilityFormulaId.RandomPaPlusHalfPa, new(9, 0));
        Assert.Equal(new AbilityFormulaResult(15, false, false, entry), randomPa.Evaluate(context, entry));
        Assert.Equal(new AbilityFormulaResult(35, false, false, entry), sum.Evaluate(context, entry));
    }

    [Fact]
    public void Dispatcher_returns_uncapped_damage_while_combat_rules_finalize_the_result()
    {
        var formula = new AbilityFormula(AbilityFormulaId.RandomPa, new(0, 255));
        var context = new AbilityFormulaContext(99, 70, 70, AbilityFormulaMode.Preview);
        var entry = new RandomState(1, 0);
        var result = formula.Evaluate(context, entry);
        Assert.Equal(12672, result.Damage);
        var ability = new PhysicalAbilityDefinition(new("test:formula-boundary"), PhysicalAbilityDelivery.AdjacentUnit, formula);
        var finalized = PhysicalDamageRules.Calculate(ability, 99, 70, 70, 0, 1, 0, BattleFacing.West, context.Mode, entry);
        Assert.True(finalized.Accepted);
        Assert.Equal(999, finalized.Outcome!.Damage);
        Assert.Equal(result.RngState, finalized.RngState);
    }
}
