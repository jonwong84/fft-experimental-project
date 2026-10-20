using Fft.Core;
using Xunit;

namespace Fft.Core.Tests;

public sealed class TerrainAwareMovementRangeTests
{
    private static readonly TerrainSurfaceId Plain = new("fft:terrain/plain");
    private static readonly TerrainSurfaceId Rough = new("fft:terrain/rough");

    [Fact]
    public void Calculate_charges_cost_of_entering_each_destination_and_uses_cheaper_routes()
    {
        var map = CreateMap(3, 2,
        [
            Tile(Plain), Tile(Rough), Tile(Plain),
            Tile(Plain), Tile(Plain), Tile(Plain),
        ]);
        UnitMovementProfile profile = CreateProfile(move: 4, plainCost: 1, roughCost: 4);

        IReadOnlySet<TilePosition> reachable = TerrainAwareMovementRange.Calculate(
            map, new TilePosition(0, 0, 0), profile);

        Assert.True(new HashSet<TilePosition>
        {
            new(1, 0, 0), // rough costs 4 and consumes the full Move allowance
            new(0, 1, 0),
            new(1, 1, 0),
            new(2, 1, 0),
            new(2, 0, 0), // reached by the lower-cost route around rough terrain
        }.SetEquals(reachable));
    }

    [Fact]
    public void Calculate_can_propagate_through_tiles_that_cannot_be_final_destinations()
    {
        var map = CreateMap(4, 1,
        [
            Tile(Plain),
            Tile(Plain, BattleTileMovementFlags.CannotStop),
            Tile(Plain, BattleTileMovementFlags.Untargetable),
            Tile(Plain),
        ]);
        UnitMovementProfile profile = CreateProfile(move: 3, plainCost: 1, roughCost: 1);

        IReadOnlySet<TilePosition> reachable = TerrainAwareMovementRange.Calculate(
            map, new TilePosition(0, 0, 0), profile);

        Assert.Equal(new[] { new TilePosition(3, 0, 0) }, reachable);
    }

    [Fact]
    public void Calculate_does_not_enter_tiles_blocked_by_tile_flag_or_surface_cost()
    {
        var map = CreateMap(3, 1,
        [
            Tile(Plain),
            Tile(Plain, BattleTileMovementFlags.Blocked),
            Tile(Rough),
        ]);
        UnitMovementProfile profile = CreateProfile(move: 4, plainCost: 1, roughCost: 1, roughBlocked: true);

        IReadOnlySet<TilePosition> reachable = TerrainAwareMovementRange.Calculate(
            map, new TilePosition(0, 0, 0), profile);

        Assert.Empty(reachable);
    }

    [Fact]
    public void Calculate_rejects_a_surface_missing_from_the_effective_profile()
    {
        var map = CreateMap(2, 1, [Tile(Plain), Tile(Rough)]);
        UnitMovementProfile profile = CreateProfile(move: 2, plainCost: 1, roughCost: 1, includeRough: false);

        Assert.Throws<InvalidOperationException>(() => TerrainAwareMovementRange.Calculate(
            map, new TilePosition(0, 0, 0), profile));
    }

    [Fact]
    public void Calculate_excludes_origin_and_rejects_invalid_origin_or_move_profile()
    {
        var map = CreateMap(1, 1, [Tile(Plain)]);
        UnitMovementProfile profile = CreateProfile(move: 0, plainCost: 1, roughCost: 1);

        Assert.Empty(TerrainAwareMovementRange.Calculate(map, new TilePosition(0, 0, 0), profile));
        Assert.Throws<ArgumentOutOfRangeException>(() => TerrainAwareMovementRange.Calculate(
            map, new TilePosition(1, 0, 0), profile));
    }

    private static BattleTile Tile(TerrainSurfaceId surface, BattleTileMovementFlags flags = BattleTileMovementFlags.None) =>
        new(surface, height: 0, movementFlags: flags);

    private static UnitMovementProfile CreateProfile(
        int move,
        int plainCost,
        int roughCost,
        bool roughBlocked = false,
        bool includeRough = true)
    {
        var costs = new List<KeyValuePair<TerrainSurfaceId, TerrainMovementCost>>
        {
            KeyValuePair.Create(Plain, new TerrainMovementCost(plainCost)),
        };
        if (includeRough)
        {
            costs.Add(KeyValuePair.Create(Rough,
                roughBlocked ? TerrainMovementCost.Blocked : new TerrainMovementCost(roughCost)));
        }

        return new UnitMovementProfile(move, jump: 0, bodyHeight: 6, UnitMovementMode.Normal,
            UnitMovementTraits.None, costs);
    }

    private static BattleMapDefinition CreateMap(int width, int depth, IReadOnlyList<BattleTile> tiles) =>
        new(width, depth, layerCount: 1, tiles);
}
