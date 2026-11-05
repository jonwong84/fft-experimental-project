using Xunit;

namespace Zodiac.Framework.Tests;

internal static class MagicCastTestData
{
    internal static MagicReadyCastRequest Ready(MagicFormulaEvaluationMode mode = MagicFormulaEvaluationMode.Execute)
    {
        using var doc = MagicCurrentTestData.Fixture(); var r = MagicCurrentTestData.Request(doc.RootElement.GetProperty("queries")[0]);
        return new(r.Caster, new(new("fft:ability/test-fire"), 16, r.Spell, 9, 9, true), r.Weather, mode,
            r.Profile, new(true, true, true, true, true));
    }
}

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicCastQueryTests
{
    [Theory] [InlineData(MagicFormulaEvaluationMode.Execute, 21)] [InlineData(MagicFormulaEvaluationMode.AiSimulation, 21)]
    [InlineData(MagicFormulaEvaluationMode.Preview, 30)]
    public void Certified_ready_cast_prepares_one_mp_proposal_and_keeps_the_snapshot(MagicFormulaEvaluationMode mode, int candidate)
    {
        var request = MagicCastTestData.Ready(mode); var copy = request with { };
        Assert.Equal((byte)9, MagicCharge.CalculateSetup(request.Definition.RawChargeTime, false, false, false).Ticks);
        var result = MagicCastQuery.PrepareReady(request);
        Assert.True(result.Accepted); Assert.True(result.Resource!.Usable); Assert.NotNull(result.Cast);
        Assert.Equal((ushort)candidate, result.Resource.CandidateMp); Assert.Equal((byte)9, result.Resource.RecordedCost);
        Assert.Equal((ushort)30, result.Cast!.Caster.Mp); Assert.Equal(copy, request);
    }

    [Fact]
    public void Scheduling_and_uncertified_ready_context_never_propose_an_mp_subtraction()
    {
        var r = MagicCastTestData.Ready();
        MagicReadyCastProfile?[] profiles = [null, new(false, true, true, true, true), new(true, false, true, true, true),
            new(true, true, false, true, true), new(true, true, true, false, true), new(true, true, true, true, false)];
        foreach (var p in profiles)
        {
            var result = MagicCastQuery.PrepareReady(r with { ReadyProfile = p });
            Assert.False(result.Accepted); Assert.Null(result.Resource); Assert.Null(result.Cast);
        }
        Assert.False(MagicCastQuery.PrepareReady(r with { Caster = r.Caster with { IsMime = true } }).Accepted);
        Assert.False(MagicCastQuery.PrepareReady(r with { Caster = r.Caster with { Statuses = new(Charging: true) } }).Accepted);
    }

    [Fact]
    public void Source_mp_block_preserves_recorded_cost_without_a_prepared_cast()
    {
        var r = MagicCastTestData.Ready();
        var result = MagicCastQuery.PrepareReady(r with { Caster = r.Caster with { Mp = 8 } });
        Assert.True(result.Accepted); Assert.Null(result.Cast);
        Assert.Equal(new MagicMpOutcome(MagicMpBlock.NotEnoughMp, 9, 8, false), result.Resource);
        result = MagicCastQuery.PrepareReady(r with { Caster = r.Caster with { Statuses = new(Silence: true) } });
        Assert.Equal(new MagicMpOutcome(MagicMpBlock.Silenced, 0, 30, false), result.Resource); Assert.Null(result.Cast);
    }

    [Fact]
    public void Invalid_ready_cast_inputs_throw_without_a_proposal()
    {
        var r = MagicCastTestData.Ready();
        Assert.Throws<ArgumentNullException>(() => MagicCastQuery.PrepareReady(null!));
        Assert.Throws<ArgumentException>(() => MagicCastQuery.PrepareReady(r with { Definition = r.Definition with { Id = default } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicCastQuery.PrepareReady(r with { Mode = (MagicFormulaEvaluationMode)255 }));
    }
}
