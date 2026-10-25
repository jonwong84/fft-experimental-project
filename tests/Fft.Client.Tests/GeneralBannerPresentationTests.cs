using System.Text.Json;
using Fft.Client.Presentation;
using Zodiac.Content;
using Zodiac.Framework;
using Zodiac.Framework.Events;
using Fft.Tests.Support;
using Xunit;

namespace Fft.Client.Tests;

public sealed class GeneralBannerPresentationTests
{
    [Theory]
    [InlineData("REVEAL-CROP")][InlineData("HIDE-CROP")][InlineData("SHARED-CLAMP")][InlineData("PALETTE-WORDS")]
    [InlineData("MODULATION")][InlineData("GRADIENT-CENTER")][InlineData("ORDER-MATTERS")]
    [InlineData("SELECTED-MILESTONES")][InlineData("ZERO-HOLD-BOUNDARY")][InlineData("SKIP-FIRST")]
    [InlineData("CANCEL")][InlineData("FAILED-TICK")][InlineData("REDRAW-OR-OMIT")][InlineData("READINESS-FAILURE")]
    [InlineData("RESTART-RETAINS-EFFECT")][InlineData("REJECT-UNSUPPORTED-PREPARED-INPUTS")]
    public void AuthoredPresentationContract(string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures/general-banner-presentation-contract-v1.json")));
        var c = doc.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);
        var input = c.TryGetProperty("input", out var i) ? i : default;
        var expected = c.GetProperty("expected");
        var artwork = AuthoredGeneralBanner.Loaded();
        switch (id)
        {
            case "REVEAL-CROP":
            case "HIDE-CROP":
                var crop = GeneralBannerPresentation.Crop(16, input.GetProperty(id == "REVEAL-CROP" ? "revealPixels" : "hidePixels").GetInt32(), id == "REVEAL-CROP");
                Assert.Equal(expected.GetProperty("screenVertices")[0][1].GetInt32(), 70 + crop.Offset);
                Assert.Equal(expected.GetProperty("uvVertices")[0][1].GetInt32(), 2 + crop.Offset);
                Assert.Equal(16, crop.Offset + crop.Rows);
                break;
            case "SHARED-CLAMP":
                var clamped = GeneralBannerPresentation.ClampShared(input.GetProperty("initialSharedOffset").GetInt32(), Ints(input.GetProperty("visitedHeights")));
                Assert.Equal(Ints(expected.GetProperty("visitedOffsets")), clamped.Offsets);
                Assert.Equal(expected.GetProperty("nextSharedOffsetAfterIncrement").GetInt32(), clamped.Next);
                break;
            case "PALETTE-WORDS":
                var words = input.GetProperty("words");
                for (int n = 0; n < words.GetArrayLength(); n++)
                {
                    var color = GeneralBannerPresentation.DecodeColor(words[n].GetUInt16());
                    Assert.Equal(expected[n].GetProperty("discard").GetBoolean(), color.Discard);
                    if (!color.Discard)
                    {
                        Assert.Equal(Ints(expected[n].GetProperty("rgb")), new[] { (int)color.Rgb.R, color.Rgb.G, color.Rgb.B });
                        Assert.Equal(expected[n].GetProperty("semiTransparent").GetBoolean(), color.Stp);
                    }
                }
                break;
            case "MODULATION":
            case "GRADIENT-CENTER":
                int[] texture = Ints(input.GetProperty("textureRgb"));
                int[] top = Ints(input.GetProperty(id == "MODULATION" ? "vertexRgb" : "topRgb"));
                int[] bottom = id == "MODULATION" ? top : Ints(input.GetProperty("bottomRgb"));
                Assert.Equal(Ints(expected.GetProperty("shadedRgb")), Enumerable.Range(0, 3).Select(n =>
                    GeneralBannerPresentation.Shade(texture[n], top[n], bottom[n], id == "MODULATION" ? 0 : 3, id == "MODULATION" ? 1 : 16)));
                break;
            case "ORDER-MATTERS":
                int selected = 100; foreach (var op in new[] { (70, "subtract"), (70, "subtract"), (80, "add") }) selected = GeneralBannerPresentation.Blend(selected, op.Item1, true, op.Item2);
                Assert.Equal(expected.GetProperty("selectedFinalChannel").GetInt32(), selected);
                int immediate = 100; foreach (var op in new[] { (80, "add"), (70, "subtract"), (70, "subtract") }) immediate = GeneralBannerPresentation.Blend(immediate, op.Item1, true, op.Item2);
                Assert.Equal(expected.GetProperty("immediateModeFinalChannel").GetInt32(), immediate);
                break;
            case "SELECTED-MILESTONES":
                var state = GeneralBannerPreview.Start(artwork, 1);
                foreach (var milestone in expected.EnumerateArray())
                {
                    state = Until(state, milestone.GetProperty("tick").GetInt32());
                    Assert.Equal(milestone.GetProperty("phase").GetString(), state.Banner!.Phase.ToString());
                    var draws = GeneralBannerPresentation.Project(state.Banner);
                    if (milestone.TryGetProperty("firstTripletVisibleRows", out var rows)) Assert.All(draws.Take(3), d => Assert.Equal(rows.GetInt32(), d.VisibleRows));
                    if (milestone.TryGetProperty("secondTripletVisibleRows", out rows)) Assert.All(draws.Skip(3), d => Assert.Equal(rows.GetInt32(), d.VisibleRows));
                    if (milestone.TryGetProperty("secondTriplet", out _)) Assert.Equal(3, draws.Length);
                    if (milestone.TryGetProperty("visibleRows", out rows)) Assert.All(draws, d => Assert.Equal(rows.GetInt32(), d.VisibleRows));
                    if (milestone.TryGetProperty("hidePixels", out var hide)) Assert.All(draws, d => Assert.Equal(hide.GetInt32(), d.CropTop));
                    if (milestone.TryGetProperty("submission", out var submission))
                    {
                        if (submission.GetString() == "none") Assert.Empty(draws);
                        else { Assert.Equal(6, draws.Length); Assert.All(draws, d => Assert.Equal(31, d.VisibleRows)); }
                    }
                }
                break;
            case "ZERO-HOLD-BOUNDARY":
                state = Until(GeneralBannerPreview.Start(artwork, 0), 20);
                Assert.Equal(GeneralBannerPhase.Hiding, state.Banner!.Phase);
                Assert.All(GeneralBannerPresentation.Project(state.Banner), d => Assert.Equal(31, d.VisibleRows));
                Assert.All(GeneralBannerPresentation.Project(Tick(state).Banner!), d => Assert.Equal(0, d.CropTop));
                Assert.True(Until(state, expected.GetProperty("completionTick").GetInt32()).Banner!.IsTerminal);
                break;
            case "SKIP-FIRST":
                state = Tick(GeneralBannerPreview.Start(artwork), true);
                Assert.Equal(GeneralBannerPhase.ExitYield, state.Banner!.Phase);
                Assert.Empty(GeneralBannerPresentation.Project(state.Banner)); Assert.Equal(0, state.Banner.RevealCompleted);
                Assert.Empty(GeneralBannerPresentation.Project(Tick(state).Banner!));
                break;
            case "CANCEL":
                state = Until(GeneralBannerPreview.Start(artwork), 4);
                var cancel = BannerSceneExecutor.AdvanceTick(state, false, [], 1, cancellation: new(state.Banner!.TaskId, 0));
                Assert.True(cancel.Accepted); Assert.Equal(GeneralBannerPhase.Cancelled, cancel.State.Banner!.Phase);
                Assert.Empty(GeneralBannerPresentation.Project(cancel.State.Banner));
                break;
            case "FAILED-TICK":
                state = Until(GeneralBannerPreview.Start(artwork, 0), 30);
                byte[] before = GeneralBannerPresentation.Compose(artwork, GeneralBannerPresentation.Project(state.Banner!));
                var rejected = BannerSceneExecutor.AdvanceTick(state, false, [], 0);
                Assert.False(rejected.Accepted); Assert.Same(state, rejected.State); Assert.Equal(30, rejected.State.Event.Tick);
                Assert.Equal(before, GeneralBannerPresentation.Compose(artwork, GeneralBannerPresentation.Project(rejected.State.Banner!)));
                Assert.False(state.Banner!.IsTerminal);
                break;
            case "REDRAW-OR-OMIT":
                var histories = new List<string>();
                foreach (int repeats in new[] { 0, 1, 2 })
                {
                    state = GeneralBannerPreview.Start(artwork);
                    var history = new List<string>();
                    while (!state.Banner!.IsTerminal)
                    {
                        state = Tick(state); history.Add(JsonSerializer.Serialize(state));
                        for (int n = 0; n < repeats; n++) _ = GeneralBannerPresentation.Compose(artwork, GeneralBannerPresentation.Project(state.Banner!));
                    }
                    Assert.Equal(expected.GetProperty("completionTick").GetInt32(), state.Event.Tick);
                    histories.Add(string.Join("\n", history));
                }
                Assert.All(histories, h => Assert.Equal(histories[0], h));
                break;
            case "READINESS-FAILURE":
                state = null;
                string readyRoot = Path.Combine(Path.GetTempPath(), "banner-ready-" + Guid.NewGuid().ToString("N"));
                try
                {
                    AuthoredGeneralBanner.Write(readyRoot);
                    File.WriteAllBytes(Path.Combine(readyRoot, "indices.bin"), new byte[51200]); // same length, wrong hash
                    Assert.Throws<InvalidDataException>(() =>
                    {
                        var loaded = PreparedGeneralBanner.Load(LooseAssetStore.Load(readyRoot), new("sample:banner/test"));
                        state = GeneralBannerPreview.Start(loaded);
                    });
                }
                finally
                {
                    foreach (string name in new[] { "banner.json", "indices.bin", "palette.bin", PreparedAssetManifest.FileName }) File.Delete(Path.Combine(readyRoot, name));
                    Directory.Delete(readyRoot);
                }
                Assert.Null(state);
                break;
            case "RESTART-RETAINS-EFFECT":
                var exit = new SceneEventProgram(new("sample:early-exit"), [new(new ConditionalEventInstruction(4, ConditionalEventOperation.Exit))]);
                state = BannerSceneState.Create(exit.Start(new(0, 0, 0, 0), SceneTaskState.Create(new("sample:retained-scene")), new("sample:first-consumer")));
                state = BannerSceneExecutor.AttachBanner(state, artwork.Definition.ToLogicalDefinition(60), new("sample:owner")).State;
                state = BannerSceneExecutor.RunUntilBlocked(state, 1).State;
                state = Until(state, 5);
                var retained = state.Banner;
                var next = BannerSceneExecutor.StartNextEvent(state, exit, new(0, 0, 0, 0), new("sample:next-consumer"));
                Assert.True(next.Accepted); Assert.Same(retained, next.State.Banner);
                Assert.Equal(5, retained!.RevealCompleted); Assert.Equal(0, retained.StartTick);
                state = BannerSceneExecutor.RunUntilBlocked(next.State, 1).State;
                Assert.Equal(91, Until(state, 91).Event.Tick);
                break;
            case "REJECT-UNSUPPORTED-PREPARED-INPUTS":
                // Full mutation matrix lives in PreparedGeneralBannerTests; this checks the composition gate.
                var badDefinition = artwork.Definition with { Condition = 0 };
                Assert.Throws<InvalidDataException>(() => GeneralBannerPreview.Start(artwork with { Definition = badDefinition }));
                break;
        }
    }

    [Theory]
    [InlineData(0, false, 100)][InlineData(32768, true, 100)][InlineData(31, false, 255)]
    public void TransparentWordStpBlackAndOpaqueTexelsInActualCompositor(int word, bool stp, int expectedRed)
    {
        var a = AuthoredGeneralBanner.Loaded();
        // Authored background at (4,20) is 52, not the vector background 100; zero and STP-black preserve it.
        var palette = a.Palette.ToArray(); palette[2] = (byte)word; palette[3] = (byte)(word >> 8);
        a = a with { Palette = [.. palette] };
        var rgba = GeneralBannerPresentation.Compose(a, [new(0, 0, 31)]);
        Assert.Equal(word == 31 ? 255 : 52, rgba[(20 * 256 + 4) * 4]);
        var decoded = GeneralBannerPresentation.DecodeColor((ushort)word);
        Assert.Equal(stp, decoded.Stp);
        Assert.Equal(expectedRed, word == 0 ? 100 : GeneralBannerPresentation.Blend(100, decoded.Rgb.R, decoded.Stp, "subtract"));
    }

    [Fact]
    public void IndexZeroCanBeOpaqueAndBothMaterialsReplaceForStpClear()
    {
        var a = AuthoredGeneralBanner.Loaded() with { Indices = [.. new byte[51200]], Palette = [31, 0, .. new byte[30]] };
        foreach (int piece in new[] { 0, 2 })
        {
            var rgba = GeneralBannerPresentation.Compose(a, [new(piece, 0, 31)]);
            Assert.Equal(255, rgba[(20 * 256 + 4) * 4]);
            Assert.Equal(0, rgba[(20 * 256 + 4) * 4 + 1]);
            Assert.Equal(255, rgba[(20 * 256 + 4) * 4 + 3]);
        }
    }

    [Theory]
    [InlineData(255, 255, true, "add", 255)]
    [InlineData(0, 255, true, "subtract", 0)]
    [InlineData(200, 20, false, "subtract", 20)]
    [InlineData(200, 20, false, "add", 20)]
    public void Rgb8MaterialBoundaries(int background, int source, bool stp, string material, int expected)
        => Assert.Equal(expected, GeneralBannerPresentation.Blend(background, source, stp, material));

    [Fact]
    public void GradientDoesNotRoundBeforeModulation()
        => Assert.Equal(1, GeneralBannerPresentation.Shade(255, 0, 1, 1, 2)); // vertex .75, product 1.494

    [Fact]
    public void CompositionCropsTextureWithoutScalingAndKeepsTraversalOrder()
    {
        var a = AuthoredGeneralBanner.Loaded();
        var indices = a.Indices.ToArray();
        // Only bottom sampled row has a nontransparent texel. Top is transparent.
        Array.Fill(indices, (byte)0); indices[(128 + 30) * 256] = 1;
        a = a with { Indices = [.. indices], Palette = [0, 0, 31, 0, .. new byte[28]] };
        var cropped = GeneralBannerPresentation.Compose(a, [new(0, 23, 8)]);
        Assert.Equal(255, cropped[((20 + 30) * 256 + 4) * 4]);
        Assert.NotEqual(255, cropped[((20 + 23) * 256 + 4) * 4]);
        var state = Until(GeneralBannerPreview.Start(a), 14);
        Assert.Equal(Enumerable.Range(0, 6), GeneralBannerPresentation.Project(state.Banner!).Select(d => d.PieceIndex));
    }

    private static int[] Ints(JsonElement node) => node.EnumerateArray().Select(n => n.GetInt32()).ToArray();
    private static BannerSceneState Tick(BannerSceneState state, bool circle = false)
    {
        var result = BannerSceneExecutor.AdvanceTick(state, circle, [], 1);
        Assert.True(result.Accepted, result.Diagnostic); return result.State;
    }
    private static BannerSceneState Until(BannerSceneState state, int tick)
    { while (state.Event.Tick < tick) state = Tick(state); return state; }
}
