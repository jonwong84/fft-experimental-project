using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

/// <summary>Managed cost representation beyond original byte records; not additional retail spell vectors.</summary>
public sealed class MagicMpCostTests
{
    [Theory]
    [InlineData(256, false, 256, 744)]
    [InlineData(513, false, 513, 487)]
    [InlineData(513, true, 256, 744)]
    public void Managed_costs_above_255_preserve_payment_and_half_mp_floor(int cost, bool half, int effective, int remaining)
    {
        var request = new MagicMpRequest(new(1000, half, false, false, false, false, MagicActionRestriction.None),
            new(16, cost, MagicResourceMenu.Default, true), MagicFormulaEvaluationMode.Execute, new(true));
        var r = MagicResources.EvaluateMp(request);
        Assert.True(r.Accepted);
        Assert.Equal(new MagicMpOutcome(MagicMpBlock.None, effective, (ushort)remaining, true), r.Outcome);
        Assert.Equal((ushort)1000, request.Actor.Mp);
    }

    [Theory]
    [InlineData(MagicFormulaEvaluationMode.Execute, 487)]
    [InlineData(MagicFormulaEvaluationMode.AiSimulation, 487)]
    [InlineData(MagicFormulaEvaluationMode.Preview, 1000)]
    public void Ready_cast_carries_an_integer_cost_once_across_multiple_targets(MagicFormulaEvaluationMode mode, int remaining)
    {
        var r = MagicCastTestData.Ready(mode);
        r = r with { Caster = r.Caster with { Mp = 1000 }, Definition = r.Definition with { MpCost = 513 } };
        var prepared = MagicCastQuery.PrepareReady(r);
        Assert.NotNull(prepared.Cast); Assert.Equal(513, prepared.Resource!.RecordedCost);
        var result = MagicCastQuery.ResolveTargets(prepared.Cast!,
            [r.Caster with { UnitId = new("first") }, r.Caster with { UnitId = new("second") }], new(true, true), new RandomState(57, 9));
        Assert.True(result.Accepted); Assert.Equal(513, result.Outcome!.RecordedCost);
        Assert.Equal((ushort)remaining, result.Outcome.CandidateMp); Assert.Equal(2, result.Outcome.Targets.Length);
        Assert.Equal((ushort)1000, r.Caster.Mp);
    }

    [Theory]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, int.MaxValue / 2)]
    public void Unaffordable_integer_costs_never_wrap_into_a_payable_value(bool half, int effective)
    {
        var r = MagicResources.EvaluateMp(new(new(1000, half, false, false, false, false, MagicActionRestriction.None),
            new(16, int.MaxValue, MagicResourceMenu.Default, true), MagicFormulaEvaluationMode.Execute, new(true)));
        Assert.True(r.Accepted);
        Assert.Equal(new MagicMpOutcome(MagicMpBlock.NotEnoughMp, effective, 1000, false), r.Outcome);
    }

    [Fact]
    public void Negative_costs_reject_at_both_query_entry_points()
    {
        var r = MagicCastTestData.Ready();
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicCastQuery.PrepareReady(r with { Definition = r.Definition with { MpCost = -1 } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicResources.EvaluateMp(new(
            new(1000, false, false, false, false, false, MagicActionRestriction.None),
            new(16, -1, MagicResourceMenu.Default, true), MagicFormulaEvaluationMode.Execute, new(true))));
    }
}
