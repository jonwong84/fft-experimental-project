using System.Text.Json;
using Fft.Client.Presentation;
using Fft.Tests.Support;
using Microsoft.Xna.Framework.Input;
using Zodiac.Content;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Fft.Client.Tests;

public sealed class IntroductionBattleSessionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "fft-intro-battle-" + Guid.NewGuid().ToString("N"));
    private BannerReadySession Prepared(int general = 60, int ready = 60)
    {
        AuthoredBannerReadySequence.Write(Path.Combine(root, "sequence"), AuthoredBannerReadySequence.Definition(general, ready));
        AuthoredGeneralBanner.Write(Path.Combine(root, "general")); AuthoredReady.WriteArtwork(Path.Combine(root, "ready"));
        return Load();
    }
    private BannerReadySession Load() => BannerReadySession.Load(LooseAssetStore.Load(Path.Combine(root, "sequence")), AuthoredBannerReadySequence.Id,
        LooseAssetStore.Load(Path.Combine(root, "general")), LooseAssetStore.Load(Path.Combine(root, "ready")));
    private IntroductionBattleSession Start(int general = 60, int ready = 60) => IntroductionBattleSession.Start(Prepared(general, ready));
    private static void Through(IntroductionBattleSession session, long tick, bool skip = false)
    {
        while (session.Gate.Introduction.Event.Tick < tick)
            Assert.True(session.AdvanceIntroduction(skip && new long[] { 5, 7, 8 }.Contains(session.Gate.Introduction.Event.Tick + 1)).Accepted);
    }
    private static string Summary(TwoUnitBattleState state) => JsonSerializer.Serialize(new
    {
        state.Outcome, state.WinnerId, state.Battle.Combatants, state.Battle.TurnBattle.Turns,
        state.Battle.TurnBattle.Battle.RngState,
        units = state.Battle.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }),
    });
    [Theory]
    [InlineData(60, 60, 162)] [InlineData(0, 0, 42)] [InlineData(255, 255, 552)]
    public void Battle_is_frozen_through_handoff_and_first_fresh_command_uses_existing_rules(int general, int ready, int exit)
    {
        var session = Start(general, ready); var initial = session.Battle;
        while (!session.Gate.BattleAvailable)
        {
            Assert.Same(initial, session.Battle); Assert.Empty(session.Journal); Assert.Null(session.SelectedUnit);
            var result = session.AdvanceIntroduction(false); Assert.True(result.Accepted);
        }
        Assert.Same(initial, session.Battle); Assert.Empty(session.Journal);
        Assert.Equal(exit, session.AvailabilityTick); Assert.Equal(1, session.AvailabilityPublications);
        var expected = TwoUnitBattleScenario.Apply(initial, new TwoUnitBattleCommand.Advance());
        Assert.True(session.Key(Keys.N)); Assert.Equal(Summary(expected.State), Summary(session.Battle));
        Assert.Single(session.Journal); Assert.IsType<TwoUnitBattleCommand.Advance>(session.Journal[0]);
        Assert.Equal(1, session.AvailabilityPublications);
    }
    [Theory]
    [InlineData(0)] [InlineData(160)] [InlineData(161)]
    public void Every_battle_route_is_closed_until_event_exit_including_selection_and_save_load_keys(int tick)
    {
        var session = Start(); Through(session, tick); var before = session.Battle;
        foreach (var key in new[] { Keys.N, Keys.Space, Keys.M, Keys.A, Keys.W, Keys.R, Keys.D1, Keys.D2,
                     Keys.Tab, Keys.Enter, Keys.Back, Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.S, Keys.F5, Keys.F9 })
            Assert.False(session.Key(key));
        foreach (var action in Enum.GetValues<BattleUiAction>()) Assert.False(session.Action(action));
        foreach (var b in BattleUiLayout.Buttons) Assert.False(session.Click(new(b.Bounds.X+1, b.Bounds.Y+1)));
        Assert.False(session.Click(new(30, 230))); Assert.False(session.Click(SceneLayout.TileCenter(0, 1, 2, 2)));
        Assert.Same(before, session.Battle); Assert.Empty(session.Journal); Assert.Null(session.SelectedUnit);
        Assert.Equal(BattleInteractionMode.Inspect, session.Mode); Assert.Equal(new TilePosition(0, 0, 0), session.Cursor);
        Assert.Equal(0, session.AvailabilityPublications);
    }
    [Fact]
    public void Transition_circle_is_never_reused_as_battle_action_and_cannot_publish_twice()
    {
        var session = Start(); var initial = session.Battle; Through(session, 18, true);
        Assert.True(session.Gate.BattleAvailable); Assert.Equal(18, session.AvailabilityTick);
        Assert.Same(initial, session.Battle); Assert.Empty(session.Journal);
        var gate = session.Gate; var trace = session.IntroductionTrace;
        for (int i = 0; i < 3; i++)
        {
            Assert.False(session.AdvanceIntroduction(true).Accepted); _ = session.ComposeIntroduction();
            Assert.Same(gate, session.Gate); Assert.Equal(trace, session.IntroductionTrace); Assert.Equal(1, session.AvailabilityPublications);
        }
    }
    [Theory]
    [InlineData(90, 8192, 0)] [InlineData(91, 0, 64)] [InlineData(91, 8192, 0)]
    [InlineData(160, 8192, 0)] [InlineData(161, 0, 64)]
    public void Rejected_intro_transaction_preserves_combined_state_and_retries_once(int tick, int instructions, int updates)
    {
        var session = Start(); Through(session, tick);
        var gate = session.Gate; var battle = session.Battle; var trace = session.IntroductionTrace;
        var rejected = session.AdvanceIntroduction(false, instructions, updates);
        Assert.False(rejected.Accepted); Assert.Same(gate, session.Gate); Assert.Same(battle, session.Battle);
        Assert.Equal(trace, session.IntroductionTrace); Assert.Empty(session.Journal); Assert.Equal(0, session.AvailabilityPublications);
        Assert.True(session.AdvanceIntroduction(false).Accepted); Assert.Equal(tick == 161 ? 1 : 0, session.AvailabilityPublications);
    }
    [Fact]
    public void Handoff_baselines_held_inputs_discards_queued_presses_and_requires_release()
    {
        var session = Start(0, 0); var input = new BattleInputBuffer();
        input.KeyDown(Keys.N); input.KeyDown(Keys.R); Through(session, 42);
        input.Baseline([Keys.N], true); input.KeyDown(Keys.N);
        Assert.False(input.TryTake(out _)); Assert.False(input.LeftPress(true)); Assert.Empty(session.Journal);
        input.KeyUp(Keys.N); input.LeftPress(false); input.KeyDown(Keys.N);
        Assert.True(input.TryTake(out var key)); Assert.True(session.Key(key)); Assert.Single(session.Journal);
        Assert.False(input.TryTake(out _)); Assert.True(input.LeftPress(true)); Assert.False(input.LeftPress(true));
    }
    [Fact]
    public void Focus_queue_clearing_and_baselining_cannot_submit_or_replay_commands()
    {
        var session = Start(0, 0); var input = new BattleInputBuffer(); var initial = session.Battle;
        input.KeyDown(Keys.N); input.ClearQueued(); input.Baseline([Keys.N], true);
        Assert.False(input.TryTake(out _)); Assert.False(input.LeftPress(true));
        Through(session, 42); input.Baseline([Keys.N], true); input.KeyDown(Keys.N);
        Assert.False(input.TryTake(out _)); Assert.Same(initial, session.Battle); Assert.Empty(session.Journal);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Native_check_routes_after_intro_match_old_router_headless_replay_and_rejections(bool skip)
    {
        var session = Start(); Through(session, skip ? 18 : 162, skip);
        var old = new BattleInteraction(SyntheticTwoUnitBattle.Create()); var oldRouter = new BattleUiRouter(old);
        foreach (var step in BattleUiCheckPlan.Inputs)
        {
            if (step.Key is { } key) { Assert.True(session.Key(key)); oldRouter.Key(key); }
            if (step.Click is { } click) { Assert.True(session.Click(click)); oldRouter.Click(click); }
            Assert.Equal(Summary(old.State), Summary(session.Battle)); Assert.Equal(old.Journal.ToArray(), session.Journal.ToArray());
        }
        Assert.Equal(14, session.Journal.Length); Assert.Equal(Summary(BattleUiCheckPlan.Expected()), Summary(session.Battle));
        Assert.Equal(new FftRandomState(2473755763, 22), session.Battle.Battle.TurnBattle.Turns.RngState);
        var intro = session.Gate; Assert.True(session.Key(Keys.R)); Assert.Same(intro, session.Gate);
        Assert.Empty(session.Journal); Assert.Equal(1, session.AvailabilityPublications); Assert.Null(session.SelectedUnit);
    }
    [Fact]
    public void Repeated_rendering_or_omitted_rendering_does_not_change_gate_trace_or_battle()
    {
        var resources = Prepared(0, 0); var a = IntroductionBattleSession.Start(resources); var b = IntroductionBattleSession.Start(resources);
        while (!a.Gate.BattleAvailable)
        {
            _ = a.ComposeIntroduction(); _ = a.ComposeIntroduction();
            Assert.Equal(JsonSerializer.Serialize(a.AdvanceIntroduction(false)), JsonSerializer.Serialize(b.AdvanceIntroduction(false)));
            Assert.Equal(Summary(a.Battle), Summary(b.Battle));
            Assert.Equal(JsonSerializer.Serialize(a.IntroductionTrace), JsonSerializer.Serialize(b.IntroductionTrace));
        }
        Assert.Empty(a.Journal); Assert.Empty(b.Journal);
    }
    [Theory]
    [InlineData("sequence", "sequence.json")] [InlineData("general", "palette.bin")] [InlineData("ready", "palette.bin")]
    public void Missing_or_corrupt_prepared_input_never_returns_a_startable_session(string package, string file)
    {
        _ = Prepared(); string path = Path.Combine(root, package, file); File.WriteAllBytes(path, [0]);
        Assert.Throws<InvalidDataException>(() => Load()); File.Delete(path);
        Assert.Throws<FileNotFoundException>(() => Load());
    }
    [Fact]
    public void New_host_options_reuse_the_closed_descriptor_without_admitting_old_flags_or_mutating_arguments()
    {
        string[] args = ["--intro-battle", "--sequence-root", "a", "--sequence-id", "sample:sequence/a",
            "--general-artwork-root", "b", "--ready-artwork-root", "c", "--check", "scratch/out", "--circle-ticks", "8,5,7"];
        var parsed = IntroductionBattleHost.Parse(args); Assert.Equal(new[] { 5, 7, 8 }, parsed.CircleTicks);
        Assert.Equal("--intro-battle", args[0]);
        Assert.Throws<ArgumentException>(() => IntroductionBattleHost.Parse([]));
        foreach (var bad in new[] { "--ready", "--battle", "--banner-ready-sequence" })
        { var copy = args.ToArray(); copy[0] = bad; Assert.Throws<ArgumentException>(() => IntroductionBattleHost.Parse(copy)); }
        Assert.Throws<ArgumentException>(() => IntroductionBattleHost.Parse([.. args, "--skip-event-end", "true"]));
        Assert.Throws<ArgumentException>(() => IntroductionBattleHost.Parse([.. args, "--sequence-id", "sample:other"]));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
