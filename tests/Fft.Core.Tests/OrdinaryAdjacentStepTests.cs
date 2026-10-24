using Fft.Core;
using Xunit;

namespace Fft.Core.Tests;

// Source-derived geometry expectations: move_range.c at
// 611b504d25d4a9b03fab8023d7dbea75fe381d5d; see docs/behavior/adjacent-step-geometry.md.
// No executing reference or original-game observation is claimed.
public sealed class OrdinaryAdjacentStepTests
{
    private static readonly TerrainSurfaceId Ground = new("fixture:terrain/ground");
    private static readonly BattleUnitId Mover = new("fixture:unit/mover");

    [Theory]
    [InlineData(10, 0, 0x00, 13, 0, 0x00, 3, 1, 0, true)]
    [InlineData(10, 0, 0x00, 14, 0, 0x00, 3, 1, 0, false)]
    [InlineData(10, 0, 0x00, 13, 1, 0x25, 3, 1, 0, false)]
    [InlineData(10, 3, 0x52, 10, 0, 0x00, 3, 1, 0, true)]
    [InlineData(10, 0, 0x00, 10, 3, 0x58, 3, 1, 0, true)]
    [InlineData(10, 3, 0x85, 10, 0, 0x00, 3, 0, 1, true)]
    [InlineData(10, 3, 0x25, 10, 0, 0x00, 3, 0, -1, true)]
    [InlineData(10, 0, 0x00, 10, 0, 0x00, 0, 1, 0, true)]
    [InlineData(10, 0, 0x00, 11, 0, 0x00, 0, 1, 0, false)]
    [InlineData(10, 0, 0x00, 17, 0, 0x00, 8, 1, 0, true)]
    [InlineData(10, 0, 0x00, 18, 0, 0x00, 8, 1, 0, false)]
    [InlineData(10, 0, 0x00, 17, 0, 0x00, 255, 1, 0, true)]
    [InlineData(10, 0, 0x00, 18, 0, 0x00, 255, 1, 0, false)]
    public void Source_traced_threshold_and_facing_edge_vectors_work_in_both_directions(
        int ah, int halfA, byte slopeA, int bh, int halfB, byte slopeB, int jump, int dx, int dy, bool accepted)
    {
        var a = Tile(ah, halfA, slopeA);
        var b = Tile(bh, halfB, slopeB);
        Check(a, b, dx, dy);
        Check(b, a, -dx, -dy);

        void Check(BattleTile source, BattleTile destination, int xStep, int yStep)
        {
            var (map, origin, endpoint) = Pair(source, destination, xStep, yStep);
            var state = new BattleState(map, new[] { new BattleUnitState(Mover, origin, Profile(jump)) });
            var result = BattleSimulator.Apply(state, new(Mover, endpoint), BattleMovementPolicy.OrdinaryAdjacentStepPrototype);
            Assert.Equal(accepted, result.Accepted);
            Assert.Equal(origin, state.Units[0].Position);
            Assert.False(state.Units[0].HasMoved);
            if (accepted)
            {
                Assert.NotSame(state, result.State);
                Assert.Equal(endpoint, result.State.Units[0].Position);
                Assert.Equal(new BattleMovementEvent(Mover, origin, endpoint), Assert.Single(result.Events));
            }
            else
            {
                Assert.Same(state, result.State);
                Assert.Equal(BattleTransitionRejection.DestinationNotReachable, result.Rejection);
                Assert.Empty(result.Events);
            }
        }
    }

