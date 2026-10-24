using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Fft.Core.Events;
using Xunit;

namespace Fft.Core.Tests;

public sealed class SceneTaskLedgerTests
{
    private static readonly AssetId Scene = new("sample:scene/a");
    private static readonly AssetId Owner = new("sample:owner/a");
    private static readonly AssetId Consumer = new("sample:consumer/a");
    private static readonly AssetId Continuation = new("sample:continuation/a");
    private static SceneTaskUpdate.Start Start(SceneTaskFamily family = SceneTaskFamily.Camera) => new(family, Owner);
    private static SceneTaskId Id(long sequence) => new(Scene, sequence);
    private static SceneTaskState Empty() => SceneTaskState.Create(Scene);
    private static SceneTaskState Camera() => SceneTaskLedger.ApplyUpdates(Empty(), [Start()]).State;
    private static SceneTaskState Barrier(SceneTaskState state, SceneTaskClass selector = SceneTaskClass.Camera)
        => SceneTaskLedger.BeginBarrier(state, selector, Consumer, 9, Continuation).State;

    [Fact]
    public void New_scene_is_ready_with_no_event_lifecycle_or_implicit_work()
    {
        var state = Empty();
        Assert.Equal(Scene, state.SceneRunId);
        Assert.Equal(0, state.Tick);
        Assert.Equal(1, state.NextSequence);
        Assert.Equal(0, state.TotalStarted);
        Assert.Equal(0, state.LiveTasks);
        Assert.Empty(state.Tasks);
        Assert.Null(state.Barrier);
        Assert.Equal(SceneBarrierStatus.Ready, state.BarrierStatus);
        var tick = SceneTaskLedger.AdvanceTick(state, [], 0);
        Assert.True(tick.Accepted);
        Assert.Equal(1, tick.State.Tick);
        Assert.Null(tick.State.Barrier);
        Assert.Single(tick.Trace);
        Assert.Equal(SceneTaskTraceKind.TickAdvanced, tick.Trace[0].Kind);
    }

    [Fact]
    public void Apply_does_not_tick_or_poll_and_identical_starts_are_distinct()
    {
        var initial = Empty();
        var result = SceneTaskLedger.ApplyUpdates(initial, [Start(), Start()]);
        Assert.True(result.Accepted);
        Assert.Equal(2, result.AcceptedUpdates);
        Assert.Equal(0, result.TicksConsumed);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(3, result.State.NextSequence);
        Assert.Equal(new[] { Id(1), Id(2) }, result.State.Tasks.Select(t => t.Id));
        Assert.All(result.State.Tasks, t =>
        {
            Assert.Equal(Owner, t.OwnerId);
            Assert.Equal(SceneTaskFamily.Camera, t.Family);
            Assert.Equal(SceneTaskPhase.Running, t.Phase);
            Assert.Equal(0, t.Revision);
        });
        Assert.Equal(new[] { SceneTaskTraceKind.TaskStarted, SceneTaskTraceKind.TaskStarted }, result.Trace.Select(t => t.Kind));
        Assert.Null(result.Trace[0].Before);
        Assert.Equal(result.State.Tasks[0], result.Trace[0].After);
        Assert.Empty(initial.Tasks);
        Assert.Equal(1, initial.NextSequence);
        Assert.Null(result.State.Barrier);
    }

