using Zodiac.Framework;
using Samples.Relay;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class EngineIndependenceTests
{
    [Fact]
    public void Changing_FFT_movement_profile_changes_FFT_range_but_not_neutral_delivery()
    {
        var before = Deliver();
        var small = UnitMovementProfile.ForFlatGridPrototype(0);
        var large = UnitMovementProfile.ForFlatGridPrototype(2);
        var map = new BattleMapDefinition(3, 1, 1, Enumerable.Repeat(new BattleTile(0), 3).ToArray());
        Assert.Empty(FlatGridMovementRange.Calculate(map, new(0, 0, 0), small.Move));
        Assert.Equal(2, FlatGridMovementRange.Calculate(map, new(0, 0, 0), large.Move).Count);
        Assert.Equal(before, Deliver());
        Assert.Equal(1, before.Deliveries);
    }

    private static RelayScenario.State Deliver() =>
        RelayScenario.Move(RelayScenario.Move(RelayScenario.Initial, new(2, 0, 0)).State, new(3, 1, 0)).State;
}
