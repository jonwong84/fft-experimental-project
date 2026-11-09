using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class MagicResultFinalizationTests
{
    private static readonly MagicResultFinalizationProfile Known = new(true,true,true,true,true,true,new(true,true,true));
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","magic-finalization-contract-v1.json")));
    private static MagicResultFrame Frame(JsonElement d)
    {
        var t=d.GetProperty("types");var e=d.GetProperty("effects");
        return new(d.GetProperty("hit").GetBoolean(),d.GetProperty("accuracy").GetUInt16(),(MagicResultMissKind)d.GetProperty("miss").GetByte(),
            d.GetProperty("damage").GetInt16(),d.GetProperty("healing").GetInt16(),
            new(t.GetProperty("damage").GetBoolean(),t.GetProperty("healing").GetBoolean(),t.GetProperty("pseudo").GetBoolean()),
            new(e.GetProperty("absorb").GetBoolean(),e.GetProperty("weak").GetBoolean(),e.GetProperty("added").GetBoolean()),d.GetProperty("clear").GetBoolean());
    }
    private static MagicResultFinalizationProfile Profile(JsonElement p)
    {
        var e=p.GetProperty("entry");
        return new(p.GetProperty("living").GetBoolean(),p.GetProperty("noBlocked").GetBoolean(),p.GetProperty("noGolem").GetBoolean(),
            p.GetProperty("emptyStatus").GetBoolean(),p.GetProperty("noKnockback").GetBoolean(),p.GetProperty("hpOnly").GetBoolean(),
            new(e.GetProperty("primary").GetBoolean(),e.GetProperty("noPending").GetBoolean(),e.GetProperty("clearFlag").GetBoolean()));
    }
    private static FaithMagicElementAmount Projection() => FaithMagicAmountFormula.ProjectWithElementsAfterHit(
        new(FaithMagicAmountKind.Damage,5,14,new(70),new(70)),new(new ElementToggles { Fire = true },MagicDamageWeather.None,new(),new(true,true,true)));

    [Fact]
    public void Twenty_literal_raw_frames_preserve_signed_fields_hit_labels_and_flags()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("raw").EnumerateArray())
        {
            var r=MagicResultFinalization.Evaluate(Frame(row.GetProperty("input")),Known);
            Assert.True(r.Accepted,row.GetProperty("id").GetString());Assert.Equal(MagicResultFinalizationRejection.None,r.Rejection);
            Assert.Equal(Frame(row.GetProperty("expected")),r.Outcome);count++;
        }
        Assert.Equal(20,count);
    }

    [Fact]
    public void Twenty_composed_descriptions_check_routing_proc_finalization_and_atomic_RNG()
    {
        using var doc=Fixture();int count=0;
        foreach(var row in doc.RootElement.GetProperty("composed").EnumerateArray())
        {
            var projection=FaithMagicAmountFormula.ProjectWithElementsAfterHit(MagicElementTestData.Input(row.GetProperty("input")),MagicElementTestData.Context(row.GetProperty("context")));
            var before=new RandomState(row.GetProperty("word").GetUInt32(),row.GetProperty("count").GetUInt64());
            var r=MagicResultFinalization.EvaluateAfterHit(projection,row.GetProperty("targetIsUndead").GetBoolean(),row.GetProperty("accuracy").GetUInt16(),
                Enum.Parse<MagicFormulaEvaluationMode>(row.GetProperty("mode").GetString()!),Profile(row.GetProperty("profile")),before);
            var e=row.GetProperty("expected");
            Assert.Equal(e.GetProperty("accepted").GetBoolean(),r.Accepted);
            Assert.Equal(Enum.Parse<MagicResultFinalizationRejection>(e.GetProperty("rejection").GetString()!),r.Finalization.Rejection);
            Assert.Equal(Enum.Parse<MagicProcRejection>(e.GetProperty("procRejection").GetString()!),r.ProcRejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),r.RngState);
            Assert.Equal(e.GetProperty("draws").GetUInt64(),r.RngState.DrawCount-before.DrawCount);
            if(r.Accepted)
            {
                Assert.Equal(Frame(e.GetProperty("outcome")),r.Finalization.Outcome);
                Assert.Equal(Enum.Parse<MagicProcDisposition>(e.GetProperty("procDisposition").GetString()!),r.Proc!.Disposition);
                Assert.Equal(e.GetProperty("setFlag").GetBoolean(),r.Proc.SetAddedEffectFlag);
            }
            else { Assert.Null(r.Finalization.Outcome);Assert.Null(r.Proc);Assert.Equal(before,r.RngState); }
            count++;
        }
        Assert.Equal(20,count);
    }

    [Theory]
    [InlineData("living")] [InlineData("blocked")] [InlineData("golem")] [InlineData("status")]
    [InlineData("knockback")] [InlineData("work")] [InlineData("entry")] [InlineData("primary")] [InlineData("pending")]
    public void Missing_positive_scope_facts_reject_both_paths_without_drawing(string fact)
    {
        var p=fact switch
        {
            "living" => Known with { LivingTarget=false }, "blocked" => Known with { NoPetrifyOrWall=false },
            "golem" => Known with { NoGolem=false }, "status" => Known with { EmptyStatusRecord=false },
            "knockback" => Known with { NoPendingKnockback=false }, "work" => Known with { OnlySupportedHpResultWork=false },
            "entry" => Known with { EntryProcContext=null }, "primary" => Known with { EntryProcContext=new(false,true,true) },
            _ => Known with { EntryProcContext=new(true,false,true) }
        };
        Assert.False(MagicResultFinalization.Evaluate(new(true,100,MagicResultMissKind.Hit,1001,0,new(true),new()),p).Accepted);
        var before=new RandomState(1,7);var r=MagicResultFinalization.EvaluateAfterHit(Projection(),false,100,MagicFormulaEvaluationMode.Execute,p,before);
        Assert.False(r.Accepted);Assert.Null(r.Finalization.Outcome);Assert.Null(r.Proc);Assert.Equal(before,r.RngState);
    }

    [Fact]
    public void Raw_known_post_proc_flags_need_no_clear_entry_but_preview_composition_does()
    {
        var p=Known with { EntryProcContext=new(true,true,false) };var before=new RandomState(1,ulong.MaxValue);
        var raw=MagicResultFinalization.Evaluate(new(true,100,MagicResultMissKind.Hit,10,0,new(true),new(AddedEffect:true)),p);
        Assert.True(raw.Accepted);Assert.True(raw.Outcome!.Types.PseudoStatus);
        Assert.True(MagicConditionalProc.EvaluateAfterHit(Projection(),MagicFormulaEvaluationMode.Preview,p.EntryProcContext,before).Accepted);
        var r=MagicResultFinalization.EvaluateAfterHit(Projection(),false,100,MagicFormulaEvaluationMode.Preview,p,before);
        Assert.False(r.Accepted);Assert.Equal(before,r.RngState);
    }

    [Fact]
    public void Known_miss_labels_are_preserved_as_data_without_implementing_miss_behaviors()
    {
        foreach(var label in Enum.GetValues<MagicResultMissKind>())
        {
            var frame=new MagicResultFrame(false,0,label,-32768,1000,new(false,true),new());
            var r=MagicResultFinalization.Evaluate(frame,Known);
            Assert.Equal(frame with { HpHealing=999 },r.Outcome);
        }
    }

    [Fact]
    public void Missing_context_is_unsupported_and_invalid_arguments_throw()
    {
        var frame=new MagicResultFrame(true,100,MagicResultMissKind.Hit,0,0,new(),new());
        Assert.False(MagicResultFinalization.Evaluate(frame,null).Accepted);
        Assert.False(MagicResultFinalization.EvaluateAfterHit(Projection(),false,100,MagicFormulaEvaluationMode.Execute,null,new(1,0)).Accepted);
        Assert.Throws<ArgumentNullException>(()=>MagicResultFinalization.Evaluate(null!,Known));
        Assert.Throws<ArgumentOutOfRangeException>(()=>MagicResultFinalization.Evaluate(frame with { Miss=(MagicResultMissKind)255 },Known));
        Assert.Throws<ArgumentNullException>(()=>MagicResultFinalization.EvaluateAfterHit(null!,false,100,MagicFormulaEvaluationMode.Execute,Known,new(1,0)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>MagicResultFinalization.EvaluateAfterHit(Projection(),false,100,(MagicFormulaEvaluationMode)255,Known,new(1,0)));
    }
}
