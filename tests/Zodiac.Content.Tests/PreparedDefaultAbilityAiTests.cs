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
    private static PreparedOrdinaryMagicBattle Package(bool usable=true,byte unknown=0)
    {
        var d=AuthoredOrdinaryMagic.Document() with {SchemaVersion=2,MetadataPackageId="test:package/ai"};
        var metadata=new ContentPackage(d.MetadataPackageId!,"test:profile/ai",ContentPackageKind.Baseline,null,[],
            [new(new("test:skillset/magic"),[new(0,new(AuthoredOrdinaryMagic.Fire))])],
            [new(new(AuthoredOrdinaryMagic.Fire),50,90,15,new AiAbilityBehavior(TargetsEnemies:true,AffectsHp:true,AffectedByFaith:true),
                usable,aiEvadeWithMotion:true,unresolvedAiBitsRaw:unknown,unknownTypeBit4:true)]);
        return PreparedOrdinaryMagicBattle.Create(d,metadata);
    }

    [Fact]
    public void Bridge_uses_owned_metadata_behavior_and_executable_references_independently_of_serialized_class()
    {
        var p=Package();var sourceIds=SourceIds;var c=PreparedDefaultAbilityAi.CreateCatalog(p,sourceIds);
        var slot=Assert.Single(c.PrimaryByJob[new(AuthoredOrdinaryMagic.Wizard)].Slots);
        Assert.Same(p.AbilityMetadata[new(AuthoredOrdinaryMagic.Fire)].AiBehavior,slot.Behavior);
        Assert.Same(p.Definition.Spells[new(AuthoredOrdinaryMagic.Fire)],slot.Spell);
        Assert.True(slot.UsableByAi);Assert.False(slot.Behavior.Evadeable);Assert.True(slot.Spell.Spell.SecondaryEvadeable);
        Assert.True(p.AbilityMetadata[new(AuthoredOrdinaryMagic.Fire)].UnknownTypeBit4);
        sourceIds[new("test:skillset/magic")]=5;
        Assert.Equal((byte)11,c.PrimaryByJob[new(AuthoredOrdinaryMagic.Wizard)].OriginalId);
    }

    [Fact]
    public void Static_disabled_metadata_is_not_replaced_by_execution_or_motion_flags()
    {
        var p=Package(false);var c=PreparedDefaultAbilityAi.CreateCatalog(p,SourceIds);
        var state=OrdinaryMagicBattle.Apply(p.Definition.Start(),new OrdinaryMagicCommand.Advance()).State;
        var result=DefaultAbilityAiReadyQuery.Evaluate(state,c,new(new("mage"),0,new(4,2,0),[]),new(57,0));
        Assert.False(result.Accepted);Assert.Equal(0UL,result.RngState.DrawCount);
    }

    [Theory]
    [InlineData("legacy")] [InlineData("unknown-flags")] [InlineData("missing-map")] [InlineData("wrong-map")]
    public void Missing_or_untraced_metadata_cannot_authorize_ai(string change)
    {
        var p=change=="legacy"?PreparedOrdinaryMagicBattle.Create(AuthoredOrdinaryMagic.Document()):Package(unknown:change=="unknown-flags"?(byte)2:(byte)0);
        Assert.Throws<NotSupportedException>(()=>PreparedDefaultAbilityAi.CreateCatalog(p,change=="missing-map"?null:
            change=="wrong-map"?new Dictionary<SkillsetId,byte>{{new("test:skillset/magic"),5}}:SourceIds));
        Assert.Equal(2,p.Definition.Start().Units.Length); // Player execution remains loadable.
    }
}