    [Fact]
    public void Ordered_start_phase_phase_complete_can_reference_allocated_task_in_same_batch()
    {
        var initial = Empty();
        var result = SceneTaskLedger.ApplyUpdates(initial, [Start(SceneTaskFamily.DarkScreen),
            new SceneTaskUpdate.Phase(Id(1), 0, SceneTaskPhase.Holding),
            new SceneTaskUpdate.Phase(Id(1), 1, SceneTaskPhase.Leaving), new SceneTaskUpdate.Complete(Id(1), 2)]);
        Assert.True(result.Accepted);
        Assert.Equal(4, result.AcceptedUpdates);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(1, result.State.TotalStarted);
        Assert.Equal(0, result.State.LiveTasks);
        var task = Assert.Single(result.State.Tasks);
        Assert.Equal(SceneTaskPhase.Completed, task.Phase);
        Assert.Equal(3, task.Revision);
        Assert.Equal(2, result.State.NextSequence);
        Assert.Null(task.ActiveClass);
        Assert.Null(result.State.Barrier);
        Assert.Equal(new[] { SceneTaskTraceKind.TaskStarted, SceneTaskTraceKind.TaskPhaseChanged,
            SceneTaskTraceKind.TaskPhaseChanged, SceneTaskTraceKind.TaskCompleted }, result.Trace.Select(t => t.Kind));
        Assert.Equal(new long[] { 0, 1, 2, 3 }, result.Trace.Select(t => t.After!.Revision));
        Assert.Equal(new[] { SceneTaskPhase.Entering, SceneTaskPhase.Holding, SceneTaskPhase.Leaving, SceneTaskPhase.Completed },
            result.Trace.Select(t => t.After!.Phase));
    }

    [Theory]
    [InlineData(SceneTaskFamily.Camera, SceneTaskPhase.Running, SceneTaskClass.Camera)]
    [InlineData(SceneTaskFamily.ConditionDisplay, SceneTaskPhase.Running, SceneTaskClass.ConditionDisplay)]
    [InlineData(SceneTaskFamily.DarkScreen, SceneTaskPhase.Entering, SceneTaskClass.DarkScreenTransition)]
    [InlineData(SceneTaskFamily.DarkScreen, SceneTaskPhase.Holding, SceneTaskClass.DarkScreenHold)]
    [InlineData(SceneTaskFamily.DarkScreen, SceneTaskPhase.Leaving, SceneTaskClass.DarkScreenTransition)]
    public void Every_active_phase_has_the_selected_class_and_can_be_acknowledged_cancelled(
        SceneTaskFamily family, SceneTaskPhase phase, SceneTaskClass expectedClass)
    {
        var state = AtPhase(family, phase);
        var task = state.Tasks[0];
        Assert.Equal(expectedClass, task.ActiveClass);
        var pending = Barrier(state, expectedClass);
        var blocked = SceneTaskLedger.AdvanceTick(pending, []);
        Assert.Equal(SceneBarrierStatus.Pending, blocked.State.BarrierStatus);
        Assert.Equal(Id(1), Assert.Single(blocked.Trace[^1].MatchingTasks));
        var cancelled = SceneTaskLedger.AdvanceTick(blocked.State,
            [new SceneTaskUpdate.Cancel(Id(1), task.Revision, SceneTaskCancellationReason.OwnerAcknowledgedCancellation)]);
        Assert.True(cancelled.Accepted);
        Assert.Equal(SceneBarrierStatus.Released, cancelled.State.BarrierStatus);
        Assert.Equal(SceneTaskPhase.Cancelled, cancelled.State.Tasks[0].Phase);
        Assert.Equal(task.Revision + 1, cancelled.State.Tasks[0].Revision);
        Assert.Equal(SceneTaskCancellationReason.OwnerAcknowledgedCancellation, cancelled.State.Tasks[0].CancellationReason);
        Assert.Null(cancelled.State.Tasks[0].ActiveClass);
        Assert.Equal(2, cancelled.State.Tick);
        Assert.Equal(0, cancelled.State.LiveTasks);
    }

