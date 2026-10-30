using System.Collections.Immutable;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework;

namespace Zodiac.Content.Samples;

/// <summary>Invented flat scene with explicit Accumulate/Throw Stone grants; not an original encounter.</summary>
public static class SyntheticBasicSkillBattle
{
    public const string ProfileId = "sample:profile/basic-skill-battle";
    public static BattleUnitId First => new("sample:unit/first");
    public static BattleUnitId Second => new("sample:unit/second");
    public static AbilityId Accumulate => new("sample:ability/accumulate");
    public static AbilityId ThrowStone => new("sample:ability/throw-stone");
    public static TerrainSurfaceId Ground => new("sample:terrain/ordinary-ground");

    public static ContentPackage Metadata() => new("sample:package/basic-skill-metadata", ProfileId, ContentPackageKind.Baseline, null,
        [new(new("sample:job/trainee"), new("sample:skillset/basic"), 2, 2, false, [])],
        [new(new("sample:skillset/basic"), [new(0, Accumulate), new(2, ThrowStone)])],
        [new(Accumulate, 123, 37, 1, new byte[4]), new(ThrowStone, 90, 80, 1, new byte[4])]);
    public static ImmediateAbilityContentPackage SelfEffects(ContentPackage metadata) =>
        new("sample:package/basic-self", metadata.ProfileId, metadata.PackageId, [new(Accumulate, ImmediateAbilityEffect.SelfPhysicalAttackIncrease, 1, 0, 0)]);
    public static TargetedAbilityContentPackage TargetedEffects(ContentPackage metadata) =>
        new("sample:package/basic-targeted", metadata.ProfileId, metadata.PackageId, [new(ThrowStone, TargetedAbilityEffect.ThrowStoneFlat, 4, 2, 0, 0)]);

    public static TwoUnitBattleDefinition Create(int physicalAttack = 5, int swordPower = 4)
    {
        var metadata = Metadata();
        return Create(metadata, SelfEffects(metadata), TargetedEffects(metadata), physicalAttack, swordPower);
    }

    public static TwoUnitBattleDefinition Create(ContentPackage metadata, ImmediateAbilityContentPackage self,
        TargetedAbilityContentPackage targeted, int physicalAttack = 5, int swordPower = 4, bool grantTargeted = true)
    {
        ArgumentNullException.ThrowIfNull(metadata); ArgumentNullException.ThrowIfNull(self); ArgumentNullException.ThrowIfNull(targeted);
        self.ValidateAgainst(metadata); targeted.ValidateAgainst(metadata);
        if (metadata.ProfileId != ProfileId || metadata.Jobs.Length != 1 || metadata.Skillsets.Length != 1 ||
            metadata.Abilities.Length != 2 || self.Abilities.Length != 1 || targeted.Abilities.Length != 1 ||
            self.Abilities[0].Id != Accumulate || targeted.Abilities[0].Id != ThrowStone)
            throw new NotSupportedException("Select the closed authored Basic Skill metadata and companions explicitly.");
        var map = new BattleMapDefinition(7, 5, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 35).ToArray());
        var rng = FftBiosRandom.Seed(1);
        var setup = BaseJobBattleSetup.Create(new(metadata.Jobs, metadata.Skillsets, metadata.Abilities), map,
            [Placement(First, new(1, 2, 0)), Placement(Second, new(3, 2, 0))], rng);
        var turns = new TurnCycleState([new(First, 10, 0, 0), new(Second, 10, 0, 1)], rng);
        var weapon = new AssetId("sample:item/basic-sword");
        var battle = new AttackBattleSnapshot(new(setup.InitialState, turns),
            [Unit(First, physicalAttack, BattleFacing.East), Unit(Second, 5, BattleFacing.West)]);
        var immediate = ImmediateAbilityBattleSetup.CreateProfile(metadata, self, setup, [new(First, 0, Accumulate)], 0);
        var damage = TargetedAbilityBattleSetup.CreateProfile(metadata, targeted, setup,
            grantTargeted ? [new(First, 2, ThrowStone)] : [], Ground, 0);
        return new(new("sample:scenario/basic-skill-battle"), First, Second, battle, [new(weapon, swordPower)], immediate, damage);

        BaseJobUnitPlacement Placement(BattleUnitId id, TilePosition position) => new(id, metadata.Jobs[0].Id, position, 6, [new(Ground, new(1))]);
        AttackUnitState Unit(BattleUnitId id, int pa, BattleFacing facing) =>
            new(id, 50, 50, 0, 0, pa, 0, 0, 0, 0, 70, 0, facing, AttackUnitStatus.None, 0, weapon);
    }

    /// <summary>Finite legal turns plus deliberate duplicate, ungranted and terminal rejects; no state injection.</summary>
    public static ImmutableArray<TwoUnitBattleCommand> Journal()
    {
        var state = Create().Start();
        var commands = ImmutableArray.CreateBuilder<TwoUnitBattleCommand>();
        Add(new TwoUnitBattleCommand.Advance());
        Add(new TwoUnitBattleCommand.UseAbility(First, new(Accumulate.Value)));
        Add(new TwoUnitBattleCommand.UseAbility(First, new(Accumulate.Value)), accepted: false);
        Add(new TwoUnitBattleCommand.Move(First, new(2, 2, 0)));
        Add(new TwoUnitBattleCommand.Advance());
        Add(new TwoUnitBattleCommand.UseTargetedAbility(Second, new(ThrowStone.Value), First), accepted: false);
        Add(new TwoUnitBattleCommand.Wait(Second));
        for (int attempt = 0; state.Outcome == TwoUnitBattleOutcome.InProgress && attempt < 80; attempt++)
        {
            Add(new TwoUnitBattleCommand.Advance());
            var actor = state.Battle.TurnBattle.Turns.ActiveUnitId!.Value;
            if (actor == First) Add(new TwoUnitBattleCommand.UseTargetedAbility(First, new(ThrowStone.Value), Second));
            if (state.Outcome == TwoUnitBattleOutcome.InProgress) Add(new TwoUnitBattleCommand.Wait(actor));
        }
        if (state.Outcome != TwoUnitBattleOutcome.FirstUnitWon) throw new InvalidOperationException("Authored Basic Skill journal exceeded its finite turn budget.");
        foreach (TwoUnitBattleCommand command in new TwoUnitBattleCommand[]
        {
            new TwoUnitBattleCommand.Advance(), new TwoUnitBattleCommand.Move(First, new(1,2,0)),
            new TwoUnitBattleCommand.Attack(First,Second), new TwoUnitBattleCommand.UseAbility(First,new(Accumulate.Value)),
            new TwoUnitBattleCommand.UseTargetedAbility(First,new(ThrowStone.Value),Second), new TwoUnitBattleCommand.Wait(First),
        }) Add(command, accepted: false);
        return commands.ToImmutable();

        void Add(TwoUnitBattleCommand command, bool accepted = true)
        {
            var result = TwoUnitBattleScenario.Apply(state, command);
            if (result.Accepted != accepted) throw new InvalidOperationException($"Unexpected authored command outcome: {command} / {result}");
            commands.Add(command); state = result.State;
        }
    }
}
