using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiUsefulnessTests
{
    private static DefaultAbilityAiConsidered Ability => DefaultAbilityAiParameters.Load(DefaultAbilityAiParameterTests.Entry(), new(), new(Usable: true));
    internal static MagicCurrentUnit Unit => new(new("target"), 5, 70, new(MagicZodiacSign.Aries, IsMale: true), new(), new(),
        Equipment: DefaultAbilityBattleDefinition.BareEquipment, Mp: 50);

    [Theory]
    [InlineData(true, DefaultAbilityAiEffect.Useful)]
    [InlineData(false, DefaultAbilityAiEffect.Adverse)]
    public void Damage_preference_is_based_on_side_even_with_zero_faith(bool enemy, DefaultAbilityAiEffect expected)
    {
        Assert.Equal(expected, DefaultAbilityAiUsefulness.Classify(Ability, Unit with { Faith = 0 }, new(100, 200, 50, 70), enemy, false));
    }

    [Theory]
    [InlineData(49, DefaultAbilityAiEffect.Useful)]
    [InlineData(50, DefaultAbilityAiEffect.None)]
    [InlineData(51, DefaultAbilityAiEffect.None)]
    public void Healing_uses_hp_less_than_floor_half_not_half_of_a_percentage(ushort hp, DefaultAbilityAiEffect expected)
    {
        var a = Ability with { Behavior = Ability.Behavior with { TargetsEnemies = false, TargetsAllies = true, MagicalAttack = false } };
        Assert.Equal(expected, DefaultAbilityAiUsefulness.Classify(a, Unit, new(hp, 101, 50, 70), false, false));
        Assert.Equal(DefaultAbilityAiEffect.Adverse, DefaultAbilityAiUsefulness.Classify(a, Unit, new(hp, 101, 50, 70), true, false));
    }

    [Fact]
    public void Usage_side_restrictions_dead_and_caster_exclusion_precede_preferences()
    {
        Assert.Equal(DefaultAbilityAiEffect.None, DefaultAbilityAiUsefulness.Classify(Ability with { Usage = new(EnemiesOnly: true) }, Unit, new(100,200,50,70), false, false));
        Assert.Equal(DefaultAbilityAiEffect.None, DefaultAbilityAiUsefulness.Classify(Ability with { Geometry = Ability.Geometry with { CannotHitCaster = true } }, Unit, new(100,200,50,70), false, true));
        Assert.Equal(DefaultAbilityAiEffect.None, DefaultAbilityAiUsefulness.Classify(Ability, Unit with { Statuses = new(Dead: true) }, new(0,200,50,70), true, false));
        Assert.Equal(DefaultAbilityAiEffect.Adverse, DefaultAbilityAiUsefulness.Classify(Ability with { Behavior = Ability.Behavior with { TargetsAllies = true } }, Unit, new(10,200,50,70), false, false));
    }

    [Fact]
    public void Untraced_flags_and_status_combinations_are_rejected()
    {
        Assert.Throws<NotSupportedException>(() => DefaultAbilityAiUsefulness.Classify(Ability with { Behavior = Ability.Behavior with { RandomUse = true } }, Unit, new(100,200,50,70), true, false));
        Assert.Throws<NotSupportedException>(() => DefaultAbilityAiUsefulness.Classify(Ability, Unit with { Statuses = new(Sleep: true) }, new(100,200,50,70), true, false));
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

        Assert.Equal(DefaultAbilityAiEffect.Useful,
            DefaultAbilityAiUsefulness.Classify(ability, Unit, new(200, 200, 50, 70), enemy, false));
        Assert.Equal(DefaultAbilityAiEffect.None,
            DefaultAbilityAiUsefulness.Classify(ability with { Usage = new(AlliesOnly: true, EnemiesOnly: true) },
                Unit, new(200, 200, 50, 70), enemy, false));
    }

    [Fact]
    public void Neutral_hp_preferences_reach_ready_formula_work_without_a_side_credit_or_penalty()
    {
        var (_, original, state) = DefaultAbilityAiReadyTests.Setup();
        var catalog = new DefaultAbilityAiCatalog(state.Definition, original.PrimaryByJob.Select(pair =>
            KeyValuePair.Create(pair.Key, pair.Value with
            {
                Slots = pair.Value.Slots.Select(slot => slot with
                {
                    Behavior = slot.Behavior with { TargetsEnemies = false, TargetsAllies = false }
                }).ToImmutableArray()
            })));
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = DefaultAbilityAiReadyQuery.Evaluate(state, catalog,
            new(DefaultAbilityAiReadyTests.Mage, 0, new(4, 2, 0), ImmutableHashSet<BattleUnitId>.Empty), new(57, 0));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(DefaultAbilityAiReadyDisposition.Scored, result.Disposition);
        Assert.Equal((short)49, result.Outcome!.Score!.Priority); // Neither side preference adds a credit or penalty.
        Assert.Equal((ushort)146, result.Outcome.Units[1].Vitals.Hp);
        Assert.Equal(4UL, result.RngState.DrawCount);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }
}
