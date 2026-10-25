using Zodiac.Content.BattleSetup;
using Zodiac.Content.Definitions;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class BaseJobBattleSetupTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ground");
    private static readonly JobId Recruit = new("sample:job/recruit");
    private static readonly SkillsetId Practice = new("sample:skillset/practice");
    private static readonly AbilityId Focus = new("sample:ability/focus");
    private static readonly AbilityId Innate = new("sample:ability/innate");
    private static readonly BattleUnitId A = new("sample:unit/a");
    private static readonly BattleUnitId B = new("sample:unit/b");

    [Fact]
    public void Two_units_share_definitions_but_keep_independent_immutable_battle_state_and_rng()
    {
        DefinitionCatalog catalog = Catalog();
        var rng = new FftRandomState(0x80000000u, 17);
        BaseJobBattleSetup setup = BaseJobBattleSetup.Create(catalog, Map(),
            new[] { Placement(A, new(0, 0, 0)), Placement(B, new(4, 0, 0)) }, rng);
        Assert.Equal(2, setup.Definitions.Length);
        Assert.Same(catalog.Jobs[0], setup.Definitions[0].Job);
        Assert.Same(setup.Definitions[0].Job, setup.Definitions[1].Job);
        Assert.Same(catalog.Skillsets[0], setup.Definitions[0].PrimarySkillset);
        Assert.Equal(new[] { Focus, Innate }, setup.Definitions[0].Abilities.Select(ability => ability.Id));
        Assert.Same(catalog.GetAbility(Focus), setup.Definitions[0].Abilities[0]);
        Assert.Equal(A, setup.Definitions[0].UnitId);
        Assert.Equal(B, setup.Definitions[1].UnitId);
        BattleState before = setup.InitialState;
        Assert.NotSame(before.Units[0], before.Units[1]);
        var result = BattleSimulator.Apply(before, new(A, new(1, 0, 0)), BattleMovementPolicy.TerrainCostPrototype);
        Assert.True(result.Accepted);
        Assert.Equal(new TilePosition(0, 0, 0), before.Units[0].Position);
        Assert.False(before.Units[0].HasMoved);
        Assert.True(result.State.Units[0].HasMoved);
        Assert.Same(before.Units[1], result.State.Units[1]);
        Assert.False(result.State.Units[1].HasMoved);
        Assert.Equal(rng, before.RngState);
        Assert.Equal(rng, result.State.RngState);
    }

    [Fact]
    public void Authored_Move_changes_reachability_without_changing_simulation_or_old_snapshots()
    {
        var map = Map();
        var placement = Placement(A, new(0, 0, 0));
        BaseJobBattleSetup slow = BaseJobBattleSetup.Create(Catalog(move: 1, jump: 2), map, new[] { placement });
        BaseJobBattleSetup fast = BaseJobBattleSetup.Create(Catalog(move: 3, jump: 7), map, new[] { placement });
        var command = new MoveUnitCommand(A, new(3, 0, 0));
        var rejected = BattleSimulator.Apply(slow.InitialState, command, BattleMovementPolicy.TerrainCostPrototype);
        var accepted = BattleSimulator.Apply(fast.InitialState, command, BattleMovementPolicy.TerrainCostPrototype);
        Assert.False(rejected.Accepted);
        Assert.Same(slow.InitialState, rejected.State);
        Assert.Empty(rejected.Events);
        Assert.True(accepted.Accepted);
        Assert.Equal(1, slow.InitialState.Units[0].MovementProfile.Move);
        Assert.Equal(2, slow.InitialState.Units[0].MovementProfile.Jump);
        Assert.Equal(3, fast.InitialState.Units[0].MovementProfile.Move);
        Assert.Equal(7, fast.InitialState.Units[0].MovementProfile.Jump);
        Assert.False(fast.InitialState.Units[0].HasMoved);
        Assert.Equal(1, slow.Definitions[0].Job.BaseMove);
    }

    [Fact]
    public void Placement_snapshots_costs_before_setup_and_profiles_remain_immutable()
    {
        var costs = Costs().ToList();
        var placement = new BaseJobUnitPlacement(A, Recruit, new(0, 0, 0), 6, costs);
        costs[0] = new(Ground, new TerrainMovementCost(99));
        costs.Clear();
        BaseJobBattleSetup setup = BaseJobBattleSetup.Create(Catalog(), Map(), new[] { placement });
        Assert.Equal(1, Assert.Single(placement.TerrainCosts).Value.MovePoints);
        Assert.Equal(1, setup.InitialState.Units[0].MovementProfile.TerrainCosts[Ground].MovePoints);
        Assert.Equal(UnitMovementMode.Normal, setup.InitialState.Units[0].MovementProfile.Mode);
        Assert.Equal(UnitMovementTraits.None, setup.InitialState.Units[0].MovementProfile.Traits);
    }

    [Fact]
    public void Missing_job_and_missing_catalog_references_fail_before_a_battle_is_returned()
    {
        var missing = new BaseJobUnitPlacement(A, new("sample:job/missing"), new(0, 0, 0), 6, Costs());
        Assert.Contains("missing job", Assert.Throws<ArgumentException>(() =>
            BaseJobBattleSetup.Create(Catalog(), Map(), new[] { missing })).Message);
        JobDefinition job = Catalog().Jobs[0];
        Assert.Throws<ArgumentException>(() => new DefinitionCatalog(new[] { job },
            Array.Empty<SkillsetDefinition>(), Catalog().Abilities));
        Assert.Throws<ArgumentException>(() => new DefinitionCatalog(new[] { job },
            Catalog().Skillsets, Array.Empty<AbilityDefinition>()));
    }

    [Fact]
    public void Default_ids_are_rejected_at_placement_boundary()
    {
        Assert.Throws<ArgumentException>(() => new BaseJobUnitPlacement(default, Recruit, new(0, 0, 0), 6, Costs()));
        Assert.Throws<ArgumentException>(() => new BaseJobUnitPlacement(A, default, new(0, 0, 0), 6, Costs()));
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-position")]
    [InlineData("outside")]
    [InlineData("blocked")]
    [InlineData("cannot-stop")]
    [InlineData("untargetable")]
    public void Invalid_initial_placements_are_rejected(string invalid)
    {
        BattleMapDefinition map = Map();
        BaseJobUnitPlacement[] placements = { Placement(A, new(0, 0, 0)) };
        switch (invalid)
        {
            case "duplicate-id": placements = new[] { placements[0], Placement(A, new(4, 0, 0)) }; break;
            case "duplicate-position": placements = new[] { placements[0], Placement(B, new(0, 0, 0)) }; break;
            case "outside": placements = new[] { Placement(A, new(5, 0, 0)) }; break;
            default:
                var flags = invalid switch { "blocked" => BattleTileMovementFlags.Blocked,
                    "cannot-stop" => BattleTileMovementFlags.CannotStop, _ => BattleTileMovementFlags.Untargetable };
                map = Map(flags);
                break;
        }
        Assert.Throws<ArgumentException>(() => BaseJobBattleSetup.Create(Catalog(), map, placements));
    }

    [Theory]
    [InlineData(true, 6)]
    [InlineData(false, 4)]
    [InlineData(false, 0)]
    public void Unsupported_stepping_stone_or_body_size_fails_explicitly(bool steppingStone, int bodyHeight)
    {
        var placement = new BaseJobUnitPlacement(A, Recruit, new(0, 0, 0), bodyHeight, Costs());
        Assert.Throws<NotSupportedException>(() => BaseJobBattleSetup.Create(
            Catalog(steppingStone: steppingStone), Map(), new[] { placement }));
    }

    [Fact]
    public void Missing_surface_cost_and_blocked_origin_cost_are_not_invented_or_ignored()
    {
        var missing = new BaseJobUnitPlacement(A, Recruit, new(0, 0, 0), 6,
            Array.Empty<KeyValuePair<TerrainSurfaceId, TerrainMovementCost>>());
        Assert.Throws<ArgumentException>(() => BaseJobBattleSetup.Create(Catalog(), Map(), new[] { missing }));
        var blocked = new BaseJobUnitPlacement(A, Recruit, new(0, 0, 0), 6,
            new[] { new KeyValuePair<TerrainSurfaceId, TerrainMovementCost>(Ground, new(0, true)) });
        Assert.Throws<ArgumentException>(() => BaseJobBattleSetup.Create(Catalog(), Map(), new[] { blocked }));
    }

    private static BaseJobUnitPlacement Placement(BattleUnitId id, TilePosition position) =>
        new(id, Recruit, position, 6, Costs());
    private static KeyValuePair<TerrainSurfaceId, TerrainMovementCost>[] Costs() =>
        new[] { new KeyValuePair<TerrainSurfaceId, TerrainMovementCost>(Ground, new(1)) };
    private static BattleMapDefinition Map(BattleTileMovementFlags originFlags = BattleTileMovementFlags.None) =>
        new(5, 1, 1, new[] { new BattleTile(Ground, 10, movementFlags: originFlags), new(Ground, 10),
            new(Ground, 10), new(Ground, 10), new(Ground, 10) });
    private static DefinitionCatalog Catalog(int move = 2, int jump = 3, bool steppingStone = false) =>
        new(new[] { new JobDefinition(Recruit, Practice, move, jump, steppingStone, new[] { Innate, Focus }) },
            new[] { new SkillsetDefinition(Practice, new[] { new SkillsetSlot(0, Focus), new(1, null), new(2, Focus) }) },
            new[] { new AbilityDefinition(Focus, 7, 0, 1, new byte[4]), new(Innate, 9, 0, 1, new byte[4]) });
}
