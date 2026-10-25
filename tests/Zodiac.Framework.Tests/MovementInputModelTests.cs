using Zodiac.Framework;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class MovementInputModelTests
{
    [Fact]
    public void BattleTile_preserves_movement_geometry_and_flags()
    {
        var surface = new TerrainSurfaceId("base:terrain/grassland");
        var tile = new BattleTile(
            surface,
            height: 4,
            halfHeight: 35,
            waterDepth: 8,
            slope: new TileSlope(east: 1, west: 2, south: 0, north: 3),
            ceilingDepth: 40,
            movementFlags: BattleTileMovementFlags.Blocked | BattleTileMovementFlags.CannotStop);

        Assert.Equal(surface, tile.Surface);
        Assert.Equal(4, tile.Height);
        Assert.Equal(35, tile.HalfHeight);
        Assert.Equal(8, tile.WaterDepth);
        Assert.Equal(new TileSlope(1, 2, 0, 3), tile.Slope);
        Assert.Equal(40, tile.CeilingDepth);
        Assert.True(tile.IsBlocked);
        Assert.False(tile.IsUntargetable);
        Assert.True(tile.CannotStop);
    }

    [Fact]
    public void TileSlope_round_trips_every_original_packed_byte()
    {
        for (int value = byte.MinValue; value <= byte.MaxValue; value++)
        {
            TileSlope slope = TileSlope.FromPackedByte((byte)value);

            Assert.Equal((byte)value, slope.ToPackedByte());
        }
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(4, 0, 0, 0)]
    public void TileSlope_rejects_edges_outside_the_original_two_bit_encoding(
        int east,
        int west,
        int south,
        int north)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileSlope(east, west, south, north));
    }

    [Fact]
    public void UnitMovementProfile_copies_custom_terrain_costs_without_a_fixed_table_limit()
    {
        var costs = Enumerable.Range(0, 100).ToDictionary(
            index => new TerrainSurfaceId($"mod:surface/{index}"),
            index => index == 99 ? TerrainMovementCost.Blocked : new TerrainMovementCost(index + 1));

        var profile = new UnitMovementProfile(
            move: 7,
            jump: 5,
            bodyHeight: 6,
            mode: UnitMovementMode.Normal,
            traits: UnitMovementTraits.AnyGround | UnitMovementTraits.IgnoreHeight,
            terrainCosts: costs);
        costs.Clear();

        Assert.Equal(7, profile.Move);
        Assert.Equal(5, profile.Jump);
        Assert.Equal(6, profile.BodyHeight);
        Assert.Equal(UnitMovementMode.Normal, profile.Mode);
        Assert.Equal(UnitMovementTraits.AnyGround | UnitMovementTraits.IgnoreHeight, profile.Traits);
        Assert.Equal(100, profile.TerrainCosts.Count);
        Assert.True(profile.TryGetTerrainCost(new TerrainSurfaceId("mod:surface/98"), out TerrainMovementCost cost));
        Assert.Equal(new TerrainMovementCost(99), cost);
        Assert.True(profile.TryGetTerrainCost(new TerrainSurfaceId("mod:surface/99"), out TerrainMovementCost blocked));
        Assert.True(blocked.IsBlocked);
    }

    [Fact]
    public void UnitMovementProfile_rejects_default_invalid_terrain_cost()
    {
        KeyValuePair<TerrainSurfaceId, TerrainMovementCost>[] costs =
        [new(new TerrainSurfaceId("base:terrain/grassland"), default)];

        Assert.Throws<ArgumentException>(() => new UnitMovementProfile(
            move: 4,
            jump: 3,
            bodyHeight: 6,
            mode: UnitMovementMode.Normal,
            traits: UnitMovementTraits.None,
            terrainCosts: costs));
    }

    [Fact]
    public void BattleUnitState_keeps_effective_movement_profile_separate_from_map_position()
    {
        var profile = new UnitMovementProfile(
            move: 4,
            jump: 3,
            bodyHeight: 6,
            mode: UnitMovementMode.Float,
            traits: UnitMovementTraits.None,
            terrainCosts: Array.Empty<KeyValuePair<TerrainSurfaceId, TerrainMovementCost>>());
        var unit = new BattleUnitState(new BattleUnitId("unit:ramza"), new TilePosition(2, 1, 0), profile);

        Assert.Same(profile, unit.MovementProfile);
        Assert.Equal(4, unit.Move);
        Assert.Equal(new TilePosition(2, 1, 0), unit.Position);
    }
}
