using Fft.Core;
using Xunit;

namespace Fft.Core.Tests;

public sealed class BattleSimulatorTests
{
    private static readonly BattleUnitId MoverId = new("unit:mover");
    private static readonly BattleUnitId OtherId = new("unit:other");

    [Fact]
    public void Apply_accepts_reachable_destination_and_returns_new_state_and_event()
    {
        BattleState initial = CreateState(
            new BattleUnitState(MoverId, new TilePosition(1, 1, 0), move: 2));

        BattleTransitionResult result = BattleSimulator.Apply(
            initial,
            new MoveUnitCommand(MoverId, new TilePosition(2, 2, 0)));

        Assert.True(result.Accepted);
        Assert.Equal(BattleTransitionRejection.None, result.Rejection);
        Assert.NotSame(initial, result.State);
        Assert.True(initial.TryGetUnit(MoverId, out BattleUnitState? original));
        Assert.Equal(new TilePosition(1, 1, 0), original!.Position);
        Assert.False(original.HasMoved);
        Assert.True(result.State.TryGetUnit(MoverId, out BattleUnitState? moved));
        Assert.Equal(new TilePosition(2, 2, 0), moved!.Position);
        Assert.True(moved.HasMoved);
        Assert.Equal(
            new BattleMovementEvent(MoverId, new TilePosition(1, 1, 0), new TilePosition(2, 2, 0)),
            Assert.Single(result.Events));
    }

    [Fact]
    public void Apply_rejects_unknown_unit_without_changing_state()
    {
        BattleState initial = CreateState(new BattleUnitState(MoverId, new TilePosition(1, 1, 0), move: 2));

        BattleTransitionResult result = BattleSimulator.Apply(
            initial,
            new MoveUnitCommand(OtherId, new TilePosition(2, 1, 0)));

        AssertRejectedWithoutMutation(initial, result, BattleTransitionRejection.UnknownUnit);
    }

    [Fact]
    public void Apply_rejects_second_move_from_same_unit()
    {
        BattleState initial = CreateState(
            new BattleUnitState(MoverId, new TilePosition(1, 1, 0), move: 2, hasMoved: true));

        BattleTransitionResult result = BattleSimulator.Apply(
            initial,
            new MoveUnitCommand(MoverId, new TilePosition(2, 1, 0)));

        AssertRejectedWithoutMutation(initial, result, BattleTransitionRejection.UnitHasAlreadyMoved);
    }

    [Fact]
    public void Apply_rejects_occupied_destination()
    {
        BattleState initial = CreateState(
            new BattleUnitState(MoverId, new TilePosition(1, 1, 0), move: 2),
            new BattleUnitState(OtherId, new TilePosition(2, 1, 0), move: 1));

        BattleTransitionResult result = BattleSimulator.Apply(
            initial,
            new MoveUnitCommand(MoverId, new TilePosition(2, 1, 0)));

        AssertRejectedWithoutMutation(initial, result, BattleTransitionRejection.DestinationOccupied);
    }

    [Fact]
    public void Apply_rejects_unreachable_destination()
    {
        BattleState initial = CreateState(new BattleUnitState(MoverId, new TilePosition(1, 1, 0), move: 1));

        BattleTransitionResult result = BattleSimulator.Apply(
            initial,
            new MoveUnitCommand(MoverId, new TilePosition(3, 3, 0)));

        AssertRejectedWithoutMutation(initial, result, BattleTransitionRejection.DestinationNotReachable);
    }

    [Fact]
    public void Apply_rejects_destination_on_another_layer()
    {
        BattleState initial = CreateState(new BattleUnitState(MoverId, new TilePosition(1, 1, 0), move: 2));

        BattleTransitionResult result = BattleSimulator.Apply(
            initial,
            new MoveUnitCommand(MoverId, new TilePosition(1, 1, 1)));

        AssertRejectedWithoutMutation(initial, result, BattleTransitionRejection.DestinationNotReachable);
    }

    [Fact]
    public void Apply_rejects_destination_outside_map_bounds()
    {
        BattleState initial = CreateState(new BattleUnitState(MoverId, new TilePosition(0, 0, 0), move: 1));

        BattleTransitionResult result = BattleSimulator.Apply(
            initial,
            new MoveUnitCommand(MoverId, new TilePosition(-1, 0, 0)));

        AssertRejectedWithoutMutation(initial, result, BattleTransitionRejection.DestinationNotReachable);
    }

    [Fact]
    public void Apply_is_deterministic_for_equal_initial_state_and_command()
    {
        BattleState firstInitial = CreateState(
            new BattleUnitState(MoverId, new TilePosition(1, 1, 0), move: 2),
            new BattleUnitState(OtherId, new TilePosition(4, 4, 1), move: 1));
        BattleState secondInitial = CreateState(
            new BattleUnitState(MoverId, new TilePosition(1, 1, 0), move: 2),
            new BattleUnitState(OtherId, new TilePosition(4, 4, 1), move: 1));
        var command = new MoveUnitCommand(MoverId, new TilePosition(2, 2, 0));

        BattleTransitionResult first = BattleSimulator.Apply(firstInitial, command);
        BattleTransitionResult second = BattleSimulator.Apply(secondInitial, command);

        Assert.Equal(first.Accepted, second.Accepted);
        Assert.Equal(first.Rejection, second.Rejection);
        Assert.Equal(first.State.Units, second.State.Units);
        Assert.Equal(first.Events, second.Events);
    }

    [Fact]
    public void BattleState_rejects_duplicate_unit_ids()
    {
        BattleUnitState unit = new(MoverId, new TilePosition(0, 0, 0), move: 1);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CreateState(unit, new BattleUnitState(MoverId, new TilePosition(1, 0, 0), move: 1)));

        Assert.Contains("Duplicate battle unit ID", exception.Message);
    }

    private static void AssertRejectedWithoutMutation(
        BattleState initial,
        BattleTransitionResult result,
        BattleTransitionRejection expectedRejection)
    {
        Assert.False(result.Accepted);
        Assert.Equal(expectedRejection, result.Rejection);
        Assert.Same(initial, result.State);
        Assert.Empty(result.Events);
    }

    private static BattleState CreateState(params BattleUnitState[] units)
    {
        var tiles = Enumerable.Repeat(new BattleTile(0), count: 5 * 5 * 2).ToArray();
        var map = new BattleMapDefinition(width: 5, depth: 5, layerCount: 2, tiles);
        return new BattleState(map, units);
    }
}
