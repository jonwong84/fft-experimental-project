using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Tests.Support;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content;
using Zodiac.Framework.Events;
using Xunit;

namespace Fft.Client.Tests;

public sealed class DarkScreenIntroductionSessionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "fft-dark-intro-" + Guid.NewGuid().ToString("N"));
    private DarkScreenIntroductionSession Session(int g = 60, int r = 60, int standalone = 44)
    {
        AuthoredDarkScreenIntroduction.Write(Path.Combine(root, "sequence"), AuthoredDarkScreenIntroduction.Definition(g, r));
        AuthoredDarkScreen.Write(Path.Combine(root, "dark"), AuthoredDarkScreenIntroduction.Dark(standalone));
        AuthoredGeneralBanner.Write(Path.Combine(root, "general")); AuthoredReady.WriteArtwork(Path.Combine(root, "ready"));
        return Load();
    }
    private DarkScreenIntroductionSession Load() => DarkScreenIntroductionSession.Load(
        LooseAssetStore.Load(Path.Combine(root, "sequence")), AuthoredDarkScreenIntroduction.Id,
        LooseAssetStore.Load(Path.Combine(root, "dark")), LooseAssetStore.Load(Path.Combine(root, "general")), LooseAssetStore.Load(Path.Combine(root, "ready")));

    [Theory] [InlineData(0, 0, 155)] [InlineData(60, 60, 275)] [InlineData(255, 255, 665)]
    public void All_battle_routes_are_frozen_until_one_terminal_handoff_then_existing_restart_works(int g, int r, int exit)
    {
        var s = DarkScreenIntroductionBattleSession.Start(Session(g, r), () => { }); var battle = s.Battle;
        while (!s.State.BattleAvailable)
        {
            foreach (var key in Enum.GetValues<Keys>()) Assert.False(s.Key(key));
            foreach (var action in Enum.GetValues<BattleUiAction>()) Assert.False(s.Action(action));
            foreach (var button in BattleUiLayout.Buttons) Assert.False(s.Click(new(button.Bounds.X + 1, button.Bounds.Y + 1)));
            Assert.Same(battle, s.Battle); Assert.Empty(s.Journal); Assert.Null(s.SelectedUnit);
            Assert.Equal(BattleInteractionMode.Inspect, s.Mode); Assert.Equal(new(0, 0, 0), s.Cursor);
            var result = s.AdvanceIntroduction(false); Assert.True(result.Accepted, result.Diagnostic);
            Assert.Same(battle, s.Battle); Assert.Equal(result.BattleBecameAvailable, s.State.Event.Tick == exit);
        }
        Assert.Equal(exit, s.AvailabilityTick); Assert.Equal(1, s.AvailabilityPublications); Assert.Empty(s.Journal);
        var terminal = s.State; Assert.False(s.AdvanceIntroduction(false).Accepted); Assert.Same(terminal, s.State);
        Assert.True(s.Key(Keys.N)); Assert.Single(s.Journal); Assert.NotSame(battle, s.Battle);
        Assert.True(s.Action(BattleUiAction.Restart)); Assert.Empty(s.Journal); Assert.Same(terminal, s.State);
        Assert.Equal(1, s.AvailabilityPublications);
    }
    [Fact]
    public void Composition_is_fresh_dark_then_active_display_and_order_is_visibly_sensitive()
    {
        var resources = Session(); var s = DarkScreenIntroductionBattleSession.Start(resources, () => { }); bool sensitive = false;
        while (!s.State.BattleAvailable)
        {
            var state = s.State; string before = JsonSerializer.Serialize(state);
            byte[] dark = DarkScreenPresentation.Compose(DarkScreenPresentation.Project(resources.Dark, state.Dark), DarkScreenPresentation.CheckerBackground());
            var general = state.General is null ? [] : GeneralBannerPresentation.Project(state.General);
            var ready = state.Ready is null ? [] : ReadyPresentation.Project(state.Ready, ReadyRasterState.Selected);
            byte[] expected = state.Ready is not null ? ReadyPresentation.ComposeOver(resources.Ready, ready, dark)
                : FiveBitGeneralBannerPresentation.ComposeOver(resources.General, [.. general.Select(g => new BannerRasterDraw(g, new(false, 0, 0)))], dark);
            Assert.Equal(expected, resources.Compose(state)); Assert.Equal(expected, resources.Compose(state));
            Assert.Equal(before, JsonSerializer.Serialize(state));
            if (state.Event.Tick == 75)
            {
                var wrong = DarkScreenPresentation.Compose(DarkScreenPresentation.Project(resources.Dark, state.Dark),
                    FiveBitGeneralBannerPresentation.ComposeOver(resources.General, [.. general.Select(g => new BannerRasterDraw(g, new(false, 0, 0)))], DarkScreenPresentation.CheckerBackground()));
                sensitive = !expected.SequenceEqual(wrong);
            }
            Assert.True(s.AdvanceIntroduction(false).Accepted);
        }
        Assert.True(sensitive); Assert.Equal(DarkScreenPresentation.CheckerBackground(), resources.Compose(s.State));
    }
    [Fact]
    public void Render_omissions_and_standalone_hold_changes_preserve_full_state_trace_and_skip_schedule()
    {
        using var other = new DarkScreenIntroductionSessionTests();
        var first = Session(60, 60, 0); var second = other.Session(60, 60, 255);
        var a = DarkScreenIntroductionBattleSession.Start(first, () => { }); var b = DarkScreenIntroductionBattleSession.Start(second, () => { });
        int[] circles = [1, 55, 56, 57, 59, 60];
        while (!a.State.BattleAvailable)
        {
            if (a.State.Event.Tick % 19 == 0) { _ = a.ComposeIntroduction(); _ = a.ComposeIntroduction(); }
            bool circle = circles.Contains((int)a.State.Event.Tick + 1);
            Assert.Equal(JsonSerializer.Serialize(a.AdvanceIntroduction(circle)), JsonSerializer.Serialize(b.AdvanceIntroduction(circle)));
        }
        Assert.Equal(127, a.AvailabilityTick); Assert.Equal(JsonSerializer.Serialize(a.Trace), JsonSerializer.Serialize(b.Trace));
    }
    [Theory]
    [InlineData("sequence", "assets.manifest.json")] [InlineData("sequence", "introduction.json")]
    [InlineData("dark", "assets.manifest.json")] [InlineData("dark", "dark-screen.json")]
    [InlineData("general", "assets.manifest.json")] [InlineData("general", "palette.bin")]
    [InlineData("ready", "assets.manifest.json")] [InlineData("ready", "palette.bin")]
    public void Every_missing_or_corrupt_resource_prevents_a_startable_session(string package, string file)
    {
        _ = Session(); string path = Path.Combine(root, package, file); File.WriteAllText(path, "{}");
        var corrupt = Record.Exception(() => Load());
        Assert.True(corrupt is InvalidDataException or JsonException, corrupt?.ToString());
        File.Delete(path); Assert.Throws<FileNotFoundException>(() => Load());
    }
    [Fact]
    public void Foreign_snapshots_late_rollback_and_graphics_allocation_failures_publish_no_progress()
    {
        var first = Session(); var second = Load(); bool allocated = false;
        Assert.Throws<IOException>(() => DarkScreenIntroductionBattleSession.Start(first, () => throw new IOException("initial graphics failure")));
        var session = DarkScreenIntroductionBattleSession.Start(first, () => allocated = true); Assert.True(allocated);
        var before = session.State; Assert.Throws<ArgumentException>(() => second.Compose(before));
        var rejected = session.AdvanceIntroduction(false, updateBudget: 0);
        // Tick one changes no ledger; use the first Holding transition to force late rollback.
        if (rejected.Accepted) while (session.State.Event.Tick < 54) Assert.True(session.AdvanceIntroduction(false).Accepted);
        before = session.State; int traceCount = session.Trace.Length;
        var late = session.AdvanceIntroduction(false, updateBudget: 0); Assert.False(late.Accepted);
        Assert.Same(before, session.State); Assert.Equal(traceCount, session.Trace.Length); Assert.Equal(0, session.AvailabilityPublications);
        Assert.Throws<IOException>(() => session.RecreateGraphics(() => throw new IOException("recreation failure")));
        Assert.Same(before, session.State); Assert.Equal(traceCount, session.Trace.Length);
        Assert.True(session.AdvanceIntroduction(false).Accepted);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Existing_battle_check_plan_and_rng_survive_handoff_and_restart(bool skip)
    {
        var session = DarkScreenIntroductionBattleSession.Start(Session(), () => { });
        while (!session.State.BattleAvailable) Assert.True(session.AdvanceIntroduction(skip).Accepted);
        foreach (var step in BattleUiCheckPlan.Inputs)
        {
            if (step.Key is { } key) Assert.True(session.Key(key));
            if (step.Click is { } click) Assert.True(session.Click(click));
        }
        Assert.Equal(14, session.Journal.Length);
        Assert.Equal(new Zodiac.Framework.FftRandomState(2473755763, 22), session.Battle.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(BattleSummary(BattleUiCheckPlan.Expected()), BattleSummary(session.Battle));
        var terminal = session.State; Assert.True(session.Key(Keys.R)); Assert.Empty(session.Journal); Assert.Same(terminal, session.State);
    }
    [Theory] [InlineData("0")] [InlineData("4097")] [InlineData("1,1")] [InlineData("bad")]
    public void Host_rejects_invalid_or_unsupported_input_options(string schedule)
    {
        string[] args = ["--dark-intro-battle", "--sequence-root", "a", "--sequence-id", "sample:intro/a", "--dark-screen-root", "b",
            "--general-artwork-root", "c", "--ready-artwork-root", "d", "--check", "scratch/check", "--circle-ticks", schedule];
        Assert.Throws<ArgumentException>(() => DarkScreenIntroductionOptions.Parse(args));
        Assert.Throws<ArgumentException>(() => DarkScreenIntroductionOptions.Parse([.. args, "--skip-event-end", "true"]));
    }
    private static string BattleSummary(Zodiac.Framework.TwoUnitBattleState state) => JsonSerializer.Serialize(new
    {
        state.Outcome, state.WinnerId, state.Battle.Combatants, state.Battle.TurnBattle.Turns,
        state.Battle.TurnBattle.Battle.RngState,
        units = state.Battle.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }),
    });
    [Theory] [InlineData(0)] [InlineData(75)] [InlineData(275)]
    public void Resize_layout_and_successful_or_failed_recreation_preserve_accepted_snapshots(int tick)
    {
        var session = DarkScreenIntroductionBattleSession.Start(Session(), () => { });
        while (session.State.Event.Tick < tick) Assert.True(session.AdvanceIntroduction(false).Accepted);
        var state = session.State; int traces = session.Trace.Length; var battle = session.Battle;
        byte[] pixels = session.ComposeIntroduction();
        foreach (var (width, height) in new[] { (960, 640), (1280, 720), (640, 960), (1, 1) })
        {
            session.RecreateGraphics(() =>
            {
                var fit = SceneLayout.Fit(width, height);
                Assert.True(fit.Width > 0 && fit.Height > 0);
                _ = BattleUiLayout.CanvasPoint(new(width / 2, height / 2), width, height, width, height);
                Assert.Equal(pixels, session.ComposeIntroduction());
            });
            Assert.Same(state, session.State); Assert.Same(battle, session.Battle); Assert.Equal(traces, session.Trace.Length);
        }
        Assert.Throws<IOException>(() => session.RecreateGraphics(() => throw new IOException("injected recreation failure")));
        Assert.Same(state, session.State); Assert.Equal(traces, session.Trace.Length);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
