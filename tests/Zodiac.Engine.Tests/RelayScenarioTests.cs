using Samples.Relay;
using Xunit;

namespace Zodiac.Engine.Tests;

public sealed class RelayScenarioTests
{
    [Fact]
    public void Courier_delivers_by_two_legal_moves_and_rejects_shortcut_without_mutation()
    {
        var initial = RelayScenario.Initial;
        var shortcut = RelayScenario.Move(initial, new(3, 1, 0));
        Assert.False(shortcut.Accepted);
        Assert.Same(initial, shortcut.State);
        var halfway = RelayScenario.Move(initial, new(2, 0, 0));
        Assert.True(halfway.Accepted);
        Assert.Equal(new GridPosition(0, 0, 0), initial.Courier);
        Assert.Equal(0, halfway.State.Deliveries);
        var delivery = RelayScenario.Move(halfway.State, new(3, 1, 0));
        Assert.True(delivery.Accepted);
        Assert.Equal(new RelayScenario.State(new(3, 1, 0), 1), delivery.State);
        Assert.Equal(delivery, RelayScenario.Move(halfway.State, new(3, 1, 0)));
    }
}
