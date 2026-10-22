using Fft.Core;
using Xunit;

namespace Fft.Core.Tests;

public sealed class BattleRandomStateTests
{
    private static readonly BattleUnitId Mover = new("fixture:unit/mover");
    private static readonly BattleUnitId Other = new("fixture:unit/other");

    [Theory]
    [InlineData(BattleMovementPolicy.FlatGridPrototype)]
    [InlineData(BattleMovementPolicy.TerrainCostPrototype)]
    [InlineData(BattleMovementPolicy.OrdinaryAdjacentStepPrototype)]
    public void Movement_replays_preserve_full_rng_state_and_count_without_mutating_inputs(BattleMovementPolicy policy)
    {
        var rng = new FftRandomState(0xfedcba98u, 0xfedcba9876543210UL);
        BattleState initial = State(rng);
        BattleTransitionResult[] first = Replay(initial, policy);
        BattleTransitionResult[] repeated = Replay(initial, policy);

        Assert.Equal(new[] { false, false, false, true, false, true }, first.Select(result => result.Accepted));
        Assert.Equal(new[] { BattleTransitionRejection.UnknownUnit, BattleTransitionRejection.DestinationOccupied,
            BattleTransitionRejection.DestinationNotReachable, BattleTransitionRejection.None,
            BattleTransitionRejection.UnitHasAlreadyMoved, BattleTransitionRejection.None }, first.Select(result => result.Rejection));
        for (int i = 0; i < first.Length; i++)
        {
            Assert.Equal(rng, first[i].State.RngState);
            Assert.Equal(rng, repeated[i].State.RngState);
            Assert.Equal(first[i].Accepted, repeated[i].Accepted);
            Assert.Equal(first[i].Rejection, repeated[i].Rejection);
            Assert.Equal(first[i].State.Units.ToArray(), repeated[i].State.Units.ToArray());
            Assert.Equal(first[i].Events.ToArray(), repeated[i].Events.ToArray());
        }
        Assert.Equal(rng, initial.RngState);
        Assert.False(initial.Units[0].HasMoved);
        Assert.False(initial.Units[1].HasMoved);
        Assert.Equal(new TilePosition(0, 0, 0), initial.Units[0].Position);
        Assert.Equal(new TilePosition(2, 0, 0), initial.Units[1].Position);
    }

    [Theory]
    [InlineData(BattleMovementPolicy.FlatGridPrototype)]
    [InlineData(BattleMovementPolicy.TerrainCostPrototype)]
    [InlineData(BattleMovementPolicy.OrdinaryAdjacentStepPrototype)]
    public void Movement_does_not_attempt_a_draw_even_when_counter_is_exhausted(BattleMovementPolicy policy)
    {
        var rng = new FftRandomState(uint.MaxValue, ulong.MaxValue);
        foreach (BattleTransitionResult result in Replay(State(rng), policy))
            Assert.Equal(rng, result.State.RngState);
    }

    [Fact]
    public void Existing_unseeded_movement_keeps_null_rng_state()
    {
        foreach (BattleTransitionResult result in Replay(State(null), BattleMovementPolicy.FlatGridPrototype))
            Assert.Null(result.State.RngState);
    }

    private static BattleTransitionResult[] Replay(BattleState initial, BattleMovementPolicy policy)
    {
        MoveUnitCommand[] commands =
        [
            new(new BattleUnitId("fixture:unit/unknown"), new(1, 1, 0)),
            new(Mover, new(2, 0, 0)),
            new(Mover, new(3, 0, 0)),
            new(Mover, new(1, 0, 0)),
            new(Mover, new(1, 1, 0)),
            new(Other, new(2, 1, 0)),
        ];
        var results = new List<BattleTransitionResult>();
        BattleState state = initial;
        foreach (MoveUnitCommand command in commands)
        {
            BattleUnitState[] unitsBefore = state.Units.ToArray();
            FftRandomState? rngBefore = state.RngState;
            BattleTransitionResult result = BattleSimulator.Apply(state, command, policy);
            Assert.Equal(unitsBefore, state.Units.ToArray());
            Assert.Equal(rngBefore, state.RngState);
            Assert.Equal(rngBefore, result.State.RngState);
            Assert.Same(state.Map, result.State.Map);
            if (result.Accepted)
            {
                Assert.NotSame(state, result.State);
                Assert.Single(result.Events);
            }
            else
            {
                Assert.Same(state, result.State);
                Assert.Empty(result.Events);
            }
            results.Add(result);
            state = result.State;
        }
        return results.ToArray();
    }

    private static BattleState State(FftRandomState? rng)
    {
        var surface = new TerrainSurfaceId("fixture:terrain/ground");
        var map = new BattleMapDefinition(3, 2, 1, Enumerable.Repeat(new BattleTile(surface, 0), 6).ToArray());
        var profile = new UnitMovementProfile(2, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [new KeyValuePair<TerrainSurfaceId, TerrainMovementCost>(surface, new(1))]);
        return new BattleState(map,
            [new BattleUnitState(Mover, new(0, 0, 0), profile), new BattleUnitState(Other, new(2, 0, 0), profile)], rng);
    }
}
