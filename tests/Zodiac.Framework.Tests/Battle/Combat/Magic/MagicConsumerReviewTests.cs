using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicConsumerReviewTests
{
    private static readonly MagicResultFinalizationProfile Known = new(true, true, true, true, true, true, new(true, true, true));
    private static readonly MagicEvadeEquipmentInput Bare = new(false, new(255, 0, false), new(255, 0, false), new(255, 0, false));
    private static MagicCurrentUnit Caster => new(new("caster"), 7, 90, new(MagicZodiacSign.Aries, IsMale: true),
        new(), new(MagicAttackUp: true, HalfMp: true), Strengthens: MagicElement.Fire, Equipment: Bare, Mp: 50);
    private static MagicCurrentUnit Target => new(new("target"), 5, 90, new(MagicZodiacSign.Leo, IsMale: true),
        new(Shell: true, Frog: true, Undead: true), new(MagicDefenseUp: true),
        Affinities: new(Absorb: MagicElement.Fire, Half: MagicElement.Fire, Weak: MagicElement.Fire), Equipment: Bare, Mp: 20);

    [Theory]
    [InlineData(MagicFormulaEvaluationMode.Execute)]
    [InlineData(MagicFormulaEvaluationMode.Preview)]
    [InlineData(MagicFormulaEvaluationMode.AiSimulation)]
    public void Damage_cast_preserves_each_modifier_store_then_weather_affinities_faith_and_absorption(MagicFormulaEvaluationMode mode)
    {
        // Source literals: XA 7 -> 8 -> 10 -> 6 -> 4 -> 6 -> 7; product77,
        // storm57, half28, weak56, Faith45. Combining modifiers would change the result.
        var cast = Prepare(FaithMagicAmountKind.Damage, mode);
        var before = new RandomState(57, 9);
        var result = MagicCastQuery.ResolveTargets(cast, [Target], new(true, true), before);
        Assert.True(result.Accepted);
        var target = Assert.Single(result.Outcome!.Targets);
        Assert.Equal((short)0, target.Result.HpDamage);
        Assert.Equal((short)45, target.Result.HpHealing);
        Assert.True(target.Result.Hit);
        Assert.Equal((ushort)100, target.Result.Accuracy);
        Assert.True(target.Result.Types.HpHealing);
        Assert.True(target.Result.Types.PseudoStatus);
        Assert.True(target.Result.Effects.Absorption);
        Assert.True(target.Result.Effects.Weakness);
        Assert.False(target.Result.ClearFirstCanceledStatusByte); // Damage absorption is not undead healing reversal.
        Assert.Equal(5, result.Outcome.RecordedCost);
        Assert.Equal((ushort)(mode == MagicFormulaEvaluationMode.Preview ? 50 : 45), result.Outcome.CandidateMp);
        Assert.Equal(before.DrawCount + (mode == MagicFormulaEvaluationMode.Execute ? 5UL : 4UL), result.RngState.DrawCount);
        Assert.Equal((ushort)50, Caster.Mp);
    }

    [Theory]
    [InlineData(MagicFormulaEvaluationMode.Execute)]
    [InlineData(MagicFormulaEvaluationMode.Preview)]
    [InlineData(MagicFormulaEvaluationMode.AiSimulation)]
    public void Healing_cast_bypasses_target_defenses_weather_affinities_and_evasion_before_undead_reversal(MagicFormulaEvaluationMode mode)
    {
        // Same current facts: XA 7 -> 8 -> 10 -> 12; product132, Faith106.
        // Even an evadeable healing definition does not enter magic evasion or the damage proc tail.
        var cast = Prepare(FaithMagicAmountKind.Healing, mode);
        var before = new RandomState(57, ulong.MaxValue);
        var result = MagicCastQuery.ResolveTargets(cast, [Target], new(true, true), before);
        Assert.True(result.Accepted);
        var target = Assert.Single(result.Outcome!.Targets);
        Assert.Equal(new MagicResultFrame(true, 100, MagicResultMissKind.Hit, 106, 0,
            new(true), new(), true), target.Result);
        Assert.Null(target.Evasion);
        Assert.Equal(MagicProcDisposition.NoConditionalBranch, target.Proc!.Disposition);
        Assert.Equal(before, result.RngState);
        Assert.Equal(5, result.Outcome.RecordedCost);
        Assert.Equal((ushort)(mode == MagicFormulaEvaluationMode.Preview ? 50 : 45), result.Outcome.CandidateMp);
    }

    [Fact]
    public void Nullified_first_target_does_not_leak_flags_or_skip_the_next_targets_formula_draws()
    {
        var first = Target with { Affinities = Target.Affinities with { Nullify = MagicElement.Fire } };
        var second = Target with { UnitId = new("second"), Affinities = default };
        var result = MagicCastQuery.ResolveTargets(Prepare(FaithMagicAmountKind.Damage, MagicFormulaEvaluationMode.Execute),
            [first, second], new(true, true), new(57, 0));
        Assert.True(result.Accepted);
        Assert.Equal(new MagicResultFrame(false, 0, MagicResultMissKind.Nullified, 0, 0, new(), new()), result.Outcome!.Targets[0].Result);
        var next = result.Outcome.Targets[1].Result;
        Assert.True(next.Hit);
        Assert.Equal((short)46, next.HpDamage);
        Assert.Equal((short)0, next.HpHealing);
        Assert.False(next.Effects.Absorption);
        Assert.False(next.Effects.Weakness);
        Assert.Equal(9UL, result.RngState.DrawCount); // Four evades, then four evades and the second target's proc.
        Assert.Equal((ushort)45, result.Outcome.CandidateMp);
    }

    [Fact]
    public void Rounded_zero_accuracy_keeps_the_ai_formula_tail_but_cannot_apply_hp()
    {
        var caster = Caster with { MagicAttack = 5, Faith = 70, Supports = new(), Strengthens = MagicElement.None };
        var target = Target with { Faith = 70, Identity = caster.Identity, Statuses = new(), Supports = new(),
            Affinities = new(Weak: MagicElement.Fire), Equipment = new(false, new(1, 90, true), new(2, 90, true), new(3, 90, true)) };
        var definition = new MagicCastDefinition(new("test:fire"), 16, new(FaithMagicAmountKind.Damage, 14, MagicElement.Fire, true), 6, 0, true);
        var cast = MagicCastQuery.PrepareReady(new(caster, definition, MagicDamageWeather.None,
            MagicFormulaEvaluationMode.AiSimulation, Known, new(true, true, true, true, true))).Cast!;
        var result = MagicCastQuery.ResolveTargets(cast, [target], new(true, true), new(57, 0));
        Assert.True(result.Accepted);
        var frame = Assert.Single(result.Outcome!.Targets).Result;
        Assert.False(frame.Hit);
        Assert.Equal((ushort)0, frame.Accuracy);
        Assert.Equal((short)68, frame.HpDamage);
        Assert.True(frame.Effects.Weakness);
        Assert.True(frame.Types.PseudoStatus);
        Assert.Equal(4UL, result.RngState.DrawCount);
        var vitals = new MagicUnitVitals(100, 100, 50, 70, false);
        var applied = DefaultAbilityHitApplication.Project(target, vitals, frame);
        Assert.Equal(target, applied.Current);
        Assert.Equal(vitals, applied.Vitals);
    }

    private static PreparedMagicCast Prepare(FaithMagicAmountKind kind, MagicFormulaEvaluationMode mode)
    {
        var definition = new MagicCastDefinition(new("test:faith"), (short)(kind == FaithMagicAmountKind.Damage ? 16 : 1),
            new(kind, 11, MagicElement.Fire, true), 11, 0, true);
        var prepared = MagicCastQuery.PrepareReady(new(Caster, definition, MagicDamageWeather.Storm, mode,
            Known, new(true, true, true, true, true)));
        Assert.True(prepared.Accepted);
        return Assert.IsType<PreparedMagicCast>(prepared.Cast);
    }
}
