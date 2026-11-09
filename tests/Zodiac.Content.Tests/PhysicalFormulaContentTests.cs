using System.Text.Json.Nodes;
using Xunit;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Content.Samples;
using Zodiac.Framework.Formulas;

namespace Zodiac.Content.Tests;

public sealed class PhysicalFormulaContentTests
{
    private static TargetedAbilityContentPackage Package(int formula=50,int x=9,int y=0)
    {
        var metadata=SyntheticBasicSkillBattle.Metadata();
        return new("sample:package/custom",metadata.ProfileId,metadata.PackageId,
            [new(SyntheticBasicSkillBattle.ThrowStone,TargetedAbilityEffect.DashFlat,new AbilityFormula((AbilityFormulaId)formula,new(x,y)))]);
    }

    [Fact]
    public void Custom_formula_round_trip_preserves_identity_targeting_metadata_and_explicit_operands()
    {
        var metadata=SyntheticBasicSkillBattle.Metadata();var before=metadata.ToJson();var package=Package();
        package.ValidateAgainst(metadata);string json=package.ToJson();var read=TargetedAbilityContentPackage.ReadJson(json);
        Assert.Equal(json,read.ToJson());Assert.Equal(2,read.Version);Assert.Equal(before,metadata.ToJson());
        var ability=Assert.Single(read.Abilities);Assert.Equal(SyntheticBasicSkillBattle.ThrowStone,ability.Id);
        Assert.Equal(1,ability.Range);Assert.False(ability.Definition.Evadeable);Assert.False(ability.Definition.DirectTrajectory);
        Assert.Equal(AbilityFormulaId.PummelFormula,ability.Formula.Id);Assert.Equal(9,ability.Formula.Parameters.X);Assert.Equal(0,ability.Formula.Parameters.Y);
        Assert.Contains("\"ruleset\": \"custom\"",json);Assert.DoesNotContain("maximumDamageMultiplier",json);
    }

    [Fact]
    public void Formula_only_json_edit_retains_X_Y_instead_of_copying_another_ability()
    {
        var package=Package(55,0,4);var json=JsonNode.Parse(package.ToJson())!;
        json["abilities"]![0]!["formula"]=50;
        var changed=TargetedAbilityContentPackage.ReadJson(json.ToJsonString());var ability=changed.Abilities[0];
        Assert.Equal(0,ability.Formula.Parameters.X);Assert.Equal(4,ability.Formula.Parameters.Y);Assert.Equal(9,ability.Formula.CalculateDamage(5,16384));
        Assert.Equal(AbilityFormulaId.RandomPowerTimesPa,package.Abilities[0].Formula.Id);
    }

    [Theory]
    [InlineData(AbilityFormulaId.RandomPowerTimesPa)]
    [InlineData(AbilityFormulaId.PummelFormula)]
    public void Current_content_schemas_reject_Z_instead_of_losing_it_on_save(AbilityFormulaId id)
    {
        var metadata = SyntheticBasicSkillBattle.Metadata();
        var formula = new AbilityFormula(id, new(0, 0, 1));
        var ability = new TargetedAbilityDefinition(SyntheticBasicSkillBattle.ThrowStone,
            TargetedAbilityEffect.DashFlat, formula);

        Assert.Throws<ContentPackageValidationException>(() => new TargetedAbilityContentPackage(
            "sample:package/custom", metadata.ProfileId, metadata.PackageId, [ability]));
    }

    [Theory] [InlineData("formula","1")] [InlineData("x","256")] [InlineData("x","9.0")]
    [InlineData("y","-1")] [InlineData("range","4")] [InlineData("evadeable","true")]
    [InlineData("chargeTicks","1")] [InlineData("mpCost","1")]
    public void Unsupported_formula_or_unrepresented_delivery_edits_fail_closed(string field,string value)
    {
        var json=JsonNode.Parse(Package().ToJson())!;json["abilities"]![0]![field]=JsonNode.Parse(value);
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(json.ToJsonString()));
    }

    [Fact]
    public void Custom_schema_requires_explicit_ruleset_complete_parameters_and_safe_signed_damage_envelope()
    {
        foreach(string field in new[]{"ruleset","schemaVersion"})
        {
            var json=JsonNode.Parse(Package().ToJson())!;json.AsObject().Remove(field);
            Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(json.ToJsonString()));
        }
        foreach(string field in new[]{"formula","x","y","evadeable"})
        {
            var json=JsonNode.Parse(Package().ToJson())!;json["abilities"]![0]!.AsObject().Remove(field);
            Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(json.ToJsonString()));
        }
        var overflow=JsonNode.Parse(Package().ToJson())!;overflow["abilities"]![0]!["x"]=255;overflow["abilities"]![0]!["y"]=255;
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(overflow.ToJsonString()));
        var original=JsonNode.Parse(Package().ToJson())!;original["ruleset"]="original-fft";
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(original.ToJsonString()));
        var empty=JsonNode.Parse(Package().ToJson())!;empty["abilities"]=new JsonArray();
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson(empty.ToJsonString()));
        Assert.Throws<ContentPackageValidationException>(()=>TargetedAbilityContentPackage.ReadJson("[]"));
    }
}
