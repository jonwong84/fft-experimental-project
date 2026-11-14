using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;
using Zodiac.Framework.Ai;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class PreparedDefaultAbilityAiTests
{
    private static Dictionary<SkillsetId,byte> SourceIds=>new(){{new("test:skillset/magic"),11}};
    private static PreparedDefaultAbilityBattle Package(bool usable=true,byte unknown=0)
    {
        var d=AuthoredDefaultAbility.Document() with {SchemaVersion=2,MetadataPackageId="test:package/ai"};
        var metadata=new ContentPackage(d.MetadataPackageId!,"test:profile/ai",ContentPackageKind.Baseline,null,[],
            [new(new("test:skillset/magic"),[new(0,new(AuthoredDefaultAbility.Fire))])],
            [new(new(AuthoredDefaultAbility.Fire),50,90,15,new AiAbilityBehavior(TargetsEnemies:true,AffectsHp:true,AffectedByFaith:true),
                usable,aiEvadeWithMotion:true,unresolvedAiBitsRaw:unknown,unknownTypeBit4:true)]);
        return PreparedDefaultAbilityBattle.Create(d,metadata);
    }

    [Fact]
    public void Bridge_uses_owned_metadata_behavior_and_executable_references_independently_of_serialized_class()
    {
        var p=Package();var sourceIds=SourceIds;var c=PreparedDefaultAbilityAi.CreateCatalog(p,sourceIds);
        var slot=Assert.Single(c.PrimaryByJob[new(AuthoredDefaultAbility.Wizard)].Slots);
        Assert.Same(p.AbilityMetadata[new(AuthoredDefaultAbility.Fire)].AiBehavior,slot.Behavior);
        Assert.Same(p.Definition.Spells[new(AuthoredDefaultAbility.Fire)],slot.Spell);
        Assert.True(slot.UsableByAi);Assert.False(slot.Behavior.Evadeable);Assert.True(slot.Spell.Spell.SecondaryEvadeable);
        Assert.True(p.AbilityMetadata[new(AuthoredDefaultAbility.Fire)].UnknownTypeBit4);
        sourceIds[new("test:skillset/magic")]=5;
        Assert.Equal((byte)11,c.PrimaryByJob[new(AuthoredDefaultAbility.Wizard)].OriginalId);
    }

    [Fact]
    public void Static_disabled_metadata_is_not_replaced_by_execution_or_motion_flags()
    {
        var p=Package(false);var c=PreparedDefaultAbilityAi.CreateCatalog(p,SourceIds);
        var state=DefaultAbilityBattle.Apply(p.Definition.Start(),new DefaultAbilityCommand.Advance()).State;
        var result=DefaultAbilityAiReadyQuery.Evaluate(state,c,new(new("mage"),0,new(4,2,0),[]),new(57,0));
        Assert.False(result.Accepted);Assert.Equal(0UL,result.RngState.DrawCount);
    }

    [Theory]
    [InlineData("legacy")] [InlineData("unknown-flags")] [InlineData("missing-map")] [InlineData("wrong-map")]
    public void Missing_or_untraced_metadata_cannot_authorize_ai(string change)
    {
        var p=change=="legacy"?PreparedDefaultAbilityBattle.Create(AuthoredDefaultAbility.Document()):Package(unknown:change=="unknown-flags"?(byte)2:(byte)0);
        Assert.Throws<NotSupportedException>(()=>PreparedDefaultAbilityAi.CreateCatalog(p,change=="missing-map"?null:
            change=="wrong-map"?new Dictionary<SkillsetId,byte>{{new("test:skillset/magic"),5}}:SourceIds));
        Assert.Equal(2,p.Definition.Start().Units.Length); // Player execution remains loadable.
    }
}
