using System.Collections.Immutable;
using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Tests.Support;
using Zodiac.Content;
using Zodiac.Framework.Events;
using Xunit;

namespace Fft.Client.Tests;

public sealed class ReadyPresentationTests
{
    private static readonly ReadyRasterState Raster = ReadyRasterState.Selected;
    private static string Temp() => Path.Combine(Path.GetTempPath(), "fft-ready-session-" + Guid.NewGuid().ToString("N"));
    private static ReadySession Session(int hold = 1)
    {
        string root = Temp(); AuthoredReady.WriteEvent(Path.Combine(root, "event"), AuthoredReady.Event(hold)); AuthoredReady.WriteArtwork(Path.Combine(root, "art"));
        return ReadySession.Load(LooseAssetStore.Load(Path.Combine(root, "event")), AuthoredReady.Id, LooseAssetStore.Load(Path.Combine(root, "art")));
    }
    private static ReadySceneState Start(int hold = 1) => Session(hold).Start(new("sample:scene/ready"), new("sample:consumer/ready")).State;
    private static ReadySceneState Through(ReadySceneState state, int tick)
    {
        while (state.Event.Tick < tick)
        {
            var next = ReadySceneExecutor.AdvanceTick(state, false, [], 8192); Assert.True(next.Accepted, next.Diagnostic); state = next.State;
        }
        return state;
    }
    private static byte[] Background(byte r = 248, byte g = 120, byte b = 8)
    {
        var bytes = new byte[256 * 240 * 4];
        for (int at = 0; at < bytes.Length; at += 4) { bytes[at] = r; bytes[at + 1] = g; bytes[at + 2] = b; bytes[at + 3] = 255; }
        return bytes;
    }
    public static IEnumerable<object[]> Cases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/ready-presentation-contract-v1.json")));
        Assert.Equal(PreparedReadyArtwork.ReferenceRevision, doc.RootElement.GetProperty("referenceRevision").GetString());
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int index = int.Parse(c.GetProperty("id").GetString()![4..]);
            if (index is >= 6 and <= 21 or >= 26 and <= 32) yield return [c.GetProperty("id").GetString()!, c.Clone()];
        }
    }
    [Theory, MemberData(nameof(Cases))]
    public void RPR_Client_expectations_execute(string id, JsonElement c)
    {
        var given = c.GetProperty("given"); var expected = c.GetProperty("expected");
        switch (id)
        {
            case "RPR-006":
                Assert.Equal(Ints(expected, "scaledChannels"), Ints(given, "channels").Zip(Ints(given, "scales"), ReadyPresentation.ScaleChannel)); break;
            case "RPR-007":
            case "RPR-008":
                {
                    var corners = Ints(given, "corners");
                    var actual = given.GetProperty("samples").EnumerateArray().Select(p => ReadyPresentation.VertexChannel(corners[0], corners[1], corners[2], corners[3], p[0].GetInt32(), p[1].GetInt32(), given.GetProperty("width").GetInt32(), given.GetProperty("height").GetInt32()));
                    Assert.Equal(Ints(expected, "channels"), actual); break;
                }
            case "RPR-009":
                {
                    var v = Ints(expected, "corners");
                    Assert.Equal(Ints(expected, "channels"), Ints(given, "columns").Select(x => ReadyPresentation.VertexChannel(v[0], v[1], v[2], v[3], x, given.GetProperty("row").GetInt32(), 16, 24)));
                    break;
                }
            case "RPR-010":
                {
                    var art = Flat(31); var before = Background(0, 0, 0);
                    var output = ReadyPresentation.ComposeOver(art, [new(0, 128, Raster)], before);
                    int changed = Enumerable.Range(0, 256 * 240).Count(i => output[i * 4] != before[i * 4]);
                    Assert.Equal(expected.GetProperty("coveredPixels").GetInt32(), changed);
                    Assert.Equal(248, output[((40 + 23) * 256 + 8 + 15) * 4]);
                    Assert.Equal(0, output[((40 + 24) * 256 + 8 + 16) * 4]); break;
                }
            case "RPR-011":
                {
                    var art = Flat(31); var indices = new byte[51200]; indices[47 * 256 + 64] = 1;
                    var def = art.Definition with { Glyphs = [.. art.Definition.Glyphs.Select(g => g with { U = 48, V = 24, Width = 17 })] };
                    var output = ReadyPresentation.ComposeOver(art with { Definition = def, Indices = [.. indices] }, [new(0, 128, Raster)], Background(0, 0, 0));
                    Assert.Equal(248, output[((40 + 23) * 256 + 8 + 16) * 4]);
                    Assert.Equal(1, Enumerable.Range(0, 256 * 240).Count(i => output[i * 4] != 0)); break;
                }
            case "RPR-012":
            case "RPR-013":
            case "RPR-014":
                {
                    ushort word = (ushort)given.GetProperty("paletteWord").GetInt32(); var rgb = Ints(given, "background5");
                    var before = Background((byte)(rgb[0] << 3), (byte)(rgb[1] << 3), (byte)(rgb[2] << 3)); var art = Flat(word);
                if (id == "RPR-012") art = art with { Indices = [.. Enumerable.Repeat((byte)given.GetProperty("paletteIndex").GetInt32(), 51200)] };
                    if (id == "RPR-013") art = art with { Indices = [.. new byte[51200]], Palette = [31, 0, .. new byte[30]] };
                    var output = ReadyPresentation.ComposeOver(art, [new(0, 128, Raster)], before);
                    int at = (40 * 256 + 8) * 4;
                    Assert.Equal(Ints(expected, "output5"), output.AsSpan(at, 3).ToArray().Select(b => (int)b >> 3));
                    if (id == "RPR-012") Assert.Equal(before, output);
                    break;
                }
            case "RPR-015":
                Assert.Equal(Ints(expected, "output5"), given.GetProperty("backgroundSourcePairs").EnumerateArray().Select(p => ReadyPresentation.Blend5(p[0].GetInt32(), p[1].GetInt32(), true))); break;
            case "RPR-016":
                {
                    var actual = given.GetProperty("textureColorPairs").EnumerateArray().Select(p => ReadyPresentation.Shade5(p[0].GetInt32(), p[1].GetInt32())).ToArray();
                    Assert.Equal(Ints(expected, "source5"), actual); Assert.Equal(Ints(expected, "scanout8"), actual.Select(v => v << 3)); break;
                }
            case "RPR-017":
                {
                    foreach (var bad in new[] { Raster with { DitherEnabled = true }, Raster with { MaskSet = true }, Raster with { MaskEvaluate = true }, Raster with { TextureWindowX = 1 }, Raster with { Policy = "rgb8" } })
                        Assert.Throws<ArgumentException>(() => ReadyPresentation.Compose(AuthoredReady.Loaded(), [new(0, 128, bad)]));
                    Assert.Throws<ArgumentException>(() => ReadyPresentation.Compose(AuthoredReady.Loaded(), [new(0, 128, null!)]));
                    Assert.Equal(ReadyPresentation.Compose(AuthoredReady.Loaded(), [new(0, 128, Raster)]), ReadyPresentation.Compose(AuthoredReady.Loaded(), [new(0, 128, Raster with { FramebufferY = 240 })])); break;
                }
            case "RPR-018":
                Assert.Equal(Ints(expected, "drawOrder"), ReadyPresentation.Project(Start().Ready, Raster).Select(d => d.GlyphIndex)); break;
            case "RPR-019":
                {
                    Assert.All(ReadyPresentation.Project(Start(0).Ready, Raster), d => Assert.Equal(given.GetProperty("hold0BirthScale").GetInt32(), d.Scale));
                    var state = Start(60); Assert.All(ReadyPresentation.Project(state.Ready, Raster), d => Assert.Equal(given.GetProperty("hold60BirthScale").GetInt32(), d.Scale));
                    state = ReadySceneExecutor.AdvanceTick(state, true, [], 8192).State;
                    var draws = ReadyPresentation.Project(state.Ready, Raster);
                    Assert.Equal(expected.GetProperty("drawsPerNonemptySnapshot").GetInt32(), draws.Length);
                    Assert.All(draws, d => Assert.Equal(given.GetProperty("earlyReleaseScale").GetInt32(), d.Scale)); break;
                }
            case "RPR-020":
                {
                    var state = Start(1); var ticks = Ints(given, "inspectTicks"); var counts = new List<int>(); var scales = new List<int?>();
                    foreach (int tick in ticks) { state = Through(state, tick); counts.Add(ReadyPresentation.Project(state.Ready, Raster).Length); scales.Add(state.Ready!.DrawScale); }
                    Assert.Equal(Ints(expected, "drawCounts"), counts);
                    Assert.Equal(expected.GetProperty("drawScales").EnumerateArray().Select(n => n.ValueKind == JsonValueKind.Null ? (int?)null : n.GetInt32()), scales); break;
                }
            case "RPR-021":
            case "RPR-027":
                Assert.Equal(Replay(0), Replay(1)); Assert.Equal(Replay(1), Replay(3)); break;
            case "RPR-026":
                FailedReadiness(); break;
            case "RPR-028":
                {
                    var art = AuthoredReady.Loaded(); var background = Background(); var frozen = background.ToArray();
                    Assert.Equal(background, ReadyPresentation.ComposeOver(art, [], background)); Assert.Equal(frozen, background);
                    Assert.Throws<ArgumentException>(() => ReadyPresentation.ComposeOver(art, [], background.AsSpan(1)));
                    background[0] = 1; Assert.Throws<ArgumentException>(() => ReadyPresentation.ComposeOver(art, [], background));
                    background[0] = 0; background[3] = 0; Assert.Throws<ArgumentException>(() => ReadyPresentation.ComposeOver(art, [], background)); break;
                }
            case "RPR-029":
                {
                    using var evidence = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/ready-rendering-source-v1.json")));
                    Assert.False(evidence.RootElement.GetProperty("originalComparisonPerformed").GetBoolean());
                    Assert.False(evidence.RootElement.GetProperty("originalFidelityEstablished").GetBoolean());
                    Assert.Equal(Ints(given, "sourceFrames"), evidence.RootElement.GetProperty("observations").EnumerateArray().Select(o => o.GetProperty("sourceFrame").GetInt32())); break;
                }
            case "RPR-030":
                {
                    var art = AuthoredGeneralBanner.Loaded(); var state = GeneralBannerPreview.Start(art);
                    var draws = GeneralBannerPresentation.Project(state.Banner!);
                    Assert.Equal(new byte[] { 40, 56, 72, 255 }, GeneralBannerPresentation.Compose(art, draws).Take(4));
                    Assert.Throws<InvalidDataException>(() => PreparedReadyArtwork.Parse(art.Definition.ToUtf8()));
                    Assert.Throws<ArgumentException>(() => BannerPreviewOptions.Parse(["--ready", "--content-root", "art"])); break;
                }
            case "RPR-031":
                Assert.Equal(255 * 128, expected.GetProperty("scaleProductMax").GetInt32());
                Assert.Equal(255 * 24 * 4096, expected.GetProperty("crossNumeratorTimes4096Max").GetInt32());
                Assert.Throws<ArgumentOutOfRangeException>(() => ReadyPresentation.ScaleChannel(255, 129));
                Assert.Throws<ArgumentOutOfRangeException>(() => ReadyPresentation.VertexChannel(0, 0, 0, 0, 0, 0, 0, 24)); break;
            case "RPR-032":
                {
                    var session = Session(); var run = session.Start(new("sample:scene/prepared-only"), new("sample:consumer/prepared-only"));
                    Assert.True(run.Accepted, run.Diagnostic); Assert.Equal(256 * 240 * 4, ReadyPresentation.Compose(session.Artwork, ReadyPresentation.Project(run.State.Ready, Raster)).Length); break;
                }
            default: throw new InvalidOperationException("Unimplemented presentation case " + id);
        }
    }
    private static int[] Ints(JsonElement element, string key) => element.GetProperty(key).EnumerateArray().Select(v => v.GetInt32()).ToArray();
    private static LoadedReadyArtwork Flat(ushort word)
    {
        var loaded = AuthoredReady.Loaded(word);
        return loaded with { Definition = loaded.Definition with { Glyphs = [.. loaded.Definition.Glyphs.Select(g => g with { Colors = [new(128, 128, 128), new(128, 128, 128), new(128, 128, 128), new(128, 128, 128)] })] } };
    }
    private static string Replay(int reads)
    {
        var session = Session(); var run = session.Start(new("sample:scene/replay"), new("sample:consumer/replay"));
        Assert.True(run.Accepted, run.Diagnostic); var state = run.State; var trace = new List<string> { JsonSerializer.Serialize(run.Trace) };
        var retained = state.Ready; string frozen = JsonSerializer.Serialize(retained);
        while (!state.Event.Exited)
        {
            string before = JsonSerializer.Serialize(state);
            for (int i = 0; i < reads; i++)
            {
                var draws = ReadyPresentation.Project(state.Ready, Raster);
                Assert.Equal(ReadyPresentation.Compose(session.Artwork, draws), ReadyPresentation.Compose(session.Artwork, draws));
            }
            Assert.Equal(before, JsonSerializer.Serialize(state));
            run = ReadySceneExecutor.AdvanceTick(state, false, [], 8192); Assert.True(run.Accepted, run.Diagnostic); state = run.State; trace.Add(JsonSerializer.Serialize(run.Trace));
        }
        Assert.Equal(frozen, JsonSerializer.Serialize(retained));
        return JsonSerializer.Serialize(state) + string.Join('\n', trace);
    }
    private static void FailedReadiness()
    {
        foreach (string file in new[] { "artwork.json", "indices.bin", "palette.bin" })
        {
            string root = Temp(); AuthoredReady.WriteEvent(Path.Combine(root, "event")); AuthoredReady.WriteArtwork(Path.Combine(root, "art"));
            File.Delete(Path.Combine(root, "art", file)); ReadySession? session = null;
            Assert.Throws<FileNotFoundException>(() => session = ReadySession.Load(LooseAssetStore.Load(Path.Combine(root, "event")), AuthoredReady.Id, LooseAssetStore.Load(Path.Combine(root, "art"))));
            Assert.Null(session);
        }
    }
    [Fact]
    public void RDY_031_and_032_prepared_session_projects_all_corners_and_finite_ready_lifetime()
    {
        using var spec = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/ready-display-contract-v1.json")));
        Assert.Equal(new[] { "RDY-031", "RDY-032" }, spec.RootElement.GetProperty("cases").EnumerateArray().Skip(30).Select(c => c.GetProperty("id").GetString()));
        var session = Session(60); var run = session.Start(new("sample:scene/ready-host"), new("sample:consumer/ready-host")); Assert.True(run.Accepted, run.Diagnostic);
        var state = run.State;
        Assert.NotEqual(session.Artwork.Definition.Glyphs[0].Colors[0], session.Artwork.Definition.Glyphs[0].Colors[1]);
        state = Through(state, 68); Assert.True(state.Ready!.ChildIsTerminal); Assert.False(state.Ready.IsTerminal); Assert.Empty(ReadyPresentation.Project(state.Ready, Raster));
        state = Through(state, 69); Assert.True(state.Ready!.IsTerminal); Assert.False(state.Event.Exited);
        Assert.True(Through(state, 70).Event.Exited);
    }
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 2, 1)]
    [InlineData(60, 1, 1)]
    [InlineData(60, 5, 5)]
    [InlineData(60, 60, 60)]
    [InlineData(255, -1, 255)]
    public void Presented_lifetime_preserves_early_late_and_ignored_Circle(int hold, int circleTick, int release)
    {
        var session = Session(hold); var run = session.Start(new("sample:scene/input"), new("sample:consumer/input"));
        Assert.True(run.Accepted, run.Diagnostic); var state = run.State;
        while (!state.Event.Exited)
        {
            var draws = ReadyPresentation.Project(state.Ready, Raster);
            Assert.Equal(state.Event.Tick < release + 7 ? 6 : 0, draws.Length);
            if (state.Event.Tick == release + 6) Assert.All(draws, d => Assert.Equal(56, d.Scale));
            _ = ReadyPresentation.Compose(session.Artwork, draws);
            run = ReadySceneExecutor.AdvanceTick(state, state.Event.Tick + 1 == circleTick, [], 8192);
            Assert.True(run.Accepted, run.Diagnostic); state = run.State;
        }
        Assert.Equal(release + 10, state.Event.Tick);
    }
    [Theory]
    [InlineData("state")]
    [InlineData("duplicate")]
    [InlineData("id")]
    [InlineData("scale")]
    [InlineData("origin")]
    public void Invalid_draws_reject_before_composition(string fault)
    {
        ImmutableArray<ReadyGlyphDraw> draws = fault switch
        {
            "state" => default,
            "duplicate" => [new(0, 128, Raster), new(0, 128, Raster)],
            "id" => [new(6, 128, Raster)],
            "scale" => [new(0, 47, Raster)],
            _ => [new(0, 128, Raster with { FramebufferY = 273 })]
        };
        Assert.Throws<ArgumentException>(() => ReadyPresentation.Compose(AuthoredReady.Loaded(), draws));
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("dither")]
    [InlineData("precision")]
    [InlineData("circle")]
    [InlineData("mode")]
    public void Unsupported_host_options_are_closed(string fault)
    {
        var args = new List<string> { "--ready", "--event-root", "event", "--event-id", AuthoredReady.Id.Value, "--artwork-root", "art" };
        if (fault == "missing") args.RemoveRange(5, 2);
        if (fault == "duplicate") args.AddRange(["--event-id", AuthoredReady.Id.Value]);
        if (fault == "dither") args.AddRange(["--dither", "off"]);
        if (fault == "precision") args.AddRange(["--precision", "rgb8"]);
        if (fault == "circle") args.AddRange(["--circle-tick", "1"]);
        if (fault == "mode") args[0] = "--ordered-banner";
        Assert.Throws<ArgumentException>(() => ReadyPreviewOptions.Parse(args.ToArray()));
    }
}
