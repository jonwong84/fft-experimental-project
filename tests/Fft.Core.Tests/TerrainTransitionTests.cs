using Fft.Core;
using Xunit;

namespace Fft.Core.Tests;

public sealed class TerrainTransitionTests
{
    private static readonly TerrainSurfaceId Surface = new("fixture:terrain/ground");
    private static readonly BattleUnitId Mover = new("fixture:unit/mover");

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Entry_cost_and_available_Move_control_transition(int cost, bool accepted)
    {
        var state = State(cost: cost, move: 2);
        var result = BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.TerrainCostPrototype);
        Assert.Equal(accepted, result.Accepted);
        if (accepted)
        {
            Assert.False(state.Units[0].HasMoved);
            Assert.Equal(new TilePosition(0, 0, 0), state.Units[0].Position);
            Assert.True(result.State.Units[0].HasMoved);
            Assert.Equal(new BattleMovementEvent(Mover, new(0, 0, 0), new(2, 0, 0)), Assert.Single(result.Events));
        }
        else AssertRejected(state, result);
    }

    [Theory]
    [InlineData(BattleTileMovementFlags.Blocked)]
    [InlineData(BattleTileMovementFlags.CannotStop)]
    [InlineData(BattleTileMovementFlags.Untargetable)]
    public void Destination_flags_reject_without_state_or_events(BattleTileMovementFlags flags)
    {
        var state = State(destinationFlags: flags);
        AssertRejected(state, BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.TerrainCostPrototype));
        Assert.True(BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.FlatGridPrototype).Accepted);
    }

    [Theory]
    [InlineData(BattleTileMovementFlags.CannotStop)]
    [InlineData(BattleTileMovementFlags.Untargetable)]
    public void Non_stopping_intermediate_tiles_allow_propagation(BattleTileMovementFlags flags)
    {
        var state = State(intermediateFlags: flags);
        Assert.True(BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.TerrainCostPrototype).Accepted);
    }

    [Fact]
    public void Other_units_explicitly_block_terrain_traversal_and_destinations()
    {
        var state = State();
        state = new BattleState(state.Map, new[] { state.Units[0], new BattleUnitState(new("fixture:unit/other"), new(1, 0, 0), 0) });
        AssertRejected(state, BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.TerrainCostPrototype));
        var occupied = BattleSimulator.Apply(state, new(Mover, new(1, 0, 0)), BattleMovementPolicy.TerrainCostPrototype);
        Assert.Equal(BattleTransitionRejection.DestinationOccupied, occupied.Rejection);
        Assert.Same(state, occupied.State);
        Assert.Empty(occupied.Events);
        Assert.True(BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.FlatGridPrototype).Accepted);
    }

    [Fact]
    public void Occupied_intermediate_tile_can_be_bypassed_when_budget_allows()
    {
        var map = new BattleMapDefinition(3, 2, 1, Enumerable.Repeat(new BattleTile(Surface, 0), 6).ToArray());
        var profile = new UnitMovementProfile(4, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            new[] { new KeyValuePair<TerrainSurfaceId, TerrainMovementCost>(Surface, new(1)) });
        var state = new BattleState(map, new[] { new BattleUnitState(Mover, new(0, 0, 0), profile),
            new BattleUnitState(new("fixture:unit/other"), new(1, 0, 0), 0) });
        var result = BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.TerrainCostPrototype);
        Assert.True(result.Accepted);
        Assert.Equal(new TilePosition(2, 0, 0), result.State.Units[0].Position);
    }

    [Fact]
    public void Blocked_surface_rejects_and_missing_cost_is_an_explicit_error()
    {
        var state = State(blockedSurface: true);
        AssertRejected(state, BattleSimulator.Apply(state, new(Mover, new(1, 0, 0)), BattleMovementPolicy.TerrainCostPrototype));
        state = State(missingCost: true);
        var error = Assert.Throws<InvalidOperationException>(() =>
            BattleSimulator.Apply(state, new(Mover, new(1, 0, 0)), BattleMovementPolicy.TerrainCostPrototype));
        Assert.Contains(Surface.Value, error.Message);
        Assert.False(state.Units[0].HasMoved);
    }

    [Fact]
    public void Unknown_policy_is_rejected_explicitly() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        BattleSimulator.Apply(State(), new(Mover, new(1, 0, 0)), (BattleMovementPolicy)999));

    private static BattleState State(int cost = 1, int move = 2,
        BattleTileMovementFlags destinationFlags = BattleTileMovementFlags.None,
        BattleTileMovementFlags intermediateFlags = BattleTileMovementFlags.None,
        bool blockedSurface = false, bool missingCost = false)
    {
        var tiles = new[] { new BattleTile(Surface, 0), new BattleTile(Surface, 0, movementFlags: intermediateFlags),
            new BattleTile(Surface, 0, movementFlags: destinationFlags) };
        var costs = missingCost ? Array.Empty<KeyValuePair<TerrainSurfaceId, TerrainMovementCost>>()
            : new[] { new KeyValuePair<TerrainSurfaceId, TerrainMovementCost>(Surface, new(cost, blockedSurface)) };
        var profile = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None, costs);
        return new(new BattleMapDefinition(3, 1, 1, tiles), new[] { new BattleUnitState(Mover, new(0, 0, 0), profile) });
    }

    private static void AssertRejected(BattleState state, BattleTransitionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(BattleTransitionRejection.DestinationNotReachable, result.Rejection);
        Assert.Same(state, result.State);
        Assert.Empty(result.Events);
    }
}