    [Theory]
    [InlineData(SceneTaskFamily.Camera, SceneTaskClass.Camera)]
    [InlineData(SceneTaskFamily.ConditionDisplay, SceneTaskClass.ConditionDisplay)]
    public void Running_family_completion_preserves_identity_and_only_releases_on_a_later_poll(SceneTaskFamily family, SceneTaskClass selector)
    {
        var running = AtPhase(family, SceneTaskPhase.Running);
        var completed = SceneTaskLedger.ApplyUpdates(running, [new SceneTaskUpdate.Complete(Id(1), 0)]);
        Assert.True(completed.Accepted);
        Assert.Equal(0, completed.State.Tick);
        Assert.Null(completed.State.Barrier);
        Assert.Equal(0, completed.State.LiveTasks);
        Assert.Equal(new SceneTask(Id(1), Owner, family, SceneTaskPhase.Completed, 1), Assert.Single(completed.State.Tasks));
        var pending = Barrier(completed.State, selector);
        Assert.Equal(SceneBarrierStatus.Pending, pending.BarrierStatus);
        Assert.Equal(0, pending.Tick);
        var released = SceneTaskLedger.AdvanceTick(pending, []);
        Assert.Equal(SceneBarrierStatus.Released, released.State.BarrierStatus);
        Assert.Equal(1, released.State.Tick);
        Assert.Equal(SceneTaskPhase.Running, running.Tasks[0].Phase);
    }

