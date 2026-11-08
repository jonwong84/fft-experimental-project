using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Zodiac.Content;
using Zodiac.Content.BattleSetup;
using Zodiac.Framework;
using Zodiac.Framework.Ai;
using Xunit;

namespace Zodiac.AssetTool.Cli.Tests;

public sealed class DefaultAbilityAiPreparedTests
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
        var p=OriginalOrdinaryMagicImporter.DecodeSelection(Image(),OriginalDefaultAbilitySelection.ElementalTiers).Package;
        var catalog=PreparedDefaultAbilityAi.CreateCatalog(p);
        var state=OrdinaryMagicBattle.Apply(p.Definition.Start(),new OrdinaryMagicCommand.Advance()).State;
        var before=state.Turns.RngState;var original=p.ToJson();
        var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(new("mage"),id-16,new(4,2,0),[]),new(57,0));
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
        var p=OriginalOrdinaryMagicImporter.DecodeSelection(Image(),OriginalDefaultAbilitySelection.LearnOnHit).Package;
        var catalog=PreparedDefaultAbilityAi.CreateCatalog(p);var state=p.Definition.Start();
        void Apply(OrdinaryMagicCommand c){var r=OrdinaryMagicBattle.Apply(state,c);Assert.True(r.Accepted,r.Rejection);state=r.State;}
        Apply(new OrdinaryMagicCommand.Advance());Apply(new OrdinaryMagicCommand.Cast(new("mage"),new("fft.ps1:ability/source-0019"),new(4,2,0)));Apply(new OrdinaryMagicCommand.Wait(new("mage")));
        while(!state.Pending.IsEmpty)Apply(state.Turns.ActiveUnitId is{} active?new OrdinaryMagicCommand.Wait(active):new OrdinaryMagicCommand.Advance());
        var report=Assert.IsType<OrdinaryMagicLearningReport>(state.LearningReport);
        Apply(new OrdinaryMagicCommand.AnswerLearning(report.Id,report.LearnerId,true));
        while(state.Turns.ActiveUnitId!=new BattleUnitId("guard"))Apply(state.Turns.ActiveUnitId is{} active?new OrdinaryMagicCommand.Wait(active):new OrdinaryMagicCommand.Advance());
        var before=state.Turns.RngState;var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(new("guard"),3,new(0,2,0),[]),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal((short)11,r.Outcome!.Score!.Priority); // Synthetic100 growth leaves maxHP100.
        Assert.Equal((ushort)100,state.Units[0].Vitals.MaxHp);
        Assert.Equal((ushort)22,r.Outcome.Units[0].Vitals.Hp);Assert.Equal((ushort)27,r.Outcome.Units[1].Current.Mp);
        Assert.Equal(4UL,r.RngState.DrawCount);Assert.Equal(before,state.Turns.RngState);Assert.Null(state.LearningReport);
    }

    [Fact]
    public void Inspector_rejects_malformed_arguments_without_touching_assets()
    {
        using var output=new StringWriter();using var error=new StringWriter();
        Assert.Equal(1,new InspectDefaultAbilityAiCommand().Run(["inspect-magic-ai"],output,error));
        Assert.Contains("Usage",error.ToString());Assert.Equal("",output.ToString());
    }

    [Fact]
    public void Secondary_cure_grants_remain_playable_but_do_not_infer_an_ai_learned_row()
    {
        var p=OriginalOrdinaryMagicImporter.DecodeSelection(CureMagicImporterTests.Image(),OriginalDefaultAbilitySelection.CureAndFire).Package;
        Assert.Contains(new AssetId("fft.ps1:ability/source-0001"),p.Definition.Units[0].LearnedSpells);
        Assert.Contains("secondary",Assert.Throws<NotSupportedException>(()=>PreparedDefaultAbilityAi.CreateCatalog(p)).Message);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Prepared_only_inspection_is_repeatable_and_reports_source_timing_without_mutating_assets(bool delayed)
    {
        var p=OriginalOrdinaryMagicImporter.DecodeSelection(Image(),OriginalDefaultAbilitySelection.ElementalTiers).Package;
        WithPrepared(p,root=>
        {
            var before=Directory.GetFiles(root).ToDictionary(f=>Path.GetFileName(f)!,f=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
            string command=delayed?"inspect-delayed-magic-ai":"inspect-magic-ai";
            string Inspect()
            {
                using var output=new StringWriter();using var error=new StringWriter();
                Assert.Equal(0,new InspectDefaultAbilityAiCommand().Run([command,"--content-root",root],output,error));
                Assert.Equal("",error.ToString());return output.ToString();
            }
            string json=Inspect();Assert.Equal(json,Inspect());
            using var document=JsonDocument.Parse(json);var cases=document.RootElement.GetProperty("cases");
            Assert.Equal(delayed?24:12,cases.GetArrayLength());
            Assert.True(document.RootElement.GetProperty("eachCaseRestartsFromSameStateAndRng").GetBoolean());
            Assert.False(document.RootElement.GetProperty("cExecuted").GetBoolean());
            Assert.Equal(78,cases[0].GetProperty("Score").GetProperty("Priority").GetInt32());
            if(delayed)
            {
                Assert.Equal(13,cases[0].GetProperty("clock").GetProperty("Ticks").GetInt32());
                Assert.Equal(2,cases[0].GetProperty("formulaEntryRng").GetProperty("DrawCount").GetInt32());
                Assert.Equal("NotValuable",cases[1].GetProperty("disposition").GetString());
                Assert.Equal(JsonValueKind.Null,cases[1].GetProperty("Score").ValueKind);
                Assert.Equal(19,cases[6].GetProperty("clock").GetProperty("Ticks").GetInt32());
                Assert.Equal(10,cases[6].GetProperty("clock").GetProperty("units")[0].GetProperty("Ct").GetInt32());
            }
            Assert.Equal(before.Count,Directory.GetFiles(root).Length);
            foreach(var file in Directory.GetFiles(root))Assert.Equal(before[Path.GetFileName(file)],Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
        });
    }

    [Theory] [InlineData("inspect-magic-ai")] [InlineData("inspect-delayed-magic-ai")]
    public void Inspector_rejects_unrepresented_secondary_learning_without_partial_output(string command)
    {
        var p=OriginalOrdinaryMagicImporter.DecodeSelection(CureMagicImporterTests.Image(),OriginalDefaultAbilitySelection.CureAndFire).Package;
        WithPrepared(p,root=>
        {
            using var output=new StringWriter();using var error=new StringWriter();
            Assert.Equal(1,new InspectDefaultAbilityAiCommand().Run([command,"--content-root",root],output,error));
            Assert.Contains("secondary",error.ToString());Assert.Equal("",output.ToString());
        });
    }

    private static void WithPrepared(PreparedOrdinaryMagicBattle prepared,Action<string> action)
    {
        string root=Path.Combine(Path.GetTempPath(),"fft-ai-inspect-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var metadata=prepared.MetadataPackage!;
            byte[] package=Encoding.UTF8.GetBytes(prepared.ToJson()),companion=Encoding.UTF8.GetBytes(metadata.ToJson());
            File.WriteAllBytes(Path.Combine(root,"battle.json"),package);File.WriteAllBytes(Path.Combine(root,"metadata.json"),companion);
            new PreparedAssetManifest([new(PreparedOrdinaryMagicBattle.AssetId,"battle.json",package.Length,Convert.ToHexStringLower(SHA256.HashData(package))),
                new(new(metadata.PackageId),"metadata.json",companion.Length,Convert.ToHexStringLower(SHA256.HashData(companion)))])
                .SaveToFile(Path.Combine(root,PreparedAssetManifest.FileName));
            action(root);
        }
        finally
        {
            foreach(string name in new[]{"battle.json","metadata.json",PreparedAssetManifest.FileName})File.Delete(Path.Combine(root,name));
            Directory.Delete(root);
        }
    }
}
