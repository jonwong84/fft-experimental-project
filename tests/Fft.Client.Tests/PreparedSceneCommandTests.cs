using Fft.Client.Presentation;
using Xunit;

namespace Fft.Client.Tests;

public sealed class PreparedSceneCommandTests
{
    [Theory]
    [InlineData()]
    [InlineData("--prepared-scene")]
    [InlineData("--prepared-scene", "--map-root", "x")]
    [InlineData("wrong", "--map-root", "x", "--pose-root", "y")]
    [InlineData("--prepared-scene", "--map-root", "x", "--unknown", "y")]
    [InlineData("--prepared-scene", "--map-root", "x", "--pose-root", "y", "--unknown", "z")]
    [InlineData("--prepared-scene", "--map-root", "", "--pose-root", "y")]
    [InlineData("--prepared-scene", "--map-root", "x", "--pose-root", "--check", "--check", "z")]
    [InlineData("--prepared-scene", "--map-root", "x", "--pose-root", "y", "--check", " ")]
    public void Invalid_scene_command_returns_usage_without_loading_assets_or_opening_a_window(params string[] args) =>
        Assert.Equal(2, WindowsPreparedSceneHost.Run(args));
}