    [Theory]
    [InlineData(SceneTaskPhase.Entering)]
    [InlineData(SceneTaskPhase.Holding)]
    public void Dark_screen_cannot_complete_before_leaving(SceneTaskPhase phase)
    {
        var state = AtPhase(SceneTaskFamily.DarkScreen, phase);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Complete(Id(1), state.Tasks[0].Revision)]),
            SceneTaskRejection.IllegalTaskTransition, 0);
    }

    [Theory]
    [InlineData(SceneTaskPhase.Running)]
    [InlineData(SceneTaskPhase.Entering)]
    [InlineData(SceneTaskPhase.Holding)]
    [InlineData(SceneTaskPhase.Completed)]
    [InlineData(SceneTaskPhase.Cancelled)]
    public void Dark_screen_phase_command_cannot_repeat_reverse_or_bypass_terminal_commands(SceneTaskPhase target)
    {
        var state = AtPhase(SceneTaskFamily.DarkScreen, SceneTaskPhase.Holding);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Phase(Id(1), 1, target)]),
            SceneTaskRejection.IllegalTaskTransition, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Terminal_tombstones_reject_every_late_update_before_revision_checks(bool cancel)
    {
        var state = SceneTaskLedger.ApplyUpdates(Camera(), [cancel
            ? new SceneTaskUpdate.Cancel(Id(1), 0, SceneTaskCancellationReason.OwnerAcknowledgedCancellation)
            : new SceneTaskUpdate.Complete(Id(1), 0)]).State;
        SceneTaskUpdate[] late = [new SceneTaskUpdate.Complete(Id(1), 0),
            new SceneTaskUpdate.Cancel(Id(1), 9, SceneTaskCancellationReason.OwnerAcknowledgedCancellation),
            new SceneTaskUpdate.Phase(Id(1), 1, SceneTaskPhase.Running)];
        foreach (var update in late)
            Rejected(state, SceneTaskLedger.ApplyUpdates(state, [update]), SceneTaskRejection.DuplicateTerminalUpdate, 0);
        var next = SceneTaskLedger.ApplyUpdates(state, [Start()]);
        Assert.Equal(2, next.State.TotalStarted);
        Assert.Equal(Id(2), next.State.Tasks[1].Id);
        Assert.Equal(state.Tasks[0], next.State.Tasks[0]);
    }

    [Fact]
    public void Duplicate_terminal_or_reused_revision_in_batch_rolls_back_previous_changes()
    {
        var state = Camera();
        Rejected(state, SceneTaskLedger.AdvanceTick(state, [new SceneTaskUpdate.Complete(Id(1), 0), new SceneTaskUpdate.Complete(Id(1), 0)]),
            SceneTaskRejection.DuplicateTerminalUpdate, 1);
        var dark = AtPhase(SceneTaskFamily.DarkScreen, SceneTaskPhase.Entering);
        Rejected(dark, SceneTaskLedger.AdvanceTick(dark, [new SceneTaskUpdate.Phase(Id(1), 0, SceneTaskPhase.Holding),
            new SceneTaskUpdate.Phase(Id(1), 0, SceneTaskPhase.Leaving)]), SceneTaskRejection.StaleTaskRevision, 1);
    }

    [Fact]
    public void Poll_reports_all_current_matching_ids_in_sequence_order_across_owners()
    {
        var setup = SceneTaskLedger.ApplyUpdates(Empty(), [Start(), Start(SceneTaskFamily.ConditionDisplay),
            new SceneTaskUpdate.Start(SceneTaskFamily.Camera, new("sample:owner/other")), Start()]).State;
        var pending = Barrier(setup);
        var result = SceneTaskLedger.AdvanceTick(pending, [new SceneTaskUpdate.Complete(Id(3), 0),
            new SceneTaskUpdate.Start(SceneTaskFamily.Camera, new("sample:owner/third"))]);
        Assert.True(result.Accepted);
        Assert.Equal(new[] { Id(1), Id(4), Id(5) }, result.Trace[^1].MatchingTasks.ToArray());
        Assert.Equal(SceneBarrierStatus.Pending, result.State.BarrierStatus);
        Assert.Equal(new[] { SceneTaskTraceKind.TickAdvanced, SceneTaskTraceKind.TaskCompleted,
            SceneTaskTraceKind.TaskStarted, SceneTaskTraceKind.BarrierPolled }, result.Trace.Select(t => t.Kind));
        Assert.Equal(5, result.State.TotalStarted);
        Assert.Equal(4, result.State.LiveTasks);
    }

    [Fact]
    public void Released_barrier_does_not_repoll_when_matching_work_starts_later()
    {
        var released = SceneTaskLedger.AdvanceTick(Barrier(Empty()), []).State;
        var applied = SceneTaskLedger.ApplyUpdates(released, [Start()]);
        Assert.True(applied.Accepted);
        Assert.Equal(released.Barrier, applied.State.Barrier);
        Assert.Equal(1, applied.State.Tick);
        Assert.Equal(SceneTaskTraceKind.TaskStarted, Assert.Single(applied.Trace).Kind);
        var ticked = SceneTaskLedger.AdvanceTick(applied.State, []);
        Assert.Equal(SceneBarrierStatus.Released, ticked.State.BarrierStatus);
        Assert.Equal(SceneTaskTraceKind.TickAdvanced, Assert.Single(ticked.Trace).Kind);
        var fresh = Barrier(ticked.State);
        Assert.Equal(2, fresh.Barrier!.EntryTick);
        Assert.Equal(SceneBarrierStatus.Pending, SceneTaskLedger.AdvanceTick(fresh, []).State.BarrierStatus);
    }

    [Fact]
    public void Pending_apply_rejects_even_empty_batch_and_cannot_bypass_poll_boundary()
    {
        var state = Barrier(Camera());
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Complete(Id(1), 0)]), SceneTaskRejection.AwaitingTick);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, []), SceneTaskRejection.AwaitingTick);
    }

    [Fact]
    public void Live_capacity_is_checked_after_each_update_and_terminal_records_are_never_evicted()
    {
        var full = SceneTaskLedger.ApplyUpdates(Empty(), Enumerable.Repeat<SceneTaskUpdate>(Start(), 64).ToImmutableArray()).State;
        Assert.Equal(64, full.LiveTasks);
        Assert.Equal(65, full.NextSequence);
        Rejected(full, SceneTaskLedger.AdvanceTick(full, [Start(), new SceneTaskUpdate.Complete(Id(1), 0)]),
            SceneTaskRejection.LiveTaskLimitExceeded, 0);
        var accepted = SceneTaskLedger.AdvanceTick(full, [new SceneTaskUpdate.Complete(Id(1), 0), Start()]);
        Assert.True(accepted.Accepted);
        Assert.Equal(64, accepted.State.LiveTasks);
        Assert.Equal(65, accepted.State.TotalStarted);
        Assert.Equal(66, accepted.State.NextSequence);
        Assert.Equal(SceneTaskPhase.Completed, accepted.State.Tasks[0].Phase);
        Assert.Equal(Id(65), accepted.State.Tasks[^1].Id);
        Assert.Equal(64, full.TotalStarted);
        Assert.Equal(SceneTaskPhase.Running, full.Tasks[0].Phase);
    }

    [Fact]
    public void Total_start_limit_includes_tombstones_and_rejection_cannot_leak_candidate_allocation()
    {
        var state = Empty();
        for (int batch = 0; batch < 4; batch++)
        {
            var starts = SceneTaskLedger.ApplyUpdates(state, Enumerable.Repeat<SceneTaskUpdate>(Start(), batch == 3 ? 63 : 64).ToImmutableArray());
            Assert.True(starts.Accepted);
            var done = SceneTaskLedger.ApplyUpdates(starts.State, starts.State.Tasks.Where(t => !t.IsTerminal)
                .Select(t => (SceneTaskUpdate)new SceneTaskUpdate.Complete(t.Id, 0)).ToImmutableArray());
            Assert.True(done.Accepted);
            state = done.State;
        }
        Assert.Equal(255, state.TotalStarted);
        Rejected(state, SceneTaskLedger.AdvanceTick(state, [Start(), Start()]), SceneTaskRejection.TotalTaskLimitExceeded, 1);
        var last = SceneTaskLedger.ApplyUpdates(state, [Start()]);
        Assert.True(last.Accepted);
        Assert.Equal(Id(256), last.State.Tasks[^1].Id);
        state = SceneTaskLedger.ApplyUpdates(last.State, [new SceneTaskUpdate.Complete(Id(256), 0)]).State;
        Assert.Equal(256, state.TotalStarted);
        Assert.Equal(0, state.LiveTasks);
        Assert.Equal(257, state.NextSequence);
        Assert.Equal(Enumerable.Range(1, 256).Select(i => Id(i)), state.Tasks.Select(t => t.Id));
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [Start()]), SceneTaskRejection.TotalTaskLimitExceeded, 0);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Complete(Id(1), 0)]), SceneTaskRejection.DuplicateTerminalUpdate, 0);
    }

    [Fact]
    public void Failed_candidate_start_preserves_identity_for_next_accepted_start()
    {
        var state = Camera();
        Rejected(state, SceneTaskLedger.AdvanceTick(state, [Start(), new SceneTaskUpdate.Complete(Id(1), 4)]),
            SceneTaskRejection.StaleTaskRevision, 1);
        var next = SceneTaskLedger.ApplyUpdates(state, [Start()]);
        Assert.True(next.Accepted);
        Assert.Equal(Id(2), next.State.Tasks[^1].Id);
        Assert.Equal(3, next.State.NextSequence);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    public void Invalid_budget_is_typed_and_atomic_on_both_update_commands(int budget)
    {
        var state = Camera();
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [], budget), SceneTaskRejection.InvalidUpdateBudget);
        Rejected(state, SceneTaskLedger.AdvanceTick(state, [], budget), SceneTaskRejection.InvalidUpdateBudget);
    }

    [Fact]
    public void Update_envelope_boundaries_reject_before_task_lookup_or_tick()
    {
        var state = Camera();
        Rejected(state, SceneTaskLedger.AdvanceTick(state, default), SceneTaskRejection.InvalidUpdateBatch);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [Start()], 0), SceneTaskRejection.UpdateBudgetExceeded);
        Rejected(state, SceneTaskLedger.AdvanceTick(state, Enumerable.Repeat<SceneTaskUpdate>(Start(), 65).ToImmutableArray()),
            SceneTaskRejection.UpdateBudgetExceeded);
        Rejected(state, SceneTaskLedger.AdvanceTick(state, [new SceneTaskUpdate.Complete(Id(99), 0), null!]),
            SceneTaskRejection.InvalidUpdate, 1); // Entire shape preflight precedes per-instance validation.
        var zero = SceneTaskLedger.ApplyUpdates(state, [], 0);
        Assert.True(zero.Accepted);
        Assert.Empty(zero.Trace);
        Assert.Equal(state.Tasks.ToArray(), zero.State.Tasks.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(65535)]
    [InlineData(-1)]
    public void Unsupported_selector_rejects_before_pending_boundary(int value)
    {
        var state = Barrier(Camera());
        Rejected(state, SceneTaskLedger.BeginBarrier(state, (SceneTaskClass)value, Consumer, 4, Continuation),
            SceneTaskRejection.UnsupportedTaskClass);
    }

    [Fact]
    public void Malformed_updates_ids_and_barrier_metadata_have_typed_diagnostics()
    {
        var state = Camera();
        (SceneTaskUpdate Update, SceneTaskRejection Reason)[] malformed =
        [
            (Start((SceneTaskFamily)99), SceneTaskRejection.UnsupportedTaskFamily),
            (new SceneTaskUpdate.Start(SceneTaskFamily.Camera, default), SceneTaskRejection.InvalidId),
            (new SceneTaskUpdate.Complete(new(default, 1), 0), SceneTaskRejection.InvalidId),
            (new SceneTaskUpdate.Complete(Id(0), 0), SceneTaskRejection.InvalidUpdate),
            (new SceneTaskUpdate.Complete(Id(1), -1), SceneTaskRejection.InvalidUpdate),
            (new SceneTaskUpdate.Phase(Id(1), 0, (SceneTaskPhase)99), SceneTaskRejection.InvalidUpdate),
            (new SceneTaskUpdate.Cancel(Id(1), 0, (SceneTaskCancellationReason)99), SceneTaskRejection.InvalidUpdate),
        ];
        foreach (var item in malformed)
            Rejected(state, SceneTaskLedger.AdvanceTick(state, [item.Update]), item.Reason, 0);
        Rejected(state, SceneTaskLedger.BeginBarrier(state, SceneTaskClass.Camera, default, 4, Continuation), SceneTaskRejection.InvalidId);
        Rejected(state, SceneTaskLedger.BeginBarrier(state, SceneTaskClass.Camera, Consumer, 4, default), SceneTaskRejection.InvalidId);
        Rejected(state, SceneTaskLedger.BeginBarrier(state, SceneTaskClass.Camera, Consumer, -1, Continuation), SceneTaskRejection.InvalidSourceOffset);
    }

    [Fact]
    public void Id_limits_apply_to_scene_owner_foreign_reference_consumer_and_continuation()
    {
        var max = new AssetId("x:" + new string('a', 254));
        var tooLong = new AssetId("x:" + new string('a', 255));
        Assert.Throws<ArgumentException>(() => SceneTaskState.Create(default));
        Assert.Throws<ArgumentException>(() => SceneTaskState.Create(tooLong));
        Assert.Throws<ArgumentException>(() => new AssetId("BAD ID"));
        var state = SceneTaskState.Create(max);
        Assert.Equal(max, state.SceneRunId);
        var accepted = SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, max)]);
        Assert.True(accepted.Accepted);
        Assert.True(SceneTaskLedger.BeginBarrier(accepted.State, SceneTaskClass.Camera, max, int.MaxValue, max).Accepted);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, tooLong)]), SceneTaskRejection.InvalidId, 0);
        Rejected(state, SceneTaskLedger.AdvanceTick(state, [new SceneTaskUpdate.Complete(new(tooLong, 1), 0)]), SceneTaskRejection.InvalidId, 0);
        Rejected(state, SceneTaskLedger.BeginBarrier(state, SceneTaskClass.Camera, tooLong, 4, Continuation), SceneTaskRejection.InvalidId);
        Rejected(state, SceneTaskLedger.BeginBarrier(state, SceneTaskClass.Camera, Consumer, 4, tooLong), SceneTaskRejection.InvalidId);
    }

    [Fact]
    public void Referenced_update_error_precedence_is_scene_then_existence_then_terminal_then_revision_then_phase()
    {
        var state = AtPhase(SceneTaskFamily.DarkScreen, SceneTaskPhase.Holding);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Phase(new(new("sample:scene/b"), 99), 9, SceneTaskPhase.Entering)]),
            SceneTaskRejection.WrongScene, 0);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Phase(Id(99), 9, SceneTaskPhase.Entering)]),
            SceneTaskRejection.UnknownTask, 0);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Phase(Id(1), 0, SceneTaskPhase.Entering)]),
            SceneTaskRejection.StaleTaskRevision, 0);
        Rejected(state, SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Phase(Id(1), 1, SceneTaskPhase.Entering)]),
            SceneTaskRejection.IllegalTaskTransition, 0);
    }

    [Theory]
    [InlineData("tick")]
    [InlineData("sequence")]
    [InlineData("revision")]
    public void Checked_counter_exhaustion_rejects_atomically_without_public_restore_surface(string counter)
    {
        // Fault injection only: finite proof budgets prevent reaching sequence/revision overflow through public commands.
        var initial = Camera();
        var state = (SceneTaskState)Activator.CreateInstance(typeof(SceneTaskState), BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [Scene, counter == "tick" ? long.MaxValue : 0L,
                counter == "sequence" ? long.MaxValue : 2L,
                counter == "revision" ? ImmutableArray.Create(initial.Tasks[0] with { Revision = long.MaxValue }) : initial.Tasks,
                initial.Barrier], culture: null)!;
        ImmutableArray<SceneTaskUpdate> updates = counter switch
        {
            "sequence" => [Start()],
            "revision" => [new SceneTaskUpdate.Complete(Id(1), long.MaxValue)],
            _ => [],
        };
        Rejected(state, SceneTaskLedger.AdvanceTick(state, updates), SceneTaskRejection.CounterOverflow, counter == "tick" ? null : 0);
    }

    [Fact]
    public void Repeated_pending_ticks_are_bounded_and_never_force_task_or_event_completion()
    {
        var input = Barrier(Camera());
        var state = input;
        for (int i = 1; i <= 100; i++)
        {
            var result = SceneTaskLedger.AdvanceTick(state, []);
            Assert.True(result.Accepted);
            Assert.Equal(i, result.State.Tick);
            Assert.Equal(2, result.Trace.Length);
            Assert.Equal(SceneBarrierStatus.Pending, result.State.BarrierStatus);
            Assert.Equal(input.Tasks.ToArray(), result.State.Tasks.ToArray());
            state = result.State;
        }
        Assert.Equal(0, input.Tick);
        Assert.Equal(0, input.Barrier!.EntryTick);
    }

    private static SceneTaskState AtPhase(SceneTaskFamily family, SceneTaskPhase phase)
    {
        var state = SceneTaskLedger.ApplyUpdates(Empty(), [Start(family)]).State;
        if (phase is SceneTaskPhase.Holding or SceneTaskPhase.Leaving)
            state = SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Phase(Id(1), 0, SceneTaskPhase.Holding)]).State;
        if (phase == SceneTaskPhase.Leaving)
            state = SceneTaskLedger.ApplyUpdates(state, [new SceneTaskUpdate.Phase(Id(1), 1, SceneTaskPhase.Leaving)]).State;
        return state;
    }

    private static void Rejected(SceneTaskState input, SceneTaskResult result, SceneTaskRejection reason, int? index = null)
    {
        string before = JsonSerializer.Serialize(input);
        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Rejection);
        Assert.Same(input, result.State);
        Assert.Empty(result.Trace);
        Assert.Equal(0, result.AcceptedUpdates);
        Assert.Equal(0, result.TicksConsumed);
        Assert.NotNull(result.Diagnostic);
        Assert.Equal(index, result.FailureUpdateIndex);
        Assert.Equal(before, JsonSerializer.Serialize(result.State));
    }
}
