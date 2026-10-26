using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;

namespace Zodiac.Content.Samples;

/// <summary>Separate invented ability-enabled setup. No original media, learned progression or save support.</summary>
public static class SyntheticAccumulateBattle
{
    public const string ProfileId = "sample:profile/accumulate-battle";
    public static AbilityId AccumulateId => new("sample:ability/accumulate");

    public static ContentPackage Metadata() => new("sample:package/accumulate-metadata", ProfileId, ContentPackageKind.Baseline, null,
        [new(new("sample:job/trainee"), new("sample:skillset/basic"), 1, 2, false, [])],
        [new(new("sample:skillset/basic"), [new(0, AccumulateId)])],
        [new(AccumulateId, 123, 37, 1, new byte[4])]);

    public static ImmediateAbilityContentPackage Effects(ContentPackage metadata) =>
        new("sample:package/accumulate-effects", metadata.ProfileId, metadata.PackageId,
            [new(AccumulateId, ImmediateAbilityEffect.SelfPhysicalAttackIncrease, 1, 0, 0)]);

    public static TwoUnitBattleDefinition Create(int physicalAttack = 5, int swordPower = 4, bool grantFirstUnit = true)
    {
        var metadata = Metadata();
        var effects = Effects(metadata);
        var first = SyntheticTwoUnitBattle.FirstUnitId;
        var second = SyntheticTwoUnitBattle.SecondUnitId;
        var surface = new TerrainSurfaceId("sample:terrain/ground");
        var map = new BattleMapDefinition(2, 2, 1, Enumerable.Repeat(new BattleTile(surface, 10), 4).ToArray());
        var rng = FftBiosRandom.Seed(1);
        var setup = BaseJobBattleSetup.Create(new(metadata.Jobs, metadata.Skillsets, metadata.Abilities), map,
            [Placement(first, new(0, 0, 0)), Placement(second, new(1, 1, 0))], rng);
        var turns = new TurnCycleState([new(first, 10, 0, 0), new(second, 10, 0, 1)], rng);
        AssetId weapon = new("sample:item/ability-sword");
        var battle = new AttackBattleSnapshot(new(setup.InitialState, turns),
            [Unit(first, physicalAttack, BattleFacing.East), Unit(second, 5, BattleFacing.West)]);
        var profile = ImmediateAbilityBattleSetup.CreateProfile(metadata, effects, setup,
            grantFirstUnit ? [new(first, 0, AccumulateId)] : [], 0);
        return new(new("sample:scenario/accumulate-battle"), first, second, battle, [new(weapon, swordPower)], profile);

        BaseJobUnitPlacement Placement(BattleUnitId id, TilePosition position) =>
            new(id, metadata.Jobs[0].Id, position, 6, [new(surface, new(1))]);
        AttackUnitState Unit(BattleUnitId id, int pa, BattleFacing facing) =>
            new(id, 50, 50, 0, 0, pa, 0, 0, 0, 0, 70, 0, facing, AttackUnitStatus.None, 0, weapon);
    }
}
