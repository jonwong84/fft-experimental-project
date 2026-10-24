using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Core.Events;
using Fft.Tests.Support;
using Xunit;

namespace Fft.Client.Tests;

public sealed class ObservedBannerRevealTests
{
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    [InlineData(6)][InlineData(7)][InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)]
    [InlineData(13)][InlineData(14)][InlineData(15)][InlineData(16)]
    public void OriginalEarlyTaskCountersAndOrderedCropsMatchManagedReplay(int tick)
    {
        // Only metadata enters this regression; original code, media and loader are not invoked.
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            "Fixtures/early-banner-reveal-v1.json")));
        var observed = document.RootElement.GetProperty("observations").EnumerateArray()
            .Single(x => x.GetProperty("logicalTick").GetInt32() == tick);
        var state = GeneralBannerPreview.Start(AuthoredGeneralBanner.Loaded());
        while (state.Event.Tick < tick)
        {
            var result = BannerSceneExecutor.AdvanceTick(state, false, [], 1);
            Assert.True(result.Accepted, result.Diagnostic);
            state = result.State;
        }
        var banner = Assert.IsType<GeneralBannerState>(state.Banner);
        Assert.Equal(observed.GetProperty("phase").GetString(), banner.Phase.ToString());
        Assert.Equal(observed.GetProperty("revealCompleted").GetInt32(), banner.RevealCompleted);
        Assert.Equal(0, banner.HoldCompleted);
        Assert.Equal(0, banner.HideCompleted);
        Assert.Equal(observed.GetProperty("draws").Deserialize<BannerDraw[]>(),
            GeneralBannerPresentation.Project(banner).ToArray());
        Assert.False(document.RootElement.GetProperty("originalEvent401Verified").GetBoolean());
    }
}