    [Theory]
    [InlineData(4, BattleTileMovementFlags.None, true)]
    [InlineData(3, BattleTileMovementFlags.None, false)]
    [InlineData(4, BattleTileMovementFlags.Blocked, false)]
    public void Geometry_combines_with_entry_cost_and_intermediate_blockers(int move, BattleTileMovementFlags middleFlags, bool accepted)
    {
        var map = new BattleMapDefinition(3, 1, 1,
            new[] { Tile(10), new BattleTile(Ground, 13, movementFlags: middleFlags), Tile(16) });
        var state = new BattleState(map, new[] { new BattleUnitState(Mover, new(0, 0, 0), Profile(3, move, cost: 2)) });
        var result = BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.OrdinaryAdjacentStepPrototype);
        Assert.Equal(accepted, result.Accepted);
        if (!accepted) { Assert.Same(state, result.State); Assert.Empty(result.Events); }
    }

    [Fact]
    public void Existing_policies_do_not_silently_acquire_geometry()
    {
        var (map, origin, destination) = Pair(Tile(10), Tile(20), 1, 0);
        var state = new BattleState(map, new[] { new BattleUnitState(Mover, origin, Profile(0)) });
        Assert.True(BattleSimulator.Apply(state, new(Mover, destination), BattleMovementPolicy.FlatGridPrototype).Accepted);
        Assert.True(BattleSimulator.Apply(state, new(Mover, destination), BattleMovementPolicy.TerrainCostPrototype).Accepted);
        Assert.False(BattleSimulator.Apply(state, new(Mover, destination), BattleMovementPolicy.OrdinaryAdjacentStepPrototype).Accepted);
    }

    [Theory]
    [InlineData("water")]
    [InlineData("ceiling")]
    [InlineData("corner")]
    [InlineData("steep")]
    [InlineData("overflow")]
    [InlineData("flat-amplitude")]
    [InlineData("mode")]
    [InlineData("traits")]
    [InlineData("size")]
    [InlineData("jump-width")]
    [InlineData("overhead-layer")]
    public void Unsupported_domains_fail_explicitly_without_mutation(string input)
    {
        var tile = input switch
        {
            "water" => new BattleTile(Ground, 10, waterDepth: 1),
            "ceiling" => new BattleTile(Ground, 10, ceilingDepth: 1),
            "corner" => Tile(10, 1, 0x41),
            "steep" => Tile(10, 4, 0x52),
            "overflow" => Tile(int.MaxValue),
            "flat-amplitude" => Tile(10, 1),
            _ => Tile(10),
        };
        var map = input == "overhead-layer" ? new BattleMapDefinition(2, 1, 2, new[] {tile, tile, tile, tile})
            : new BattleMapDefinition(2, 1, 1, new[] {tile, tile});
        var profile = new UnitMovementProfile(1, input == "jump-width" ? 256 : 3, input == "size" ? 4 : 6,
            input == "mode" ? UnitMovementMode.Fly : UnitMovementMode.Normal,
            input == "traits" ? UnitMovementTraits.IgnoreHeight : UnitMovementTraits.None,
            new[] { new KeyValuePair<TerrainSurfaceId, TerrainMovementCost>(Ground, new(1)) });
        var state = new BattleState(map, new[] { new BattleUnitState(Mover, new(0, 0, 0), profile) });
        Assert.Throws<NotSupportedException>(() => BattleSimulator.Apply(state, new(Mover, new(1, 0, 0)), BattleMovementPolicy.OrdinaryAdjacentStepPrototype));
        Assert.Equal(new TilePosition(0, 0, 0), state.Units[0].Position);
        Assert.False(state.Units[0].HasMoved);
    }

    [Fact]
    public void Blocked_other_layer_is_supported_and_occupants_still_block_traversal()
    {
        var blocked = new BattleTile(Ground, 0, movementFlags: BattleTileMovementFlags.Blocked);
        var map = new BattleMapDefinition(3, 1, 2, new[] { Tile(10), Tile(10), Tile(10), blocked, blocked, blocked });
        var state = new BattleState(map, new[] { new BattleUnitState(Mover, new(0, 0, 0), Profile(3, 2)),
            new BattleUnitState(new("fixture:unit/other"), new(1, 0, 0), 0) });
        var result = BattleSimulator.Apply(state, new(Mover, new(2, 0, 0)), BattleMovementPolicy.OrdinaryAdjacentStepPrototype);
        Assert.False(result.Accepted);
        Assert.Same(state, result.State);
        Assert.Empty(result.Events);
    }

    private static BattleTile Tile(int height, int halfHeight = 0, byte slope = 0) =>
        new(Ground, height, halfHeight, slope: TileSlope.FromPackedByte(slope));

    private static UnitMovementProfile Profile(int jump, int move = 1, int cost = 1) =>
        new(move, jump, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            new[] { new KeyValuePair<TerrainSurfaceId, TerrainMovementCost>(Ground, new(cost)) });

    private static (BattleMapDefinition Map, TilePosition Origin, TilePosition Destination) Pair(BattleTile source, BattleTile destination, int dx, int dy)
    {
        int width = dx == 0 ? 1 : 2, depth = dy == 0 ? 1 : 2;
        var origin = new TilePosition(dx < 0 ? 1 : 0, dy < 0 ? 1 : 0, 0);
        var endpoint = new TilePosition(origin.X + dx, origin.Y + dy, 0);
        var tiles = new BattleTile[2];
        tiles[origin.Y * width + origin.X] = source;
        tiles[endpoint.Y * width + endpoint.X] = destination;
        return (new BattleMapDefinition(width, depth, 1, tiles), origin, endpoint);
    }
}
