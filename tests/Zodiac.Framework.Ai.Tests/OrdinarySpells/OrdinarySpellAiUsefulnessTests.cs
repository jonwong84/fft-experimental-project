using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class OrdinarySpellAiUsefulnessTests
{
    private static OrdinarySpellAiConsidered Ability => OrdinarySpellAiParameters.Load(OrdinarySpellAiParameterTests.Entry(), new(), new(Usable: true));
    internal static MagicCurrentUnit Unit => new(new("target"), 5, 70, new(MagicZodiacSign.Aries, IsMale: true), new(), new(),
        Equipment: OrdinaryMagicBattleDefinition.BareEquipment, Mp: 50);

    [Theory]
    [InlineData(true, OrdinarySpellAiEffect.Useful)]
    [InlineData(false, OrdinarySpellAiEffect.Adverse)]
    public void Damage_preference_is_based_on_side_even_with_zero_faith(bool enemy, OrdinarySpellAiEffect expected)
    {
        Assert.Equal(expected, OrdinarySpellAiUsefulness.Classify(Ability, Unit with { Faith = 0 }, new(100, 200, 50, 70), enemy, false));
    }

    [Theory]
    [InlineData(49, OrdinarySpellAiEffect.Useful)]
    [InlineData(50, OrdinarySpellAiEffect.None)]
    [InlineData(51, OrdinarySpellAiEffect.None)]
    public void Healing_uses_hp_less_than_floor_half_not_half_of_a_percentage(ushort hp, OrdinarySpellAiEffect expected)
    {
        var a = Ability with { Behavior = Ability.Behavior with { TargetsEnemies = false, TargetsAllies = true, MagicalAttack = false } };
        Assert.Equal(expected, OrdinarySpellAiUsefulness.Classify(a, Unit, new(hp, 101, 50, 70), false, false));
        Assert.Equal(OrdinarySpellAiEffect.Adverse, OrdinarySpellAiUsefulness.Classify(a, Unit, new(hp, 101, 50, 70), true, false));
    }

    [Fact]
    public void Usage_side_restrictions_dead_and_caster_exclusion_precede_preferences()
    {
        Assert.Equal(OrdinarySpellAiEffect.None, OrdinarySpellAiUsefulness.Classify(Ability with { Usage = new(EnemiesOnly: true) }, Unit, new(100,200,50,70), false, false));
        Assert.Equal(OrdinarySpellAiEffect.None, OrdinarySpellAiUsefulness.Classify(Ability with { Geometry = Ability.Geometry with { CannotHitCaster = true } }, Unit, new(100,200,50,70), false, true));
        Assert.Equal(OrdinarySpellAiEffect.None, OrdinarySpellAiUsefulness.Classify(Ability, Unit with { Statuses = new(Dead: true) }, new(0,200,50,70), true, false));
        Assert.Equal(OrdinarySpellAiEffect.Adverse, OrdinarySpellAiUsefulness.Classify(Ability with { Behavior = Ability.Behavior with { TargetsAllies = true } }, Unit, new(10,200,50,70), false, false));
    }

    [Fact]
    public void Untraced_flags_and_status_combinations_are_rejected()
    {
        Assert.Throws<NotSupportedException>(() => OrdinarySpellAiUsefulness.Classify(Ability with { Behavior = Ability.Behavior with { RandomUse = true } }, Unit, new(100,200,50,70), true, false));
        Assert.Throws<NotSupportedException>(() => OrdinarySpellAiUsefulness.Classify(Ability, Unit with { Statuses = new(Sleep: true) }, new(100,200,50,70), true, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Hp_effect_without_side_preferences_is_useful_on_either_side(bool enemy)
    {
        var ability = Ability with
        {
            Behavior = Ability.Behavior with { TargetsEnemies = false, TargetsAllies = false }
        };

        Assert.Equal(OrdinarySpellAiEffect.Useful,
            OrdinarySpellAiUsefulness.Classify(ability, Unit, new(200, 200, 50, 70), enemy, false));
        Assert.Equal(OrdinarySpellAiEffect.None,
            OrdinarySpellAiUsefulness.Classify(ability with { Usage = new(AlliesOnly: true, EnemiesOnly: true) },
                Unit, new(200, 200, 50, 70), enemy, false));
    }

    [Fact]
    public void Neutral_hp_preferences_reach_ready_formula_work_without_a_side_credit_or_penalty()
    {
        var (_, original, state) = OrdinarySpellAiReadyTests.Setup();
        var catalog = new OrdinarySpellAiCatalog(state.Definition, original.PrimaryByJob.Select(pair =>
            KeyValuePair.Create(pair.Key, pair.Value with
            {
                Slots = pair.Value.Slots.Select(slot => slot with
                {
                    Behavior = slot.Behavior with { TargetsEnemies = false, TargetsAllies = false }
                }).ToImmutableArray()
            })));
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = OrdinarySpellAiReadyQuery.Evaluate(state, catalog,
            new(OrdinarySpellAiReadyTests.Mage, 0, new(4, 2, 0), ImmutableHashSet<BattleUnitId>.Empty), new(57, 0));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(OrdinarySpellAiReadyDisposition.Scored, result.Disposition);
        Assert.Equal((short)49, result.Outcome!.Score!.Priority); // Neither side preference adds a credit or penalty.
        Assert.Equal((ushort)146, result.Outcome.Units[1].Vitals.Hp);
        Assert.Equal(4UL, result.RngState.DrawCount);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }
}
