using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class MagicConditionalProcTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","magic-proc-contract-v1.json")));
    private static readonly MagicConditionalProcProfile Known = new(true,true,true);
    private static RandomState BeforeRaw(uint raw,ulong count=17) => new(unchecked((((raw<<16)|0x1234u)-12345u)*4005161829u),count);
    private static FaithMagicElementAmount Projection(FaithMagicAmountKind kind,bool absorb=false,bool nullify=false,bool zeroFaith=false) =>
        FaithMagicAmountFormula.ProjectWithElementsAfterHit(new(kind,5,14,new(zeroFaith ? (byte)0 : (byte)70),new(70)),
            new(new ElementToggles { Fire = true },MagicDamageWeather.None,new(absorb ? new ElementToggles { Fire = true } : ElementToggles.None,nullify ? new ElementToggles { Fire = true } : ElementToggles.None),new(true,true,true)));

    [Fact]
    public void Six_scaled_roll_boundaries_use_real_RNG_outputs_and_not_modulo()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("rolls").EnumerateArray())
        {
            uint raw=row.GetProperty("raw").GetUInt32();var before=BeforeRaw(raw);
            var r=MagicConditionalProc.Evaluate(new(FaithMagicAmountKind.Damage,MagicProcInvocation.FamilyTail,MagicFormulaEvaluationMode.Execute),Known,before);
            Assert.True(r.Accepted);Assert.Equal((uint?)raw,r.Outcome!.Raw);Assert.Equal(row.GetProperty("percent").GetInt32(),r.Outcome.Percent);
            Assert.Equal(row.GetProperty("passed").GetBoolean(),r.Outcome.ShouldApplyStatus);Assert.Equal(r.Outcome.ShouldApplyStatus,r.Outcome.SetAddedEffectFlag);
            Assert.Equal(new RandomState((raw<<16)|0x1234u,before.DrawCount+1),r.RngState);count++;
        }
        Assert.Equal(6,count);
    }

    [Fact]
    public void Twenty_six_ordered_tapes_preserve_modes_invocation_context_and_atomic_counter_guards()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("decisions").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var p=d.GetProperty("profile");
            MagicConditionalProcProfile? profile=p.ValueKind==JsonValueKind.Null ? null : new(p.GetProperty("primary").GetBoolean(),p.GetProperty("noPending").GetBoolean(),p.GetProperty("clearFlag").GetBoolean());
            var request=new MagicConditionalProcRequest(Enum.Parse<FaithMagicAmountKind>(d.GetProperty("formula").GetString()!),Enum.Parse<MagicProcInvocation>(d.GetProperty("invocation").GetString()!),Enum.Parse<MagicFormulaEvaluationMode>(d.GetProperty("mode").GetString()!));
            var r=MagicConditionalProc.Evaluate(request,profile,new(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64()));
            Assert.Equal(e.GetProperty("accepted").GetBoolean(),r.Accepted);Assert.Equal(Enum.Parse<MagicProcRejection>(e.GetProperty("rejection").GetString()!),r.Rejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),r.RngState);
            if (!r.Accepted) Assert.Null(r.Outcome);
            else
            {
                var o=r.Outcome!;Assert.Equal(Enum.Parse<MagicProcDisposition>(e.GetProperty("disposition").GetString()!),o.Disposition);
                Assert.Equal(e.GetProperty("invoked").GetBoolean(),o.Invoked);Assert.Equal(e.GetProperty("applyStatus").GetBoolean(),o.ShouldApplyStatus);Assert.Equal(e.GetProperty("setFlag").GetBoolean(),o.SetAddedEffectFlag);
                Assert.Equal(e.GetProperty("raw").ValueKind==JsonValueKind.Null ? null : (uint?)e.GetProperty("raw").GetUInt32(),o.Raw);
                Assert.Equal(e.GetProperty("percent").ValueKind==JsonValueKind.Null ? null : (int?)e.GetProperty("percent").GetInt32(),o.Percent);
            }
            count++;
        }
        Assert.Equal(26,count);
    }

    [Theory] [InlineData(13,2,0)] [InlineData(8,3,0)] [InlineData(8,2,3)]
    public void Unsupported_family_entry_or_mode_is_not_assumed_ordinary(int family,int entry,int mode) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicConditionalProc.Evaluate(new((FaithMagicAmountKind)family,(MagicProcInvocation)entry,(MagicFormulaEvaluationMode)mode),Known,BeforeRaw(0)));

    [Fact]
    public void Healing_cannot_claim_a_damage_element_cancellation() =>
        Assert.Throws<ArgumentException>(() => MagicConditionalProc.Evaluate(new(FaithMagicAmountKind.Healing,MagicProcInvocation.ElementNullified,MagicFormulaEvaluationMode.Execute),null,new(1,ulong.MaxValue)));

    [Fact]
    public void Absorbed_damage_and_undead_healing_keep_their_original_formula_family()
    {
        var damage=Projection(FaithMagicAmountKind.Damage,absorb:true);Assert.Equal(FaithMagicHpRoute.Healing,FaithMagicHpRouting.Route(damage,true).Route);
        var rolled=MagicConditionalProc.EvaluateAfterHit(damage,MagicFormulaEvaluationMode.Execute,Known,BeforeRaw(0));
        Assert.True(rolled.Accepted);Assert.True(rolled.Outcome!.Invoked);Assert.True(rolled.Outcome.ShouldApplyStatus);Assert.Equal(18ul,rolled.RngState.DrawCount);
        var healing=Projection(FaithMagicAmountKind.Healing);Assert.Equal(FaithMagicHpRoute.Damage,FaithMagicHpRouting.Route(healing,true).Route);
        var before=new RandomState(1,ulong.MaxValue);var skipped=MagicConditionalProc.EvaluateAfterHit(healing,MagicFormulaEvaluationMode.Execute,null,before);
        Assert.True(skipped.Accepted);Assert.Equal(MagicProcDisposition.NoConditionalBranch,skipped.Outcome!.Disposition);Assert.Equal(before,skipped.RngState);
    }

    [Fact]
    public void Element_nullification_skips_the_decision_even_with_exhausted_counter_and_unknown_context()
    {
        var p=Projection(FaithMagicAmountKind.Damage,nullify:true);Assert.True(p.Nullified);Assert.Null(p.Amount);
        var before=new RandomState(1,ulong.MaxValue);var r=MagicConditionalProc.EvaluateAfterHit(p,MagicFormulaEvaluationMode.Execute,null,before);
        Assert.True(r.Accepted);Assert.Equal(MagicProcDisposition.ElementNullified,r.Outcome!.Disposition);Assert.False(r.Outcome.Invoked);Assert.Equal(before,r.RngState);
    }

    [Fact]
    public void A_zero_amount_does_not_optimize_away_the_actual_draw()
    {
        var p=Projection(FaithMagicAmountKind.Damage,zeroFaith:true);Assert.Equal((short)0,p.Amount!.Amount);
        var r=MagicConditionalProc.EvaluateAfterHit(p,MagicFormulaEvaluationMode.Execute,Known,BeforeRaw(0));
        Assert.True(r.Accepted);Assert.True(r.Outcome!.ShouldApplyStatus);Assert.True(r.Outcome.SetAddedEffectFlag);Assert.Equal(18ul,r.RngState.DrawCount);
    }

    [Fact]
    public void Missing_requests_or_projections_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => MagicConditionalProc.Evaluate(null!,null,BeforeRaw(0)));
        Assert.Throws<ArgumentNullException>(() => MagicConditionalProc.EvaluateAfterHit(null!,MagicFormulaEvaluationMode.Execute,null,BeforeRaw(0)));
    }
}
