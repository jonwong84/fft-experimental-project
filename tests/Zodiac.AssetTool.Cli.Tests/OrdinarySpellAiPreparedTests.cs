using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Zodiac.Framework.Ai;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class OrdinarySpellAiPreparedTests
{
    internal static byte[] Image()
    {
        var image=ElementalMagicImporterTests.Image();
        for(int id=16;id<=27;id++)
        {
            int offset=checked((int)(0x8005ebf0u+(uint)id*8-0x80010000u+0x800));bool fourth=(id-16)%4==3;
            image[offset+2]=(byte)(fourth?30:90);image[offset+3]=(byte)(fourth?0x61:0x41);
            new byte[]{130,(byte)(fourth?21:85),2,128}.CopyTo(image,offset+4);
        }
        return image;
    }

    public static IEnumerable<object[]> Cases=>[
        [16,54,6,78],[17,70,12,84],[18,94,24,93],[19,125,48,97],
        [20,54,6,78],[21,70,10,86],[22,94,24,93],[23,125,48,97],
        [24,54,6,78],[25,70,12,84],[26,94,24,93],[27,125,48,97]];

    [Theory] [MemberData(nameof(Cases))]
    public void Every_prepared_elemental_tier_uses_metadata_shared_formula_and_fresh_mp_score(int id,int damage,int cost,int score)
    {
        var p=OriginalOrdinaryMagicImporter.DecodeSelection(Image(),OriginalOrdinarySpellSelection.ElementalTiers).Package;
        var catalog=PreparedOrdinarySpellAi.CreateCatalog(p);
        var state=OrdinaryMagicBattle.Apply(p.Definition.Start(),new OrdinaryMagicCommand.Advance()).State;
        var before=state.Turns.RngState;var original=p.ToJson();
        var r=OrdinarySpellAiReadyQuery.Evaluate(state,catalog,new(new("mage"),id-16,new(4,2,0),[]),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal(score,r.Outcome!.Score!.Priority);
        Assert.Equal((ushort)(200-damage),r.Outcome.Units[1].Vitals.Hp);Assert.Equal((ushort)(75-cost),r.Outcome.Units[0].Current.Mp);
        Assert.Equal(4UL,r.RngState.DrawCount);Assert.False(r.Outcome.Ability.Behavior.Evadeable);
        Assert.Equal((byte)10,state.Units[0].Level);Assert.Equal((byte)95,state.Units[0].Experience);
        Assert.Equal(before,state.Turns.RngState);Assert.Equal(original,p.ToJson());
        Assert.Null(state.LearningReport);Assert.Empty(state.Pending);
    }

    [Fact]
    public void Newly_learned_slot_becomes_an_ai_entry_without_another_learning_or_reward_pass()
    {
        var p=OriginalOrdinaryMagicImporter.DecodeSelection(Image(),OriginalOrdinarySpellSelection.LearnOnHit).Package;
        var catalog=PreparedOrdinarySpellAi.CreateCatalog(p);var state=p.Definition.Start();
        void Apply(OrdinaryMagicCommand c){var r=OrdinaryMagicBattle.Apply(state,c);Assert.True(r.Accepted,r.Rejection);state=r.State;}
        Apply(new OrdinaryMagicCommand.Advance());Apply(new OrdinaryMagicCommand.Cast(new("mage"),new("fft.ps1:ability/source-0019"),new(4,2,0)));Apply(new OrdinaryMagicCommand.Wait(new("mage")));
        while(!state.Pending.IsEmpty)Apply(state.Turns.ActiveUnitId is{} active?new OrdinaryMagicCommand.Wait(active):new OrdinaryMagicCommand.Advance());
        var report=Assert.IsType<OrdinaryMagicLearningReport>(state.LearningReport);
        Apply(new OrdinaryMagicCommand.AnswerLearning(report.Id,report.LearnerId,true));
        while(state.Turns.ActiveUnitId!=new BattleUnitId("guard"))Apply(state.Turns.ActiveUnitId is{} active?new OrdinaryMagicCommand.Wait(active):new OrdinaryMagicCommand.Advance());
        var before=state.Turns.RngState;var r=OrdinarySpellAiReadyQuery.Evaluate(state,catalog,new(new("guard"),3,new(0,2,0),[]),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal((short)11,r.Outcome!.Score!.Priority); // Synthetic100 growth leaves maxHP100.
        Assert.Equal((ushort)100,state.Units[0].Vitals.MaxHp);
        Assert.Equal((ushort)22,r.Outcome.Units[0].Vitals.Hp);Assert.Equal((ushort)27,r.Outcome.Units[1].Current.Mp);
        Assert.Equal(4UL,r.RngState.DrawCount);Assert.Equal(before,state.Turns.RngState);Assert.Null(state.LearningReport);
    }

    [Fact]
    public void Inspector_rejects_malformed_arguments_without_touching_assets()
    {
        using var output=new StringWriter();using var error=new StringWriter();
        Assert.Equal(1,new InspectOrdinarySpellAiCommand().Run(["inspect-magic-ai"],output,error));
        Assert.Contains("Usage",error.ToString());Assert.Equal("",output.ToString());
    }

    [Fact]
    public void Secondary_cure_grants_remain_playable_but_do_not_infer_an_ai_learned_row()
    {
        var p=OriginalOrdinaryMagicImporter.DecodeSelection(CureMagicImporterTests.Image(),OriginalOrdinarySpellSelection.CureAndFire).Package;
        Assert.Contains(new AssetId("fft.ps1:ability/source-0001"),p.Definition.Units[0].LearnedSpells);
        Assert.Contains("secondary",Assert.Throws<NotSupportedException>(()=>PreparedOrdinarySpellAi.CreateCatalog(p)).Message);
    }
}
