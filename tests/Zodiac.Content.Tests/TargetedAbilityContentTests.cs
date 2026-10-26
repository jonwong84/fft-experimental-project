using System.Text.Json.Nodes;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class TargetedAbilityContentTests
{
    private static ContentPackage Metadata() => SyntheticBasicSkillBattle.Metadata();
    private static TargetedAbilityContentPackage Package() => SyntheticBasicSkillBattle.TargetedEffects(Metadata());

    [Fact]
    public void Companion_round_trip_is_sorted_independent_and_does_not_modify_learning_metadata()
    {
        var metadata = Metadata(); string before = metadata.ToJson();
        var list = Package().Abilities.ToList();
        var companion = new TargetedAbilityContentPackage("sample:package/targeted",metadata.ProfileId,metadata.PackageId,list);
        list.Clear(); companion.ValidateAgainst(metadata);
        string json = companion.ToJson();
        Assert.Equal(json,TargetedAbilityContentPackage.ReadJson(json).ToJson());
        Assert.Equal(before,metadata.ToJson()); Assert.Single(companion.Abilities);
        Assert.DoesNotContain("\r",json); Assert.EndsWith("\n",json);
        var z = new TargetedAbilityDefinition(new("sample:ability/z"),TargetedAbilityEffect.ThrowStoneFlat,4,2,0,0);
        var a = new TargetedAbilityDefinition(new("sample:ability/a"),TargetedAbilityEffect.ThrowStoneFlat,4,2,0,0);
        var ordered = new TargetedAbilityContentPackage("sample:package/order",metadata.ProfileId,metadata.PackageId,[z,a]).ToJson();
        Assert.True(ordered.IndexOf(a.Id.Value,StringComparison.Ordinal)<ordered.IndexOf(z.Id.Value,StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("format")] [InlineData("schemaVersion")] [InlineData("packageId")] [InlineData("profileId")]
    [InlineData("requiredMetadataPackageId")] [InlineData("abilities")]
    public void Every_root_field_is_required(string field)
    {
        var json=JsonNode.Parse(Package().ToJson())!; json.AsObject().Remove(field);
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(json.ToJsonString()));
    }

    [Theory] [InlineData("id")] [InlineData("effect")] [InlineData("target")] [InlineData("range")]
    [InlineData("maximumDamageMultiplier")] [InlineData("chargeTicks")] [InlineData("mpCost")]
    public void Every_effect_field_is_required(string field)
    {
        var json=JsonNode.Parse(Package().ToJson())!; json["abilities"]![0]!.AsObject().Remove(field);
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(json.ToJsonString()));
    }

    [Theory] [InlineData("effect","unknown")] [InlineData("target","self")] [InlineData("range","3")]
    [InlineData("maximumDamageMultiplier","4")] [InlineData("chargeTicks","1")] [InlineData("mpCost","1")]
    [InlineData("range","4.0")] [InlineData("range","\"4\"")] [InlineData("id","Other:ability/bad")]
    public void Unsupported_semantics_types_and_namespaces_fail_closed(string field,string value)
    {
        var json=JsonNode.Parse(Package().ToJson())!; var item=json["abilities"]![0]!;
        item[field]=field is "id" or "target" or "effect" ? JsonValue.Create(value) : JsonNode.Parse(value);
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(json.ToJsonString()));
    }

    [Theory] [InlineData("duplicate-field")] [InlineData("unknown-root")] [InlineData("unknown-effect")]
    [InlineData("duplicate-id")] [InlineData("namespace")] [InlineData("version")] [InlineData("format")]
    public void Strict_structure_and_identity_checks(string kind)
    {
        string text=Package().ToJson();
        if(kind=="duplicate-field") text=text.Replace("\"range\": 4","\"range\": 4, \"range\": 4",StringComparison.Ordinal);
        else
        {
            var json=JsonNode.Parse(text)!;
            switch(kind)
            {
                case "unknown-root": json["extra"]=true; break;
                case "unknown-effect": json["abilities"]![0]!["extra"]=true; break;
                case "duplicate-id": json["abilities"]!.AsArray().Add(json["abilities"]![0]!.DeepClone()); break;
                case "namespace": json["abilities"]![0]!["id"]="other:ability/stone"; break;
                case "version": json["schemaVersion"]=2; break;
                case "format": json["format"]="fft.immediate-ability-content"; break;
            }
            text=json.ToJsonString();
        }
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(text));
    }

    [Theory] [InlineData("dependency")] [InlineData("profile")] [InlineData("missing")] [InlineData("class")]
    public void Companion_requires_matching_ordinary_learning_metadata(string kind)
    {
        var original=Metadata(); var p=Package();
        var metadata=kind=="missing"?new ContentPackage(original.PackageId,original.ProfileId,ContentPackageKind.Baseline,null,[],[],[]):
            kind=="class"?new ContentPackage(original.PackageId,original.ProfileId,ContentPackageKind.Baseline,null,original.Jobs,original.Skillsets,
                original.Abilities.Select(a=>a.Id==SyntheticBasicSkillBattle.ThrowStone?new AbilityDefinition(a.Id,0,0,7,new byte[4]):a)):original;
        var selected=new TargetedAbilityContentPackage(p.PackageId,kind=="profile"?"sample:profile/wrong":p.ProfileId,
            kind=="dependency"?"sample:package/wrong":p.RequiredMetadataPackageId,p.Abilities);
        Assert.Throws<ContentPackageValidationException>(()=>selected.ValidateAgainst(metadata));
    }

    [Theory] [InlineData("effect")] [InlineData("range")] [InlineData("maximum")] [InlineData("charge")] [InlineData("mp")]
    public void Typed_definition_does_not_admit_unimplemented_rules(string field) =>
        Assert.Throws<NotSupportedException>(()=>new TargetedAbilityDefinition(SyntheticBasicSkillBattle.ThrowStone,
            field=="effect"?(TargetedAbilityEffect)99:TargetedAbilityEffect.ThrowStoneFlat,
            field=="range"?5:4,field=="maximum"?3:2,field=="charge"?1:0,field=="mp"?1:0));

    [Fact]
    public void Json_and_typed_collection_budgets_reject_before_projection()
    {
        var p=Package();
        foreach(string? input in new[]{null,"{",new string(' ',ContentPackage.MaxJsonBytes+1)})
            Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(input!));
        Assert.Throws<ContentPackageValidationException>(()=>new TargetedAbilityContentPackage(p.PackageId,p.ProfileId,p.RequiredMetadataPackageId,null!));
        Assert.Throws<ContentPackageValidationException>(()=>new TargetedAbilityContentPackage(p.PackageId,p.ProfileId,p.RequiredMetadataPackageId,[null!]));
        Assert.Throws<ContentPackageValidationException>(()=>new TargetedAbilityContentPackage(p.RequiredMetadataPackageId,p.ProfileId,p.RequiredMetadataPackageId,p.Abilities));
        Assert.Throws<ContentPackageValidationException>(()=>new TargetedAbilityContentPackage(p.PackageId,p.ProfileId,p.RequiredMetadataPackageId,
            Enumerable.Range(0,ContentPackage.MaxDefinitionsPerKind+1).Select(i=>new TargetedAbilityDefinition(
                new($"sample:ability/limit-{i}"),TargetedAbilityEffect.ThrowStoneFlat,4,2,0,0))));
    }

    [Theory] [InlineData("unit")] [InlineData("slot")] [InlineData("effect")] [InlineData("duplicate")]
    [InlineData("instances")]
    public void Grants_must_bind_the_selected_metadata_instances_and_action_slot(string kind)
    {
        var metadata=Metadata(); var setup=Setup(kind=="instances"?Metadata():metadata);
        var grant=new LearnedTargetedAbilityGrant(kind=="unit"?new("sample:unit/missing"):SyntheticBasicSkillBattle.First,
            kind=="slot"?0:2,kind=="effect"?SyntheticBasicSkillBattle.Accumulate:SyntheticBasicSkillBattle.ThrowStone);
        Assert.ThrowsAny<ArgumentException>(()=>TargetedAbilityBattleSetup.CreateProfile(metadata,Package(),setup,
            kind=="duplicate"?[grant,grant]:[grant],SyntheticBasicSkillBattle.Ground,0));
    }

    [Fact]
    public void Nonzero_equipment_bonus_rejects_and_large_authored_slot_does_not_inherit_retail_capacity()
    {
        var metadata=Metadata(); var setup=Setup(metadata);
        Assert.Throws<NotSupportedException>(()=>TargetedAbilityBattleSetup.CreateProfile(metadata,Package(),setup,[],SyntheticBasicSkillBattle.Ground,1));
        var extended=new ContentPackage(metadata.PackageId,metadata.ProfileId,ContentPackageKind.Baseline,null,metadata.Jobs,
            [new(metadata.Skillsets[0].Id,[new(0,SyntheticBasicSkillBattle.Accumulate),new(2048,SyntheticBasicSkillBattle.ThrowStone)])],metadata.Abilities);
        var profile=TargetedAbilityBattleSetup.CreateProfile(extended,SyntheticBasicSkillBattle.TargetedEffects(extended),Setup(extended),
            [new(SyntheticBasicSkillBattle.First,2048,SyntheticBasicSkillBattle.ThrowStone)],SyntheticBasicSkillBattle.Ground,0);
        Assert.Contains(new AssetId(SyntheticBasicSkillBattle.ThrowStone.Value),profile.Units[SyntheticBasicSkillBattle.First].Abilities);
        Assert.Empty(profile.Units[SyntheticBasicSkillBattle.Second].Abilities);
    }

    private static BaseJobBattleSetup Setup(ContentPackage metadata)
    {
        var ground=SyntheticBasicSkillBattle.Ground;
        var map=new BattleMapDefinition(7,5,1,Enumerable.Repeat(new BattleTile(ground,10),35).ToArray());
        return BaseJobBattleSetup.Create(new(metadata.Jobs,metadata.Skillsets,metadata.Abilities),map,
            [new(SyntheticBasicSkillBattle.First,metadata.Jobs[0].Id,new(1,2,0),6,[new(ground,new(1))]),
             new(SyntheticBasicSkillBattle.Second,metadata.Jobs[0].Id,new(3,2,0),6,[new(ground,new(1))])],FftBiosRandom.Seed(1));
    }
}
