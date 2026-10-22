using Fft.Content.BattleSetup;
using Fft.Content.ContentPackages;
using Fft.Content.Definitions;
using Fft.Core;
using Xunit;

namespace Fft.Content.Tests;

public sealed class AttackBattleSetupTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static ContentPackage Metadata(string profile = "sample:profile/attack") => new("sample:package/metadata", profile, ContentPackageKind.Baseline, null, [], [], []);
    private static SwordContentPackage Swords(int power = 5, int evade = 0) => new("sample:package/swords", "sample:profile/attack", "sample:package/metadata",
        [new SwordDefinition(new("sample:item/sword"), SwordWeaponType.Sword, 1, power, SwordDamageFormula.WeaponDamage, SwordTargetingFlags.Striking, evade, SwordElement.Neutral, new(128, new byte[5]))]);
    private static AttackUnitState Unit(BattleUnitId id, AssetId? weapon = null, int pa = 5, int hp = 50, AttackUnitStatus status = AttackUnitStatus.None, int zodiac = 0) =>
        new(id, hp, 50, 0, 0, pa, 10, 0, 0, 0, 70, zodiac, BattleFacing.East, status, 0, weapon);
    private static TurnBattleState State()
    {
        var rng = new FftRandomState(uint.MaxValue, 77);
        var map = new BattleMapDefinition(3, 3, 1, Enumerable.Repeat(new BattleTile(10), 9).ToArray());
        return new(new(map, [new(A, new(1, 1, 0), 1), new(B, new(2, 1, 0), 1)], rng),
            new([new(A, 10, 0, 0, true), new(B, 10, 100, 1)], rng, phase: TurnCyclePhase.UnitTurn));
    }

    [Fact]
    public void Equipment_resolves_before_startup_and_input_collections_are_snapshotted()
    {
        var units = new List<AttackUnitState> { Unit(A, new AssetId("sample:item/sword")), Unit(B) };
        var before = State();
        AttackBattleSnapshot snapshot = AttackBattleSetup.Create(before, units, Swords(), Metadata());
        units.Clear();
        Assert.Equal(2, snapshot.Combatants.Length);
        Assert.Equal("sample:item/sword", snapshot.Combatants[0].RightHandWeaponId!.Value.Value);
        Assert.Same(before, snapshot.TurnBattle);
        var moved = TurnBattleSimulator.Move(before, new(A, new(0, 1, 0)));
        var after = snapshot.WithTurnBattle(moved.State);
        Assert.Equal(snapshot.Combatants, after.Combatants);
        Assert.Equal(50, after.Combatants[1].Hp);
        Assert.Equal(new TilePosition(1, 1, 0), snapshot.TurnBattle.Battle.Units[0].Position);
        Assert.Equal(before.Turns.RngState, after.TurnBattle.Battle.RngState);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("evade")]
    [InlineData("overflow")]
    [InlineData("profile")]
    public void Unsupported_or_missing_content_fails_before_returning_a_setup(string kind)
    {
        AssetId weapon = new(kind == "missing" ? "sample:item/absent" : "sample:item/sword");
        AttackUnitState[] units = [Unit(A, weapon, kind == "overflow" ? 255 : 5), Unit(B)];
        void Build() => AttackBattleSetup.Create(State(), units, Swords(kind == "overflow" ? 255 : 5, kind == "evade" ? 1 : 0), Metadata(kind == "profile" ? "sample:profile/wrong" : "sample:profile/attack"));
        if (kind == "missing") Assert.Throws<ArgumentException>(Build);
        else if (kind == "profile") Assert.Throws<ContentPackageValidationException>(Build);
        else Assert.Throws<NotSupportedException>(Build);
    }

    [Fact]
    public void Result_statuses_are_representable_but_not_accepted_as_initial_supported_setup()
    {
        AttackUnitState[] units = [Unit(A), Unit(B, hp: 0, status: AttackUnitStatus.Dead)];
        var snapshot = new AttackBattleSnapshot(State(), units);
        Assert.Equal(AttackUnitStatus.Dead, snapshot.Combatants[1].Status);
        Assert.Throws<NotSupportedException>(() => AttackBattleSetup.Create(State(), units, Swords(), Metadata()));
        Assert.Throws<ArgumentException>(() => new AttackBattleSnapshot(State(), [Unit(A, hp: 0, status: AttackUnitStatus.Dead), Unit(B)]));
    }

    [Fact]
    public void Missing_duplicate_or_non_neutral_combat_projections_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new AttackBattleSnapshot(State(), [Unit(A)]));
        Assert.Throws<ArgumentException>(() => new AttackBattleSnapshot(State(), [Unit(A), Unit(A)]));
        Assert.Throws<NotSupportedException>(() => new AttackBattleSnapshot(State(), [Unit(A), Unit(B, zodiac: 1)]));
    }
}
