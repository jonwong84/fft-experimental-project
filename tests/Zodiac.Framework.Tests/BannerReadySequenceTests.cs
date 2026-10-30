using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class BannerReadySequenceTests
{
    private static BannerReadyProgram Program(int general = 60, int ready = 60) => new(new("sample:sequence/test"),
        new(new(new("sample:definition/general"), 8, general), new("sample:art/general"), new("sample:owner/scene")),
        new(new(new("sample:definition/ready"), 0, ready), new("sample:art/ready"), new("sample:owner/scene")));
    private static BannerReadyBinding Binding(BannerReadyProgram p) => new(
        new(p.General.Definition.Id, p.General.ArtworkId, p.General.OwnerId), new(p.Ready.Definition.Id, p.Ready.ArtworkId, p.Ready.OwnerId));
    private static BannerReadyState Fresh(int general = 60, int ready = 60)
    { var p = Program(general, ready); return p.Start(new("sample:scene/sequence"), new("sample:consumer/one"), Binding(p)); }
    private static BannerReadyState Accept(BannerReadyResult r) { Assert.True(r.Accepted, r.Diagnostic); return r.State; }
    private static BannerReadyState Start(int general = 60, int ready = 60) => Accept(BannerReadyExecutor.RunUntilBlocked(Fresh(general, ready)));
    private static BannerReadyState Through(BannerReadyState s, long tick, params int[] circle)
    {
        while (s.Event.Tick < tick) s = Accept(BannerReadyExecutor.AdvanceTick(s, circle.Contains((int)s.Event.Tick + 1)));
        return s;
    }
    private static void Reject(BannerReadyState original, BannerReadyResult result, BannerSceneRejection? reason = null)
    {
        Assert.False(result.Accepted); Assert.Same(original, result.State); Assert.Empty(result.Trace);
        Assert.Equal(0, result.InstructionsExecuted); Assert.Equal(0, result.AcceptedTaskUpdates);
        if (reason is not null) Assert.Equal(reason, result.Rejection);
    }
    [Theory]
    [InlineData(0, 0)] [InlineData(0, 60)] [InlineData(60, 0)] [InlineData(60, 60)] [InlineData(255, 255)]
    public void Natural_sequence_has_one_clock_two_tombstones_and_source_ordered_completion(int general, int ready)
    {
        var state = Start(general, ready); Assert.Equal(0, state.Event.Tick); Assert.Equal(0, state.General!.RevealCompleted);
        Assert.Null(state.Ready); Assert.Equal(1, state.Event.Tasks.LiveTasks);
        state = Through(state, general + 31);
        Assert.True(state.General!.IsTerminal); Assert.Null(state.Ready); Assert.Equal(SceneBarrierStatus.Pending, state.Event.Tasks.BarrierStatus);
        state = Through(state, general + 32);
        Assert.Equal(general + 32, state.Ready!.StartTick); Assert.Equal(ready == 0 ? 128 : 48, state.Ready.DrawScale);
        Assert.Equal(0, state.Ready.WaitSamplesTaken); Assert.Equal(2, state.Event.Tasks.TotalStarted); Assert.Equal(1, state.Event.Tasks.LiveTasks);
        state = Through(state, general + 32 + ready + 8); Assert.True(state.Ready!.ChildIsTerminal); Assert.False(state.Ready.IsTerminal);
        state = Through(state, general + 32 + ready + 9); Assert.True(state.Ready!.IsTerminal); Assert.False(state.Event.Exited);
        state = Through(state, general + ready + 42); Assert.True(state.Event.Exited); Assert.Equal(0, state.Event.Tasks.LiveTasks);
        Assert.Equal(new long[] { 1, 2 }, state.Event.Tasks.Tasks.Select(t => t.Id.Sequence));
        Assert.All(state.Event.Tasks.Tasks, t => { Assert.Equal(SceneTaskPhase.Completed, t.Phase); Assert.Equal(1, t.Revision); });
        Assert.Equal("sample:consumer/one", state.Event.ConsumerId.Value); Assert.Equal(3, state.Event.Tasks.NextSequence);
    }
    [Fact]
    public void Same_tick_handoff_circle_is_discarded_without_double_stepping_ready()
    {
        var state = Through(Start(), 7, 5, 7);
        Assert.Equal(7, state.Ready!.StartTick); Assert.Equal(48, state.Ready.DrawScale); Assert.Null(state.Ready.ReleaseTick);
        Assert.Equal(0, state.Ready.WaitSamplesTaken); Assert.Equal(GeneralBannerExitCause.CircleSkipped, state.General!.ExitCause);
        state = Through(state, 8); Assert.Equal(56, state.Ready!.DrawScale); Assert.Equal(1, state.Ready.WaitSamplesTaken);
    }
    [Fact]
    public void Next_tick_circle_releases_ready_but_still_waits_for_child_and_dispatcher()
    {
        var state = Through(Start(), 8, 5, 8);
        Assert.Equal(128, state.Ready!.DrawScale); Assert.Equal(8, state.Ready.ReleaseTick); Assert.Equal(ReadyExitCause.CircleSkipped, state.Ready.ExitCause);
        state = Through(state, 16); Assert.True(state.Ready!.ChildIsTerminal); Assert.False(state.Ready.IsTerminal);
        state = Through(state, 18); Assert.True(state.Event.Exited);
    }
    [Fact]
    public void Circle_after_ready_release_does_not_change_state_or_trace_sample()
    {
        var state = Through(Start(0, 0), 32);
        var no = BannerReadyExecutor.AdvanceTick(state, false); var yes = BannerReadyExecutor.AdvanceTick(state, true);
        Assert.Equal(JsonSerializer.Serialize(no), JsonSerializer.Serialize(yes));
    }
    [Theory]
    [InlineData(1, 1)] [InlineData(2, 0)]
    public void Initial_late_budget_failure_rolls_back_birth_and_ledger(int instructions, int updates)
    { var s = Fresh(); Reject(s, BannerReadyExecutor.RunUntilBlocked(s, instructions, updates)); Assert.Null(s.General); }
    [Theory]
    [InlineData(1, 1)] [InlineData(2, 0)]
    public void Handoff_late_failure_rolls_back_poll_birth_and_sequence_number(int instructions, int updates)
    {
        var s = Through(Start(), 91);
        Reject(s, BannerReadyExecutor.AdvanceTick(s, true, instructions, updates));
        Assert.Equal(2, s.Event.Tasks.NextSequence); Assert.Null(s.Ready); Assert.Equal(SceneBarrierStatus.Pending, s.Event.Tasks.BarrierStatus);
        var accepted = Accept(BannerReadyExecutor.AdvanceTick(s, true)); Assert.Equal(48, accepted.Ready!.DrawScale);
    }
    [Theory]
    [InlineData(90)] [InlineData(160)]
    public void Terminal_update_failure_rolls_back_all_worker_progress(int tick)
    {
        var s = Through(Start(), tick); Reject(s, BannerReadyExecutor.AdvanceTick(s, false, 0, 0));
        Assert.Equal(tick, s.Event.Tick); Assert.Equal(1, s.Event.Tasks.LiveTasks);
        Assert.Equal(tick + 1, Accept(BannerReadyExecutor.AdvanceTick(s, false)).Event.Tick);
    }
    [Fact]
    public void Pending_poll_needs_no_instruction_or_update_budget()
    { var s = Through(Start(), 4); var r = BannerReadyExecutor.AdvanceTick(s, false, 0, 0); Assert.Equal(5, Accept(r).Event.Tick); Assert.Equal(0, r.InstructionsExecuted); }
    [Fact]
    public void Both_bindings_must_be_ready_before_first_task()
    {
        var p = Program(); var good = Binding(p);
        foreach (var binding in new BannerReadyBinding?[] { null, good with { Ready = good.Ready with { ArtworkId = new("sample:wrong/art") } }, good with { General = good.Ready } })
        { var s = p.Start(new("sample:scene/a"), new("sample:consumer/a"), binding); Reject(s, BannerReadyExecutor.RunUntilBlocked(s)); }
    }
    [Theory]
    [InlineData(-1, 0)] [InlineData(256, 0)] [InlineData(0, -1)] [InlineData(0, 256)]
    public void Unsigned_byte_hold_domain_remains_closed(int g, int r) => Assert.Throws<ArgumentException>(() => Program(g, r));
    [Fact]
    public void Invalid_commands_and_budgets_publish_nothing()
    {
        var fresh = Fresh(); Reject(fresh, BannerReadyExecutor.AdvanceTick(fresh, false), BannerSceneRejection.NotWaiting);
        var s = Start(); Reject(s, BannerReadyExecutor.RunUntilBlocked(s), BannerSceneRejection.AwaitingTick);
        Reject(s, BannerReadyExecutor.AdvanceTick(s, null), BannerSceneRejection.MissingCircleSample);
        foreach (int budget in new[] { -1, 8193 }) Reject(s, BannerReadyExecutor.AdvanceTick(s, false, budget));
        foreach (int budget in new[] { -1, 65 }) Reject(s, BannerReadyExecutor.AdvanceTick(s, false, 2, budget));
        var terminal = Through(Start(0, 0), 42); Reject(terminal, BannerReadyExecutor.AdvanceTick(terminal, false), BannerSceneRejection.AlreadyExited);
    }
    [Fact]
    public void Complete_state_and_trace_replay_is_independent_of_snapshot_reads()
    {
        var a = Start(1, 1); var b = Start(1, 1);
        while (!a.Event.Exited)
        {
            var first = BannerReadyExecutor.AdvanceTick(a, false); _ = JsonSerializer.Serialize(first.State); _ = JsonSerializer.Serialize(first.State);
            var second = BannerReadyExecutor.AdvanceTick(b, false);
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second)); a = Accept(first); b = Accept(second);
        }
    }
    [Theory]
    [InlineData(9001, 21, false, false)] [InlineData(9002, 22, true, false)] [InlineData(9003, 23, true, true)]
    public void Retained_original_handoff_relation_matches_without_frontend_timing_conversion(int sourceFrame, int tick, bool generalClear, bool readyBorn)
    {
        // ZOD-080's pinned event-4 states: exit yield -> task clear -> parent wait 813 with Ready scale 48.
        Assert.InRange(sourceFrame, 9001, 9003);
        var s = Through(Start(), tick, 21);
        Assert.Equal(generalClear, s.General!.IsTerminal); Assert.Equal(readyBorn, s.Ready is not null);
        Assert.Equal(readyBorn ? 3 : 1, s.Event.WaitingAtOffset);
        if (readyBorn) { Assert.Equal(48, s.Ready!.DrawScale); Assert.Equal(0, s.Ready.WaitSamplesTaken); }
    }
    [Fact]
    public void Other_conditions_concurrent_artwork_or_different_owners_are_not_selected()
    {
        var p = Program();
        Assert.Throws<ArgumentException>(() => new BannerReadyProgram(p.Id, p.General with { Definition = p.General.Definition with { Condition = 9 } }, p.Ready));
        Assert.Throws<ArgumentException>(() => new BannerReadyProgram(p.Id, p.General, p.Ready with { Definition = p.Ready.Definition with { Condition = 1 } }));
        Assert.Throws<ArgumentException>(() => new BannerReadyProgram(p.Id, p.General, p.Ready with { ArtworkId = p.General.ArtworkId }));
        Assert.Throws<ArgumentException>(() => new BannerReadyProgram(p.Id, p.General, p.Ready with { OwnerId = new("sample:owner/other") }));
    }
}
