using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class BattleIntroductionTests
{
    private static BannerReadyState Fresh(int general = 60, int ready = 60)
    {
        var program = new BannerReadyProgram(new("sample:sequence/intro"),
            new(new(new("sample:def/general"), 8, general), new("sample:art/general"), new("sample:owner/intro")),
            new(new(new("sample:def/ready"), 0, ready), new("sample:art/ready"), new("sample:owner/intro")));
        return program.Start(new("sample:scene/intro"), new("sample:consumer/intro"), new(
            new(program.General.Definition.Id, program.General.ArtworkId, program.General.OwnerId),
            new(program.Ready.Definition.Id, program.Ready.ArtworkId, program.Ready.OwnerId)));
    }
    private static BattleIntroductionState Start(int general = 60, int ready = 60)
        => BattleIntroductionState.Begin(BannerReadyExecutor.RunUntilBlocked(Fresh(general, ready)).State);
    private static BattleIntroductionState Through(BattleIntroductionState state, long tick)
    {
        while (state.Introduction.Event.Tick < tick)
        {
            var result = state.AdvanceTick(false); Assert.True(result.Accepted); state = result.State;
        }
        return state;
    }
    [Theory]
    [InlineData(0, 0, 42)] [InlineData(1, 1, 44)] [InlineData(60, 60, 162)]
    [InlineData(255, 255, 552)] [InlineData(60, 0, 102)] [InlineData(0, 60, 102)]
    public void Availability_follows_only_accepted_event_exit_and_all_terminal_tasks(int general, int ready, int exit)
    {
        var state = Start(general, ready); int publications = 0;
        while (!state.BattleAvailable)
        {
            Assert.False(state.Introduction.Event.Exited);
            var result = state.AdvanceTick(false); Assert.True(result.Accepted);
            if (result.BattleBecameAvailable) publications++;
            state = result.State;
        }
        Assert.Equal(exit, state.Introduction.Event.Tick); Assert.Equal(1, publications);
        Assert.True(state.Introduction.General!.IsTerminal); Assert.True(state.Introduction.Ready!.IsTerminal);
        Assert.Equal(0, state.Introduction.Event.Tasks.LiveTasks);
        Assert.Equal(2, state.Introduction.Event.Tasks.TotalStarted);
        var repeated = state.AdvanceTick(true);
        Assert.False(repeated.Accepted); Assert.Same(state, repeated.State); Assert.False(repeated.BattleBecameAvailable);
        Assert.Null(repeated.IntroductionResult); Assert.Equal(BattleIntroductionRejection.AlreadyAvailable, repeated.Rejection);
    }
    [Fact]
    public void Hidden_ready_child_clear_and_dispatcher_clear_do_not_open_battle()
    {
        var state = Through(Start(), 159); Assert.Null(state.Introduction.Ready!.DrawScale); Assert.False(state.BattleAvailable);
        state = Through(state, 160); Assert.True(state.Introduction.Ready!.ChildIsTerminal); Assert.False(state.BattleAvailable);
        state = Through(state, 161); Assert.True(state.Introduction.Ready!.IsTerminal); Assert.False(state.BattleAvailable);
        Assert.False(state.Introduction.Event.Exited);
        Assert.True(state.AdvanceTick(false).BattleBecameAvailable);
    }
    [Fact]
    public void Skipped_intro_discards_birth_sample_and_publishes_at_eighteen()
    {
        var state = Start(); int publications = 0;
        while (!state.BattleAvailable)
        {
            var result = state.AdvanceTick(new long[] { 5, 7, 8 }.Contains(state.Introduction.Event.Tick + 1));
            Assert.True(result.Accepted); state = result.State; publications += result.BattleBecameAvailable ? 1 : 0;
            if (state.Introduction.Event.Tick == 7)
            { Assert.Equal(0, state.Introduction.Ready!.WaitSamplesTaken); Assert.Equal(48, state.Introduction.Ready.DrawScale); }
        }
        Assert.Equal(18, state.Introduction.Event.Tick); Assert.Equal(1, publications);
    }
    [Theory]
    [InlineData(90, 8192, 0)] [InlineData(91, 0, 64)] [InlineData(91, 8192, 0)]
    [InlineData(160, 8192, 0)] [InlineData(161, 0, 64)]
    public void Late_rejections_preserve_gate_identity_tick_trace_and_publication(int before, int instructions, int updates)
    {
        var state = Through(Start(), before); string snapshot = JsonSerializer.Serialize(state);
        var rejected = state.AdvanceTick(false, instructions, updates);
        Assert.False(rejected.Accepted); Assert.Same(state, rejected.State); Assert.False(rejected.BattleBecameAvailable);
        Assert.Empty(rejected.IntroductionResult!.Trace); Assert.Equal(0, rejected.IntroductionResult.AcceptedTaskUpdates);
        Assert.Equal(snapshot, JsonSerializer.Serialize(state));
        var retry = state.AdvanceTick(false); Assert.True(retry.Accepted);
        Assert.Equal(before == 161, retry.BattleBecameAvailable);
    }
    [Theory]
    [InlineData(-1, 64)] [InlineData(8193, 64)] [InlineData(8192, -1)] [InlineData(8192, 65)]
    public void Invalid_budgets_do_not_change_ownership(int instructions, int updates)
    {
        var state = Start(); var result = state.AdvanceTick(false, instructions, updates);
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.False(result.BattleBecameAvailable);
    }
    [Fact]
    public void Missing_input_is_rejected_without_advancing_the_gate()
    {
        var state = Start(); var result = state.AdvanceTick(null);
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.False(result.BattleBecameAvailable);
    }
    [Fact]
    public void Gate_cannot_be_attached_to_unadmitted_resumed_or_completed_sequences()
    {
        Assert.Throws<ArgumentNullException>(() => BattleIntroductionState.Begin(null!));
        Assert.Throws<ArgumentException>(() => BattleIntroductionState.Begin(Fresh()));
        Assert.Throws<ArgumentException>(() => BattleIntroductionState.Begin(Through(Start(), 1).Introduction));
        Assert.Throws<ArgumentException>(() => BattleIntroductionState.Begin(Through(Start(), 162).Introduction));
    }
    [Fact]
    public void Pure_replay_includes_the_same_completion_event_without_snapshot_side_effects()
    {
        var a = Start(); var b = Start();
        while (!a.BattleAvailable)
        {
            _ = JsonSerializer.Serialize(a); _ = a.Introduction.Ready?.DrawScale;
            var left = a.AdvanceTick(false); var right = b.AdvanceTick(false);
            Assert.Equal(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right)); a = left.State; b = right.State;
        }
    }
}
