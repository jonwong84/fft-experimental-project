using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Zodiac.Framework.Events;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class BannerSceneBoundaryTests
{
    private static readonly AssetId Scene = new("sample:scene/a");
    private static readonly AssetId Owner = new("sample:owner/a");
    private static readonly AssetId Consumer = new("sample:consumer/a");
    private static readonly GeneralBannerDefinition Definition = new(new("sample:effect/banner"), 8, 0);
    private static SceneEventInstruction Exit(int offset) => new(new ConditionalEventInstruction(offset, ConditionalEventOperation.Exit));
    private static SceneEventProgram Program(bool exit = false) => new(new("sample:event/banner"), exit ? [Exit(4)] : [new(4, SceneTaskClass.ConditionDisplay), Exit(7)]);
    private static BannerSceneState Empty(SceneTaskState? tasks = null, bool exit = false)
        => BannerSceneState.Create(Program(exit).Start(default, tasks ?? SceneTaskState.Create(Scene), Consumer));
    private static BannerSceneState Attached(int hold = 0, SceneTaskState? tasks = null)
        => BannerSceneExecutor.AttachBanner(Empty(tasks), Definition with { HoldFrames = hold }, Owner).State;
    private static BannerSceneState Waiting(int hold = 0, SceneTaskState? tasks = null)
        => BannerSceneExecutor.RunUntilBlocked(Attached(hold, tasks), 1).State;
    private static BannerSceneState Through(int tick, int hold = 0)
    {
        var state = Waiting(hold);
        for (int i = 0; i < tick; i++)
        {
            var result = BannerSceneExecutor.AdvanceTick(state, false, [], 1);
            Assert.True(result.Accepted, result.Diagnostic);
            state = result.State;
        }
        return state;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_tick_requires_an_explicit_Circle_sample_including_after_terminal(bool terminal)
    {
        var state = terminal ? Through(31) : Waiting();
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, null, [], 1), BannerSceneRejection.MissingCircleSample);
    }

    [Theory]
    [InlineData("definition-default")]
    [InlineData("definition-long")]
    [InlineData("owner-default")]
    [InlineData("owner-long")]
    public void Attachment_ids_are_nondefault_and_bounded_before_allocation(string problem)
    {
        var state = Empty();
        var tooLong = new AssetId("x:" + new string('a', 255));
        var definition = Definition with { Id = problem == "definition-default" ? default : problem == "definition-long" ? tooLong : Definition.Id };
        var owner = problem == "owner-default" ? default : problem == "owner-long" ? tooLong : Owner;
        Rejected(state, BannerSceneExecutor.AttachBanner(state, definition, owner), BannerSceneRejection.InvalidId);
        Assert.Equal(1, state.Event.Tasks.NextSequence);
        Assert.Null(state.Banner);
    }

    [Fact]
    public void Maximum_ids_and_attachment_to_exited_scene_at_nonzero_tick_do_not_consume_a_tick()
    {
        var id = new AssetId("x:" + new string('a', 254));
        var sceneEvent = SceneEventExecutor.RunUntilBlocked(Program(true).Start(default, SceneTaskState.Create(Scene), Consumer), 1).State;
        for (int i = 0; i < 3; i++) sceneEvent = SceneEventExecutor.AdvanceTick(sceneEvent, [], 0).State;
        var input = BannerSceneState.Create(sceneEvent);
        var result = BannerSceneExecutor.AttachBanner(input, Definition with { Id = id }, id);
        Assert.True(result.Accepted);
        Assert.Equal(3, result.State.Event.Tick);
        Assert.Equal(3, result.State.Banner!.StartTick);
        Assert.Equal(3, result.State.Banner.LastProcessedTick);
        Assert.True(result.State.Event.Exited);
        Assert.Equal(0, result.TicksConsumed);
        var tick = BannerSceneExecutor.AdvanceTick(result.State, false, [], 0, 0);
        Assert.True(tick.Accepted);
        Assert.Equal(4, tick.State.Event.Tick);
        Assert.Equal(1, tick.State.Banner!.RevealCompleted);
        Assert.True(tick.State.Event.Exited);
        Assert.Equal(0, tick.InstructionsExecuted);
    }

    [Theory]
    [InlineData(-1, SceneTaskRejection.InvalidUpdateBudget)]
    [InlineData(65, SceneTaskRejection.InvalidUpdateBudget)]
    [InlineData(0, SceneTaskRejection.UpdateBudgetExceeded)]
    public void Attach_budget_errors_are_atomic(int budget, SceneTaskRejection reason)
    {
        var state = Empty();
        Rejected(state, BannerSceneExecutor.AttachBanner(state, Definition, Owner, budget), BannerSceneRejection.TaskLedgerRejected, reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cannot_attach_to_suspended_interpreter_even_if_no_effect_is_present(bool frames)
    {
        var program = new SceneEventProgram(new("sample:event/wait"), [frames ? new(4, 1) : new(4, SceneTaskClass.Camera), Exit(7)]);
        var state = BannerSceneState.Create(SceneEventExecutor.RunUntilBlocked(program.Start(default, SceneTaskState.Create(Scene), Consumer), 1).State);
        Rejected(state, BannerSceneExecutor.AttachBanner(state, Definition, Owner), BannerSceneRejection.AwaitingTick);
    }

    [Fact]
    public void Run_commands_do_not_advance_effect_and_errors_do_not_publish_VM_or_task_changes()
    {
        var ready = Attached();
        Rejected(ready, BannerSceneExecutor.RunUntilBlocked(ready, 0), BannerSceneRejection.InstructionBudgetExceeded);
        Rejected(ready, BannerSceneExecutor.AdvanceTick(ready, false, [], 1), BannerSceneRejection.NotWaiting);
        var result = BannerSceneExecutor.RunUntilBlocked(ready, 1);
        Assert.Same(ready.Banner, result.State.Banner);
        Assert.Equal(0, result.State.Event.Tick);
        Assert.Equal(0, result.AcceptedEffectTicks);
        Rejected(result.State, BannerSceneExecutor.RunUntilBlocked(result.State, 1), BannerSceneRejection.AwaitingTick);
        var noEffect = BannerSceneState.Create(SceneEventExecutor.RunUntilBlocked(Program(true).Start(default, SceneTaskState.Create(Scene), Consumer), 1).State);
        Rejected(noEffect, BannerSceneExecutor.AdvanceTick(noEffect, false, [], 0), BannerSceneRejection.EffectNotAttached);
        var noDefinition = Empty();
        Rejected(noDefinition, BannerSceneExecutor.AttachBanner(noDefinition, null, Owner), BannerSceneRejection.InvalidDefinition);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8193)]
    public void Instruction_budget_is_validated_before_input_or_command_state(int budget)
    {
        var ready = Attached();
        Rejected(ready, BannerSceneExecutor.RunUntilBlocked(ready, budget), BannerSceneRejection.InvalidInstructionBudget);
        Rejected(ready, BannerSceneExecutor.AdvanceTick(ready, null, default, budget), BannerSceneRejection.InvalidInstructionBudget);
    }

    [Theory]
    [InlineData("program")]
    [InlineData("variables")]
    [InlineData("default-consumer")]
    [InlineData("long-consumer")]
    [InlineData("same-consumer")]
    public void Next_event_selection_rejects_invalid_inputs_without_resetting_retained_scene(string problem)
    {
        var state = Through(31);
        var program = problem == "program" ? null : Program();
        var variables = new ConditionalEventVariables(problem == "variables" ? -1 : 0, 0, 0, 0);
        var consumer = problem switch
        {
            "default-consumer" => default,
            "long-consumer" => new AssetId("x:" + new string('a', 255)),
            "same-consumer" => Consumer,
            _ => new AssetId("sample:consumer/next"),
        };
        Rejected(state, BannerSceneExecutor.StartNextEvent(state, program, variables, consumer), problem switch
        {
            "program" => BannerSceneRejection.InvalidProgram,
            "variables" => BannerSceneRejection.InvalidVariables,
            _ => BannerSceneRejection.InvalidId,
        });
    }

    [Fact]
    public void Next_event_requires_exit_and_retains_terminal_effect_without_repeated_acknowledgments()
    {
        var pending = Waiting();
        Rejected(pending, BannerSceneExecutor.StartNextEvent(pending, Program(), default, new("sample:consumer/next")), BannerSceneRejection.EventNotExited);
        var completed = Through(31);
        var next = BannerSceneExecutor.StartNextEvent(completed, Program(), new(1, 2, 3, 4), new("sample:consumer/next"));
        Assert.True(next.Accepted);
        Assert.Same(completed.Banner, next.State.Banner);
        Assert.Same(completed.Event.Tasks, next.State.Event.Tasks);
        var waiting = BannerSceneExecutor.RunUntilBlocked(next.State, 1);
        Assert.Equal(31, waiting.State.Event.Tasks.Barrier!.EntryTick);
        var tick = BannerSceneExecutor.AdvanceTick(waiting.State, true, [], 1, 0);
        Assert.True(tick.Accepted);
        Assert.True(tick.State.Event.Exited);
        Assert.Equal(new(1, 2, 3, 4), tick.State.Event.Variables);
        Assert.Same(completed.Banner, tick.State.Banner);
        Assert.Equal(31, tick.State.Banner!.LastProcessedTick);
        Assert.Equal(32, tick.State.Event.Tick);
        Assert.Equal(0, tick.AcceptedEffectTicks);
        Assert.Equal(0, tick.AcceptedTaskUpdates);
        Assert.Equal(BannerSceneTraceKind.EffectObserved, tick.Trace[1].Kind);
        Assert.DoesNotContain(tick.Trace, t => t.EventTrace?.TaskTrace?.Kind == SceneTaskTraceKind.TaskCompleted);
    }

    [Fact]
    public void Banner_and_F1_share_ticks_and_failed_resume_rolls_back_hide_progress_before_fresh_E5_yield()
    {
        var program = new SceneEventProgram(new("sample:event/frame-banner"), [new(4, 30), new(7, SceneTaskClass.ConditionDisplay), Exit(10)]);
        var state = BannerSceneExecutor.AttachBanner(BannerSceneState.Create(program.Start(default, SceneTaskState.Create(Scene), Consumer)), Definition, Owner).State;
        state = BannerSceneExecutor.RunUntilBlocked(state, 1).State;
        for (int i = 0; i < 29; i++)
        {
            var tick = BannerSceneExecutor.AdvanceTick(state, false, [], 0, 0);
            Assert.True(tick.Accepted);
            state = tick.State;
        }
        Assert.Equal(29, state.Event.Tick);
        Assert.Equal(1, state.Event.RemainingWaitTicks);
        Assert.Equal(9, state.Banner!.HideCompleted);
        string frozen = JsonSerializer.Serialize(state);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, true, [], 0, 0), BannerSceneRejection.InstructionBudgetExceeded);
        Assert.Equal(frozen, JsonSerializer.Serialize(state));
        var entered = BannerSceneExecutor.AdvanceTick(state, false, [], 1, 0);
        Assert.True(entered.Accepted);
        Assert.Equal(GeneralBannerPhase.ExitYield, entered.State.Banner!.Phase);
        Assert.Equal(10, entered.State.Banner.HideCompleted);
        Assert.Equal(SceneEventSuspension.Barrier, entered.State.Event.Suspension);
        Assert.Equal(30, entered.State.Event.Tasks.Barrier!.EntryTick);
        Assert.DoesNotContain(entered.Trace, t => t.EventTrace?.TaskTrace?.Kind == SceneTaskTraceKind.BarrierPolled);
        var done = BannerSceneExecutor.AdvanceTick(entered.State, false, [], 1, 1);
        Assert.True(done.Accepted);
        Assert.Equal(31, done.State.Event.Tick);
        Assert.Equal(GeneralBannerPhase.Completed, done.State.Banner!.Phase);
        Assert.True(done.State.Event.Exited);
    }

    [Fact]
    public void Generated_completion_and_supplied_facts_share_budget_and_failed_allocation_is_reusable()
    {
        var state = Through(30);
        ImmutableArray<SceneTaskUpdate> updates = [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Owner)];
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, false, updates, 1, 1), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UpdateBudgetExceeded);
        Assert.Equal(2, state.Event.Tasks.NextSequence);
        var accepted = BannerSceneExecutor.AdvanceTick(state, false, updates, 1, 2);
        Assert.True(accepted.Accepted);
        Assert.Equal(2, accepted.AcceptedTaskUpdates);
        Assert.Equal(new(Scene, 2), accepted.State.Event.Tasks.Tasks[^1].Id);
        Assert.Equal(1, accepted.State.Event.Tasks.LiveTasks);
        Assert.Equal(new[] { SceneTaskTraceKind.TickAdvanced, SceneTaskTraceKind.TaskStarted, SceneTaskTraceKind.TaskCompleted,
            SceneTaskTraceKind.BarrierPolled, SceneTaskTraceKind.BarrierReleased }, accepted.Trace.Where(t => t.EventTrace?.TaskTrace is not null).Select(t => t.EventTrace!.TaskTrace!.Kind));
    }

    [Fact]
    public void Supplied_start_at_live_capacity_rejects_before_generated_completion_could_free_a_slot()
    {
        var ledger = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), Enumerable.Range(1, 63)
            .Select(_ => (SceneTaskUpdate)new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Owner)).ToImmutableArray()).State;
        var state = Waiting(tasks: ledger);
        for (int i = 0; i < 30; i++) state = BannerSceneExecutor.AdvanceTick(state, false, [], 1).State;
        var start = new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Owner);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, false, [start], 1), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.LiveTaskLimitExceeded, 0);
        Assert.Equal(65, state.Event.Tasks.NextSequence);
        Assert.Equal(64, state.Banner!.TaskId.Sequence);
        var accepted = BannerSceneExecutor.AdvanceTick(state, false, [new SceneTaskUpdate.Complete(new(Scene, 1), 0), start], 1, 3);
        Assert.True(accepted.Accepted);
        Assert.Equal(63, accepted.State.Event.Tasks.LiveTasks);
        Assert.Equal(66, accepted.State.Event.Tasks.NextSequence);
        Assert.Equal(65, accepted.State.Event.Tasks.Tasks[^1].Id.Sequence);
        Assert.Equal(SceneTaskPhase.Completed, accepted.State.Event.Tasks.Tasks[63].Phase);
    }

    [Fact]
    public void Tombstone_capacity_prevents_attachment_without_eviction_or_effect_allocation()
    {
        var ledger = SceneTaskState.Create(Scene);
        for (int b = 0; b < 4; b++)
        {
            ledger = SceneTaskLedger.ApplyUpdates(ledger, Enumerable.Repeat<SceneTaskUpdate>(new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Owner), 64).ToImmutableArray()).State;
            ledger = SceneTaskLedger.ApplyUpdates(ledger, ledger.Tasks.Where(t => !t.IsTerminal).Select(t => (SceneTaskUpdate)new SceneTaskUpdate.Complete(t.Id, 0)).ToImmutableArray()).State;
        }
        var state = Empty(ledger);
        Rejected(state, BannerSceneExecutor.AttachBanner(state, Definition, Owner), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.TotalTaskLimitExceeded, 0);
        Assert.Equal(256, state.Event.Tasks.TotalStarted);
        Assert.Equal(257, state.Event.Tasks.NextSequence);
        Assert.Null(state.Banner);
    }

    [Theory]
    [InlineData("phase", false)]
    [InlineData("complete", false)]
    [InlineData("cancel", false)]
    [InlineData("phase", true)]
    [InlineData("complete", true)]
    [InlineData("cancel", true)]
    public void No_supplied_fact_can_address_managed_identity_including_its_terminal_tombstone(string kind, bool terminal)
    {
        var state = terminal ? Through(31) : Waiting();
        SceneTaskUpdate update = kind switch
        {
            "phase" => new SceneTaskUpdate.Phase(new(Scene, 1), 0, SceneTaskPhase.Running),
            "cancel" => new SceneTaskUpdate.Cancel(new(Scene, 1), 0, SceneTaskCancellationReason.OwnerAcknowledgedCancellation),
            _ => new SceneTaskUpdate.Complete(new(Scene, 1), 0),
        };
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, false, [update], 1), BannerSceneRejection.ManagedTaskUpdateForbidden, index: 0);
    }

    [Fact]
    public void Bad_unmanaged_fact_rolls_back_skip_and_allocated_identity_before_retry()
    {
        var state = Through(5, 60);
        var start = new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Owner);
        string frozen = JsonSerializer.Serialize(state);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, true, [start, new SceneTaskUpdate.Complete(new(Scene, 99), 0)], 1),
            BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UnknownTask, 1);
        Assert.Equal(frozen, JsonSerializer.Serialize(state));
        var retry = BannerSceneExecutor.AdvanceTick(state, false, [start], 0);
        Assert.True(retry.Accepted);
        Assert.Equal(GeneralBannerPhase.Revealing, retry.State.Banner!.Phase);
        Assert.Equal(6, retry.State.Banner.RevealCompleted);
        Assert.Equal(new(Scene, 2), retry.State.Event.Tasks.Tasks[^1].Id);
    }

    [Theory]
    [InlineData("default", SceneTaskRejection.InvalidUpdateBatch)]
    [InlineData("null", SceneTaskRejection.InvalidUpdate)]
    [InlineData("family", SceneTaskRejection.UnsupportedTaskFamily)]
    [InlineData("size", SceneTaskRejection.UpdateBudgetExceeded)]
    public void Malformed_unmanaged_batches_cannot_advance_effect(string problem, SceneTaskRejection reason)
    {
        var state = Waiting();
        ImmutableArray<SceneTaskUpdate> updates = problem switch
        {
            "default" => default,
            "null" => [null!],
            "family" => [new SceneTaskUpdate.Start((SceneTaskFamily)999, Owner)],
            _ => Enumerable.Repeat<SceneTaskUpdate>(new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Owner), 65).ToImmutableArray(),
        };
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, true, updates, 1), BannerSceneRejection.TaskLedgerRejected, reason,
            problem is "null" or "family" ? 0 : null);
    }

    [Fact]
    public void Owner_cancellation_preempts_natural_completion_on_exit_yield_and_terminal_state_is_frozen()
    {
        var state = Through(30);
        var cancelled = BannerSceneExecutor.AdvanceTick(state, true, [], 1, 1, new(new(Scene, 1), 0));
        Assert.True(cancelled.Accepted);
        Assert.Equal(GeneralBannerPhase.Cancelled, cancelled.State.Banner!.Phase);
        Assert.Equal(GeneralBannerExitCause.OwnerAcknowledgedCancellation, cancelled.State.Banner.ExitCause);
        Assert.Equal(10, cancelled.State.Banner.HideCompleted);
        Assert.Equal(SceneTaskPhase.Cancelled, cancelled.State.Event.Tasks.Tasks[0].Phase);
        var later = BannerSceneExecutor.AdvanceTick(cancelled.State, true, [], 0, 0);
        Assert.True(later.Accepted);
        Assert.Same(cancelled.State.Banner, later.State.Banner);
        Assert.Equal(31, later.State.Banner!.LastProcessedTick);
        Assert.Equal(32, later.State.Event.Tick);
        Assert.Equal(0, later.AcceptedEffectTicks);
        Assert.Equal(2, later.Trace.Length);
    }

    [Fact]
    public void Cancellation_cannot_take_control_of_another_existing_task()
    {
        var tasks = SceneTaskLedger.ApplyUpdates(SceneTaskState.Create(Scene), [new SceneTaskUpdate.Start(SceneTaskFamily.Camera, Owner)]).State;
        var state = Waiting(tasks: tasks);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, false, [], 1, cancellation: new(new(Scene, 1), 0)), BannerSceneRejection.InvalidManagedTaskBinding);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, false, [], 1, cancellation: new(new(Scene, 99), 0)), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.UnknownTask);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, false, [], 1, cancellation: new(new(default, 1), 0)), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.InvalidId);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, false, [], 1, cancellation: new(new(Scene, 0), 0)), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdate);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, false, [], 1, cancellation: new(new(Scene, 2), -1)), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.InvalidUpdate);
    }

    [Fact]
    public void Checked_clock_overflow_preserves_newly_attached_effect_and_pending_VM()
    {
        var ledger = Ledger(SceneTaskState.Create(Scene), tick: long.MaxValue);
        var state = Waiting(tasks: ledger);
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, true, [], 1), BannerSceneRejection.TaskLedgerRejected, SceneTaskRejection.CounterOverflow);
        Assert.Equal(long.MaxValue, state.Banner!.LastProcessedTick);
        Assert.Equal(0, state.Banner.RevealCompleted);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("family")]
    [InlineData("phase")]
    [InlineData("revision")]
    [InlineData("missing")]
    [InlineData("scene")]
    [InlineData("clock")]
    [InlineData("terminal")]
    public void Inconsistent_managed_binding_rejects_before_any_candidate_progress(string fault)
    {
        // Test-only internal snapshot fault injection; no public restoration surface is added.
        var valid = Waiting();
        var task = valid.Event.Tasks.Tasks[0];
        var corrupt = task with
        {
            OwnerId = fault == "owner" ? new("sample:owner/foreign") : task.OwnerId,
            Family = fault == "family" ? SceneTaskFamily.Camera : task.Family,
            Phase = fault == "phase" ? SceneTaskPhase.Holding : fault == "terminal" ? SceneTaskPhase.Completed : task.Phase,
            Revision = fault is "revision" or "terminal" ? 1 : 0,
        };
        var ledger = Ledger(valid.Event.Tasks, fault == "clock" ? 1 : 0,
            fault == "missing" ? [] : [corrupt], fault == "scene" ? new("sample:scene/other") : Scene);
        var e = valid.Event;
        var inner = typeof(SceneEventState).GetProperty("Inner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(e);
        var changedEvent = (SceneEventState)Activator.CreateInstance(typeof(SceneEventState), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [e.Program, inner, ledger, e.ConsumerId, e.Suspension, e.RemainingWaitTicks, e.WaitingAtOffset], null)!;
        var state = (BannerSceneState)Activator.CreateInstance(typeof(BannerSceneState), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [changedEvent, valid.Banner], null)!;
        Rejected(state, BannerSceneExecutor.AdvanceTick(state, true, [], 1), BannerSceneRejection.InvalidManagedTaskBinding);
    }

    private static SceneTaskState Ledger(SceneTaskState input, long tick, ImmutableArray<SceneTask>? tasks = null, AssetId? scene = null)
        => (SceneTaskState)Activator.CreateInstance(typeof(SceneTaskState), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [scene ?? input.SceneRunId, tick, input.NextSequence, tasks ?? input.Tasks, input.Barrier], null)!;

    private static void Rejected(BannerSceneState input, BannerSceneResult result, BannerSceneRejection reason,
        SceneTaskRejection task = SceneTaskRejection.None, int? index = null)
    {
        Assert.False(result.Accepted);
        Assert.Same(input, result.State);
        Assert.Equal(reason, result.Rejection);
        Assert.Equal(task, result.TaskRejection);
        Assert.Equal(index, result.FailureUpdateIndex);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Trace);
        Assert.Equal(0, result.AcceptedEffectTicks);
        Assert.Equal(0, result.AcceptedTaskUpdates);
        Assert.Equal(0, result.InstructionsExecuted);
        Assert.Equal(0, result.TicksConsumed);
    }
}
