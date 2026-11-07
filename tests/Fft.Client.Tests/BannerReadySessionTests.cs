using System.Security.Cryptography;
using System.Text.Json;
using Fft.Tests.Support;
using Zodiac.Content;
using Zodiac.Framework.Events;
using Fft.Client.Presentation;
using Xunit;

namespace Fft.Client.Tests;

public sealed class BannerReadySessionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "fft-sequence-session-" + Guid.NewGuid().ToString("N"));
    private BannerReadySession Session()
    {
        AuthoredBannerReadySequence.Write(Path.Combine(root, "sequence"), AuthoredBannerReadySequence.Definition(0, 0));
        AuthoredGeneralBanner.Write(Path.Combine(root, "general")); AuthoredReady.WriteArtwork(Path.Combine(root, "ready"));
        return Load();
    }
    private BannerReadySession Load() => BannerReadySession.Load(LooseAssetStore.Load(Path.Combine(root, "sequence")), AuthoredBannerReadySequence.Id,
        LooseAssetStore.Load(Path.Combine(root, "general")), LooseAssetStore.Load(Path.Combine(root, "ready")));
    private static BannerReadyState Accept(BannerReadyResult r) { Assert.True(r.Accepted, r.Diagnostic); return r.State; }
    [Fact]
    public void Both_stages_use_existing_policies_with_no_retained_framebuffer_or_palette_state()
    {
        var session = Session(); var state = Accept(session.Start(new("sample:scene/a"), new("sample:consumer/a")));
        while (!state.Event.Exited)
        {
            byte[] expected = state.Ready is { } ready
                ? ReadyPresentation.Compose(session.Ready, ReadyPresentation.Project(ready, ReadyRasterState.Selected))
                : FiveBitGeneralBannerPresentation.Compose(session.General, [.. GeneralBannerPresentation.Project(state.General!)
                    .Select(g => new BannerRasterDraw(g, new(false, 0, 0)))]);
            Assert.Equal(expected, session.Compose(state)); state = Accept(BannerReadyExecutor.AdvanceTick(state, false));
        }
        Assert.Equal(42, state.Event.Tick); Assert.Equal(ReadyPresentation.Compose(session.Ready, []), session.Compose(state));
    }
    [Fact]
    public void Rendering_reads_and_omissions_preserve_full_state_and_trace_replay()
    {
        var session = Session(); var a = Accept(session.Start(new("sample:scene/a"), new("sample:consumer/a")));
        var b = Accept(session.Start(new("sample:scene/a"), new("sample:consumer/a")));
        while (!a.Event.Exited)
        {
            _ = SHA256.HashData(session.Compose(a)); _ = session.Compose(a);
            var ra = BannerReadyExecutor.AdvanceTick(a, false); var rb = BannerReadyExecutor.AdvanceTick(b, false);
            Assert.Equal(JsonSerializer.Serialize(ra), JsonSerializer.Serialize(rb)); a = Accept(ra); b = Accept(rb);
        }
    }
    [Theory]
    [InlineData("general")] [InlineData("ready")]
    public void Either_missing_or_corrupt_resource_prevents_startable_session(string artwork)
    {
        _ = Session(); File.WriteAllBytes(Path.Combine(root, artwork, "palette.bin"), new byte[32]);
        Assert.Throws<InvalidDataException>(() => Load()); File.Delete(Path.Combine(root, artwork, "palette.bin"));
        Assert.Throws<FileNotFoundException>(() => Load());
    }
    [Fact]
    public void Foreign_session_snapshot_cannot_select_another_resources_binding()
    {
        var first = Session(); var other = Load(); var state = Accept(first.Start(new("sample:scene/a"), new("sample:consumer/a")));
        Assert.Throws<ArgumentException>(() => other.Compose(state));
    }
    [Theory]
    [InlineData("0")] [InlineData("4097")] [InlineData("1,1")] [InlineData("x")] [InlineData("")]
    public void Invalid_input_schedule_is_not_guessed(string ticks)
    {
        string[] args = ["--banner-ready-sequence", "--sequence-root", "a", "--sequence-id", "sample:sequence/a", "--general-artwork-root", "b",
            "--ready-artwork-root", "c", "--check", "scratch/out", "--circle-ticks", ticks];
        Assert.Throws<ArgumentException>(() => BannerReadyPreviewOptions.Parse(args));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
