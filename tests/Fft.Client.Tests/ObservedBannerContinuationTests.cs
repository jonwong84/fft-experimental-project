using System.Text.Json;
using Fft.Client.Presentation;
using Zodiac.Framework.Events;
using Fft.Tests.Support;
using Xunit;

namespace Fft.Client.Tests;

public sealed class ObservedBannerContinuationTests
{
    [Theory]
    [InlineData(18)][InlineData(19)][InlineData(20)][InlineData(21)][InlineData(29)][InlineData(79)]
    [InlineData(80)][InlineData(81)][InlineData(82)][InlineData(83)][InlineData(84)][InlineData(85)]
    [InlineData(86)][InlineData(87)][InlineData(88)][InlineData(89)][InlineData(90)][InlineData(91)]
    public void OriginalSuspendedTaskCountersAndCropsMatchManagedReplay(int tick)
    {
        // Metadata only: original state/code/artwork are never runtime or test inputs.
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
            "Fixtures/original-banner-continuation-v1.json")));
        var root = document.RootElement;
        var observed = root.GetProperty("observations").EnumerateArray().Single(x => x.GetProperty("logicalTick").GetInt32() == tick);
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
        Assert.Equal(observed.GetProperty("holdCompleted").GetInt32(), banner.HoldCompleted);
        Assert.Equal(observed.GetProperty("hideCompleted").GetInt32(), banner.HideCompleted);
        Assert.Equal(observed.GetProperty("phase").GetString() == "Completed", banner.IsTerminal);
        var expectedDraws = observed.GetProperty("draws").Deserialize<BannerDraw[]>()!;
        Assert.Equal(expectedDraws, GeneralBannerPresentation.Project(banner).ToArray());
        Assert.False(root.GetProperty("fullRevealObserved").GetBoolean());
        Assert.False(root.GetProperty("originalEvent401Verified").GetBoolean());
    }
}
