using System.Collections.Immutable;
using Fft.Content.BattleSetup;
using Fft.Content.ContentPackages;
using Fft.Content.Definitions;
using Fft.Core;

namespace Fft.Content.Samples;

/// <summary>Invented content and script for the headless milestone; no original media or recovered unit records.</summary>
public static class SyntheticTwoUnitBattle
{
    public static BattleUnitId FirstUnitId => new("sample:unit/first");
    public static BattleUnitId SecondUnitId => new("sample:unit/second");

    public static TwoUnitBattleDefinition Create()
    {
        var first = FirstUnitId;
        var second = SecondUnitId;
        var weaponId = new AssetId("sample:item/plain-sword");
        var metadata = new ContentPackage("sample:package/two-unit-metadata", "sample:profile/two-unit-battle",
            ContentPackageKind.Baseline, null, [], [], []);
        var swords = new SwordContentPackage("sample:package/two-unit-swords", metadata.ProfileId, metadata.PackageId,
            [new(new(weaponId.Value), SwordWeaponType.Sword, 1, 5, SwordDamageFormula.WeaponDamage,
                SwordTargetingFlags.Striking, 0, SwordElement.Neutral, new(128, new byte[5]))]);
        var surface = new TerrainSurfaceId("sample:terrain/ground");
        var profile = new UnitMovementProfile(1, 2, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [new(surface, new TerrainMovementCost(1))]);
        var map = new BattleMapDefinition(2, 2, 1, Enumerable.Repeat(new BattleTile(surface, 10), 4).ToArray());
        var rng = FftBiosRandom.Seed(1);
        var battle = new BattleState(map, [new(first, new(0, 0, 0), profile), new(second, new(1, 1, 0), profile)], rng);
        var turns = new TurnCycleState([new(first, 10, 0, 0), new(second, 10, 0, 1)], rng);
        var setup = AttackBattleSetup.Create(new(battle, turns),
            [Unit(first, BattleFacing.East), Unit(second, BattleFacing.West)], swords, metadata);
        return new(new("sample:scenario/two-unit-battle"), first, second, setup,
            [AttackBattleSetup.ResolveWeapon(swords, metadata, weaponId)]);

        AttackUnitState Unit(BattleUnitId id, BattleFacing facing) =>
            new(id, 50, 50, 0, 0, 5, 10, 0, 0, 0, 70, 0, facing, AttackUnitStatus.None, 0, weaponId);
    }

    public static ImmutableArray<TwoUnitBattleCommand> Commands =>
    [
        new TwoUnitBattleCommand.Advance(),
        new TwoUnitBattleCommand.Move(FirstUnitId, new(0, 1, 0)),
        new TwoUnitBattleCommand.Attack(FirstUnitId, SecondUnitId),
        new TwoUnitBattleCommand.Advance(),
        new TwoUnitBattleCommand.Attack(SecondUnitId, FirstUnitId),
        new TwoUnitBattleCommand.Wait(SecondUnitId),
        new TwoUnitBattleCommand.Advance(),
        new TwoUnitBattleCommand.Move(SecondUnitId, new(1, 0, 0)),
        new TwoUnitBattleCommand.Wait(SecondUnitId),
        new TwoUnitBattleCommand.Advance(),
        new TwoUnitBattleCommand.Move(FirstUnitId, new(0, 0, 0)),
        new TwoUnitBattleCommand.Attack(FirstUnitId, SecondUnitId),
    ];
}
