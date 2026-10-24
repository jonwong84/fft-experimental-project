using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Core.Events;
using Fft.Tests.Support;
using Xunit;

namespace Fft.Client.Tests;

public sealed class ObservedBannerCircleTests
{
    [Theory]
    [InlineData("reveal", 5, 5)][InlineData("reveal", 6, 5)][InlineData("reveal", 7, 5)]
    [InlineData("hold", 21, 21)][InlineData("hold", 22, 21)][InlineData("hold", 23, 21)]
    [InlineData("hide", 81, 81)][InlineData("hide", 82, 81)]
    public void OriginalPressMatchesObservedProgressAndDraws(string phase, int tick, int pressTick)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            $"Fixtures/original-banner-circle-{phase}-v1.json")));
        var observed = document.RootElement.GetProperty("observations").EnumerateArray()
            .Single(x => x.GetProperty("logicalTick").GetInt32() == tick);
        var state = GeneralBannerPreview.Start(AuthoredGeneralBanner.Loaded());
        while (state.Event.Tick < tick)
        {
            var result = BannerSceneExecutor.AdvanceTick(state, state.Event.Tick == pressTick, [], 1);
            Assert.True(result.Accepted, result.Diagnostic);
            state = result.State;
        }
        var banner = Assert.IsType<GeneralBannerState>(state.Banner);
        Assert.Equal(observed.GetProperty("phase").GetString(), banner.Phase.ToString());
        Assert.Equal(observed.GetProperty("revealCompleted").GetInt32(), banner.RevealCompleted);
        Assert.Equal(observed.GetProperty("holdCompleted").GetInt32(), banner.HoldCompleted);
        Assert.Equal(observed.GetProperty("hideCompleted").GetInt32(), banner.HideCompleted);
        Assert.Equal(observed.GetProperty("isTerminal").GetBoolean(), banner.IsTerminal);
        Assert.Equal(observed.GetProperty("exitCause").GetString(), banner.ExitCause?.ToString());
        Assert.Equal(observed.GetProperty("draws").Deserialize<BannerDraw[]>(),
            GeneralBannerPresentation.Project(banner).ToArray());
    }
}
