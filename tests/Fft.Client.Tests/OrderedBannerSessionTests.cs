using System.Security.Cryptography;
using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Tests.Support;
using Zodiac.Content;
using Zodiac.Framework;
using Zodiac.Framework.Events;
using Xunit;

namespace Fft.Client.Tests;

public sealed class OrderedBannerSessionTests
{
    private static (string Events, string Artwork) Roots()
    {
        string root = Path.Combine(Path.GetTempPath(), "fft-ordered-session-" + Guid.NewGuid().ToString("N"));
        string events = Path.Combine(root, "events"), artwork = Path.Combine(root, "artwork");
        AuthoredOrderedBannerEvent.Write(events); AuthoredGeneralBanner.Write(artwork);
        return (events, artwork);
    }

    [Fact]
    public void BOP_028_prepared_session_executes_and_renders_independently_of_draw_schedule()
    {
        using var evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "banner-opcode-admission-contract-v1.json")));
        Assert.Equal("BOP-028", evidence.RootElement.GetProperty("scope").GetProperty("contentHostCaseIds")[0].GetString());
        var roots = Roots();
        var session = OrderedBannerSession.Load(LooseAssetStore.Load(roots.Events), AuthoredOrderedBannerEvent.Id, LooseAssetStore.Load(roots.Artwork));
        var a = Execute(session, 0);
        Assert.Equal(a, Execute(session, 1));
        Assert.Equal(a, Execute(session, 3));
        Assert.Equal("synthetic", session.Definition.Source.Kind);
    }

    private static (string Final, string Trace) Execute(OrderedBannerSession session, int renders)
    {
        var run = session.Start(new("sample:scene/ordered-preview"), new("sample:consumer/a"));
        Assert.True(run.Accepted, run.Diagnostic);
        var state = run.State;
        var trace = new List<string> { JsonSerializer.Serialize(run.Trace) };
        var snapshots = new List<(GeneralBannerState State, string Frozen)>();
        for (int tick = 0; tick <= 92; tick++)
        {
            Assert.Equal(tick, state.Event.Tick);
            Assert.Equal(tick >= 91, state.Banner!.IsTerminal);
            Assert.Equal(tick >= 92, state.Event.Exited);
            for (int i = 0; i < renders; i++)
            {
                string before = JsonSerializer.Serialize(state);
                var draws = GeneralBannerPresentation.Project(state.Banner);
                byte[] rgb = GeneralBannerPresentation.Compose(session.Artwork, draws);
                byte[] rgb5 = FiveBitGeneralBannerPresentation.Compose(session.Artwork,
                    [.. draws.Select(d => new BannerRasterDraw(d, new(false, 0, 0)))]);
                Assert.Equal(256 * 240 * 4, rgb.Length);
                Assert.Equal(rgb5, FiveBitGeneralBannerPresentation.Compose(session.Artwork,
                    [.. draws.Select(d => new BannerRasterDraw(d, new(false, 0, 0)))]));
                Assert.Equal(before, JsonSerializer.Serialize(state));
                snapshots.Add((state.Banner, JsonSerializer.Serialize(state.Banner)));
            }
            if (tick == 92) break;
            var next = OrderedBannerExecutor.AdvanceTick(state, false, [], 2);
            Assert.True(next.Accepted, next.Diagnostic);
            trace.Add(JsonSerializer.Serialize(next.Trace));
            state = next.State;
        }
        Assert.Equal(7, state.Event.Variables.V3);
        Assert.Single(state.Event.Tasks.Tasks);
        Assert.Equal(1, state.Event.Tasks.Tasks[0].Revision);
        foreach (var (snapshot, frozen) in snapshots) Assert.Equal(frozen, JsonSerializer.Serialize(snapshot));
        return (JsonSerializer.Serialize(state), string.Join('\n', trace));
    }

    [Theory]
    [InlineData("event-hash")] [InlineData("artwork-hash")] [InlineData("missing-artwork")]
    [InlineData("invalid-indices")] [InlineData("wrong-artwork")]
    public void Failed_readiness_cannot_return_a_session_or_publish_any_scene(string fault)
    {
        var roots = Roots();
        string artworkRoot = roots.Artwork;
        if (fault == "event-hash") File.AppendAllText(Path.Combine(roots.Events, "event.json"), " ");
        if (fault == "artwork-hash") File.AppendAllText(Path.Combine(roots.Artwork, "banner.json"), " ");
        if (fault == "missing-artwork") File.Delete(Path.Combine(roots.Artwork, "indices.bin"));
        if (fault is "invalid-indices" or "wrong-artwork") artworkRoot += "-invalid";
        if (fault == "invalid-indices") AuthoredGeneralBanner.Write(artworkRoot, indices: Enumerable.Repeat((byte)16, 51200).ToArray());
        if (fault == "wrong-artwork") AuthoredGeneralBanner.Write(artworkRoot, AuthoredGeneralBanner.Definition() with { Id = "sample:banner/wrong" });
        OrderedBannerSession? session = null;
        var failure = Record.Exception(() => session = OrderedBannerSession.Load(LooseAssetStore.Load(roots.Events), AuthoredOrderedBannerEvent.Id, LooseAssetStore.Load(artworkRoot)));
        Assert.IsType(fault == "missing-artwork" ? typeof(FileNotFoundException) : fault == "wrong-artwork" ? typeof(KeyNotFoundException) : typeof(InvalidDataException), failure);
        Assert.Null(session);
    }

    [Theory]
    [InlineData("missing")][InlineData("duplicate")][InlineData("tutorial")][InlineData("old-mode")][InlineData("dither")]
    public void Host_options_reject_ambiguous_or_unsupported_inputs(string fault)
    {
        var args = new List<string> { "--ordered-banner", "--event-root", "event", "--event-id", AuthoredOrderedBannerEvent.Id.Value, "--artwork-root", "art" };
        if (fault == "missing") args.RemoveRange(5, 2);
        if (fault == "duplicate") args.AddRange(["--event-id", AuthoredOrderedBannerEvent.Id.Value]);
        if (fault == "tutorial") args.AddRange(["--tutorial", "true"]);
        if (fault == "old-mode") args[0] = "--general-banner";
        if (fault == "dither") args.AddRange(["--dither", "off"]);
        Assert.Throws<ArgumentException>(() => OrderedBannerPreviewOptions.Parse(args.ToArray()));
    }

    [Fact]
    public void Ordered_host_precision_is_explicit_and_legacy_options_remain_closed()
    {
        string[] args = ["--ordered-banner", "--event-root", "event", "--event-id", AuthoredOrderedBannerEvent.Id.Value, "--artwork-root", "art", "--precision", "five-bit", "--dither", "off"];
        var parsed = OrderedBannerPreviewOptions.Parse(args);
        Assert.True(parsed.Presentation.FiveBit);
        Assert.False(parsed.Presentation.Dither);
        Assert.Throws<ArgumentException>(() => BannerPreviewOptions.Parse(args));
    }
}
