using Xunit;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Zodiac.Framework.Formulas;
using Zodiac.Framework.Random;

namespace Zodiac.Content.Tests;

public sealed class PhysicalDeliveryContentTests
{
    [Theory]
    [InlineData(PhysicalAbilityDelivery.AdjacentUnit)]
    [InlineData(PhysicalAbilityDelivery.DirectProjectile)]
    public void New_ability_records_bind_by_delivery_and_preserve_a_single_definition_through_compatibility_views(
        PhysicalAbilityDelivery delivery)
    {
        var original = SyntheticBasicSkillBattle.Metadata();
        var first = new AbilityId("sample:ability/new-physical");
        var second = new AbilityId("sample:ability/another-physical");
        var metadata = new ContentPackage(original.PackageId, original.ProfileId, ContentPackageKind.Baseline, null,
            original.Jobs, [new(original.Skillsets[0].Id, [new(17, first), new(2048, second)])],
            [new(first, 50, 100, 1, new byte[4]), new(second, 60, 100, 1, new byte[4])]);
        var formula = new PhysicalDamageFormula(PhysicalDamageFormulaId.RandomPaPlusHalfPa, 9, 0);
        var content = new TargetedAbilityContentPackage("sample:package/physical", metadata.ProfileId, metadata.PackageId,
            [new(first, delivery, formula), new(second, delivery, new(PhysicalDamageFormulaId.RandomPa, 0, 4))]);
        string json = content.ToJson();
        var read = TargetedAbilityContentPackage.ReadJson(json);
        Assert.Equal(json, read.ToJson());
        Assert.Equal(2, read.Version);
        Assert.Equal(delivery, read.Abilities[0].Delivery);
        var ground = SyntheticBasicSkillBattle.Ground;
        var map = new BattleMapDefinition(7, 5, 1, Enumerable.Repeat(new BattleTile(ground, 10), 35).ToArray());
        var setup = BaseJobBattleSetup.Create(new(metadata.Jobs, metadata.Skillsets, metadata.Abilities), map,
            [new(SyntheticBasicSkillBattle.First, metadata.Jobs[0].Id, new(1, 2, 0), 6, [new(ground, new(1))]),
             new(SyntheticBasicSkillBattle.Second, metadata.Jobs[0].Id, new(3, 2, 0), 6, [new(ground, new(1))])],
            RandomStateGenerator.Seed(1));
        var profile = TargetedAbilityBattleSetup.CreatePhysicalProfile(metadata, read, setup,
            [new(SyntheticBasicSkillBattle.First, 2048, second)], ground, 0, delivery);
        foreach (var ability in read.Abilities)
        {
            Assert.Same(ability.Definition, profile.Abilities[new(ability.Id.Value)]);
            Assert.Same(ability.Formula, profile.Abilities[new(ability.Id.Value)].Formula);
        }
        Assert.Contains(new AssetId(second.Value), profile.Units[SyntheticBasicSkillBattle.First].Abilities);
        Assert.DoesNotContain(new AssetId(first.Value), profile.Units[SyntheticBasicSkillBattle.First].Abilities);
        Assert.Empty(profile.Units[SyntheticBasicSkillBattle.Second].Abilities);
        if (delivery == PhysicalAbilityDelivery.AdjacentUnit)
            Assert.Same(profile, DashAbilityBattleProfile.FromPhysicalProfile(profile).PhysicalProfile);
        else
            Assert.Same(profile, TargetedAbilityBattleProfile.FromPhysicalProfile(profile).PhysicalProfile);
    }
}
