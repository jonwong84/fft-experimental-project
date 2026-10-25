using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Atomic event, dispatcher, child transaction for the selected preloaded Ready display.</summary>
public static class ReadySceneExecutor
{
    public static ReadySceneResult RunUntilBlocked(ReadySceneState state, int instructionBudget,
        int updateBudget = SceneTaskState.MaxUpdatesPerCommand)
    {
        ArgumentNullException.ThrowIfNull(state);
        var invalid = Validate(state, instructionBudget, updateBudget);
        if (invalid is not null) return invalid;
        if (state.Event.Exited) return Reject(state, BannerSceneRejection.AlreadyExited, "Interpreter has exited.");
        if (state.Event.Suspension != SceneEventSuspension.Ready)
            return Reject(state, BannerSceneRejection.AwaitingTick, "Suspended interpreter requires an explicit tick.");
        return Pump(state, instructionBudget, updateBudget);
    }

    public static ReadySceneResult AdvanceTick(ReadySceneState state, bool? circlePressed,
        ImmutableArray<SceneTaskUpdate> unmanagedUpdates, int instructionBudget,
        int updateBudget = SceneTaskState.MaxUpdatesPerCommand, ReadyCancellation? cancellation = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var invalid = Validate(state, instructionBudget, updateBudget);
        if (invalid is not null) return invalid;
        if (circlePressed is null) return Reject(state, BannerSceneRejection.MissingCircleSample, "An explicit logical Circle sample is required.");
        if (unmanagedUpdates.IsDefault) return TaskFailure(state, SceneTaskRejection.InvalidUpdateBatch, "An initialized update batch is required.");
        if (unmanagedUpdates.Length > updateBudget) return TaskFailure(state, SceneTaskRejection.UpdateBudgetExceeded, "Supplied updates exceed the budget.");
        for (int i = 0; i < unmanagedUpdates.Length; i++)
        {
            var reason = SceneTaskLedger.ValidateUpdate(unmanagedUpdates[i]);
            if (reason != SceneTaskRejection.None) return TaskFailure(state, reason, "Invalid task update payload.", i);
        }
        if (!state.Event.Exited && state.Event.Suspension == SceneEventSuspension.Ready)
            return Reject(state, BannerSceneRejection.NotWaiting, "Run the Ready interpreter before advancing the scene.");
        if (cancellation is not null)
        {
            var bad = ValidateCancellation(state, cancellation);
            if (bad is not null) return bad;
        }

        // The event sees only preceding-tick facts. Supplied facts and managed completion publish later.
        var resumed = SceneEventExecutor.ResumeTick(state.Event, [], 0);
        if (!resumed.Accepted) return EventFailure(state, resumed);
        var trace = ImmutableArray.CreateBuilder<ReadySceneTrace>();
        trace.AddRange(Wrap(resumed.Trace, instructionBudget, updateBudget));
        var current = state.With(resumed.State, state.Ready);
        int instructions = 0, updates = 0;
        if (!current.Event.Exited && current.Event.Suspension == SceneEventSuspension.Ready)
        {
            var pumped = Pump(current, instructionBudget, updateBudget);
            if (!pumped.Accepted) return Rebase(state, pumped);
            current = pumped.State;
            trace.AddRange(pumped.Trace);
            instructions = pumped.InstructionsExecuted;
            updates = pumped.AcceptedTaskUpdates;
        }
        // Includes a newly allocated identity predicted by an external caller, and retained tombstones.
        for (int i = 0; i < unmanagedUpdates.Length; i++)
            if (current.Ready is { } managed && BannerSceneExecutor.ReferencedId(unmanagedUpdates[i]) == managed.TaskId)
                return ReadySceneResult.Reject(state, BannerSceneRejection.ManagedTaskUpdateForbidden,
                    "Only the driver can update its retained dispatcher task.", index: i);
        var applied = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks, unmanagedUpdates, updateBudget - updates);
        if (!applied.Accepted) return LedgerFailure(state, applied);
        current = current.With(BannerSceneExecutor.WithTasks(current.Event, applied.State), current.Ready);
        updates += applied.AcceptedUpdates;
        trace.AddRange(TaskTrace(applied.Trace, instructionBudget, updateBudget));

        int effectTicks = 0;
        if (state.Ready is { } before)
        {
            ReadyDisplayState after;
            long tick = current.Event.Tick;
            if (before.IsTerminal)
            {
                after = before;
                trace.Add(new(ReadySceneTraceKind.Observed, tick, before, after));
            }
            else if (cancellation is not null)
            {
                after = before.Cancel(tick);
                trace.Add(new(ReadySceneTraceKind.AggregateCancelled, tick, before, after, Cancellation: cancellation));
                effectTicks = 1;
            }
            else
            {
                var dispatcher = before.ResumeDispatcher(tick, circlePressed.Value);
                var kind = dispatcher.IsTerminal ? ReadySceneTraceKind.DispatcherCleared
                    : dispatcher.ReleaseTick == tick ? ReadySceneTraceKind.DispatcherRelease
                    : dispatcher.DispatcherPhase == ReadyDispatcherPhase.WaitingInput ? ReadySceneTraceKind.DispatcherWaitingInput
                    : ReadySceneTraceKind.DispatcherChildPollPending;
                trace.Add(new(kind, tick, before, dispatcher,
                    CirclePressed: before.DispatcherPhase == ReadyDispatcherPhase.WaitingInput ? circlePressed : null));
                after = dispatcher.ResumeChild(tick);
                if (!before.ChildIsTerminal)
                    trace.Add(new(after.ChildIsTerminal ? ReadySceneTraceKind.ChildCleared
                        : after.ChildPhase == ReadyChildPhase.FadeOut ? ReadySceneTraceKind.ChildFadeOut
                        : ReadySceneTraceKind.ChildAdvanced, tick, dispatcher, after));
                effectTicks = 1;
            }
            if (!before.IsTerminal && after.IsTerminal)
            {
                SceneTaskUpdate terminal = after.DispatcherPhase == ReadyDispatcherPhase.Cancelled
                    ? new SceneTaskUpdate.Cancel(before.TaskId, 0, SceneTaskCancellationReason.OwnerAcknowledgedCancellation)
                    : new SceneTaskUpdate.Complete(before.TaskId, 0);
                var completed = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks, [terminal], updateBudget - updates);
                if (!completed.Accepted) return LedgerFailure(state, completed);
                current = current.With(BannerSceneExecutor.WithTasks(current.Event, completed.State), after);
                updates += completed.AcceptedUpdates;
                trace.AddRange(TaskTrace(completed.Trace, instructionBudget, updateBudget));
            }
            else current = current.With(current.Event, after);
        }
        // A newborn already ran to its first yield in Pump: it never consumes this tick's Circle sample.
        return ReadySceneResult.Accept(current, trace.ToImmutable(), instructions, updates, effectTicks, 1);
    }

    public static ReadySceneResult StartNextEvent(ReadySceneState state, ReadySceneProgram? program,
        ConditionalEventVariables variables, AssetId consumerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Event.Exited) return Reject(state, BannerSceneRejection.EventNotExited, "Only an exited interpreter can be replaced.");
        if (program is null) return Reject(state, BannerSceneRejection.InvalidProgram, "A validated Ready program is required.");
        if (!variables.InDomain) return Reject(state, BannerSceneRejection.InvalidVariables, "Proof variables must be in 0..65535.");
        if (!SceneTaskState.ValidId(consumerId) || consumerId == state.Event.ConsumerId)
            return Reject(state, BannerSceneRejection.InvalidId, "A new explicit consumer ID is required.");
        var invalid = Validate(state, 0, 0);
        if (invalid is not null) return invalid;
        var next = new ReadySceneState(program, program.Body.Start(variables, state.Event.Tasks, consumerId), state.Ready, state.Binding);
        invalid = Validate(next, 0, 0);
        if (invalid is not null) return Rebase(state, invalid);
        return ReadySceneResult.Accept(next,
            [new(ReadySceneTraceKind.EventStarted, next.Event.Tick, Before: state.Ready, After: state.Ready, StartedEvent: next.Event)]);
    }

    private static ReadySceneResult Pump(ReadySceneState state, int instructionBudget, int updateBudget)
    {
        var current = state;
        var trace = ImmutableArray.CreateBuilder<ReadySceneTrace>();
        int updates = 0;
        for (int i = 0; i < instructionBudget; i++)
        {
            if (current.Event.Offset == current.Program.DisplayOffset)
            {
                if (current.Ready is not null) return Reject(state, BannerSceneRejection.EffectAlreadyAttached, "A retained Ready aggregate prevents another admission.");
                var display = current.Program.Display!;
                var started = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks,
                    [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, display.OwnerId)], updateBudget - updates);
                if (!started.Accepted) return LedgerFailure(state, started);
                var ready = ReadyDisplayState.Initialize(display.Definition, started.State.Tasks[^1].Id, display.OwnerId, current.Event.Tick);
                current = current.With(BannerSceneExecutor.WithTasks(current.Event, started.State), ready);
                updates++;
                trace.Add(new(ReadySceneTraceKind.EventAdmission, current.Event.Tick, After: ready, SourceOffset: current.Event.Offset));
                trace.AddRange(TaskTrace(started.Trace, instructionBudget, updateBudget));
                trace.Add(new(ReadySceneTraceKind.DispatcherInitialized, current.Event.Tick, After: ready));
                trace.Add(new(ReadySceneTraceKind.ChildInitialized, current.Event.Tick, After: ready));
            }
            var step = SceneEventExecutor.StepInstruction(current.Event);
            if (!step.Accepted) return EventFailure(state, step);
            current = current.With(step.State, current.Ready);
            trace.AddRange(Wrap(step.Trace, instructionBudget, updateBudget));
            if (current.Event.Exited || current.Event.Suspension != SceneEventSuspension.Ready)
                return ReadySceneResult.Accept(current, trace.ToImmutable(), i + 1, updates);
        }
        return ReadySceneResult.Reject(state, BannerSceneRejection.InstructionBudgetExceeded,
            "Instruction budget exhausted before suspension or exit.", offset: current.Event.Offset);
    }

    private static ReadySceneResult? Validate(ReadySceneState state, int instructions, int updates)
    {
        if (instructions is < 0 or > ConditionalEventProgram.MaxInstructions)
            return Reject(state, BannerSceneRejection.InvalidInstructionBudget, "Instruction budget must be in 0..8192.");
        if (updates is < 0 or > SceneTaskState.MaxUpdatesPerCommand)
            return TaskFailure(state, SceneTaskRejection.InvalidUpdateBudget, "Update budget must be in 0..64.");
        if (state.Program.Display is { } display && (state.Binding is not { } binding ||
            binding.DefinitionId != display.Definition.Id || binding.ArtworkId != display.ArtworkId || binding.OwnerId != display.OwnerId))
            return Reject(state, BannerSceneRejection.InvalidManagedTaskBinding, "A matching preloaded definition/artwork/owner binding is required.");
        if (state.Ready is not { } ready) return null;
        if (state.Binding is not { } retained || retained.DefinitionId != ready.Definition.Id || retained.OwnerId != ready.OwnerId ||
            ready.TaskId.SceneRunId != state.Event.Tasks.SceneRunId || ready.StartTick < 0 || ready.StartTick > ready.LastProcessedTick ||
            ready.LastProcessedTick > state.Event.Tick || (!ready.IsTerminal && ready.LastProcessedTick != state.Event.Tick))
            return Reject(state, BannerSceneRejection.InvalidManagedTaskBinding, "Ready binding and clock must agree.");
        var task = state.Event.Tasks.Tasks.FirstOrDefault(t => t.Id == ready.TaskId);
        bool valid = task is not null && task.OwnerId == ready.OwnerId && task.Family == SceneTaskFamily.ConditionDisplay &&
            (ready.DispatcherPhase switch
            {
                ReadyDispatcherPhase.Completed => task.Phase == SceneTaskPhase.Completed && task.Revision == 1 && task.CancellationReason is null,
                ReadyDispatcherPhase.Cancelled => task.Phase == SceneTaskPhase.Cancelled && task.Revision == 1 &&
                    task.CancellationReason == SceneTaskCancellationReason.OwnerAcknowledgedCancellation,
                _ => task.Phase == SceneTaskPhase.Running && task.Revision == 0 && task.CancellationReason is null,
            });
        return valid ? null : Reject(state, BannerSceneRejection.InvalidManagedTaskBinding, "Dispatcher and retained task must agree.");
    }

    private static ReadySceneResult? ValidateCancellation(ReadySceneState state, ReadyCancellation cancellation)
    {
        if (state.Ready is null) return Reject(state, BannerSceneRejection.EffectNotAttached, "No Ready aggregate exists to cancel.");
        if (!SceneTaskState.ValidId(cancellation.TaskId.SceneRunId)) return TaskFailure(state, SceneTaskRejection.InvalidId, "Cancellation requires a scene ID.");
        if (cancellation.TaskId.Sequence < 1 || cancellation.ExpectedRevision < 0)
            return TaskFailure(state, SceneTaskRejection.InvalidUpdate, "Cancellation identity/revision is malformed.");
        if (cancellation.TaskId.SceneRunId != state.Event.Tasks.SceneRunId)
            return TaskFailure(state, SceneTaskRejection.WrongScene, "Cancellation belongs to another scene.");
        var task = state.Event.Tasks.Tasks.FirstOrDefault(t => t.Id == cancellation.TaskId);
        if (task is null) return TaskFailure(state, SceneTaskRejection.UnknownTask, "Cancellation task does not exist.");
        if (task.Id != state.Ready.TaskId) return Reject(state, BannerSceneRejection.InvalidManagedTaskBinding, "Cancellation must address the managed dispatcher.");
        if (task.IsTerminal) return TaskFailure(state, SceneTaskRejection.DuplicateTerminalUpdate, "Dispatcher is already terminal.");
        if (task.Revision != cancellation.ExpectedRevision)
            return TaskFailure(state, SceneTaskRejection.StaleTaskRevision, "Cancellation revision differs from the dispatcher.");
        return null;
    }

    private static IEnumerable<ReadySceneTrace> Wrap(IEnumerable<SceneEventTrace> facts, int instructions, int updates)
        => facts.Select(f => new ReadySceneTrace(ReadySceneTraceKind.Event, f.Tick, EventTrace: f, InstructionBudget: instructions, UpdateBudget: updates));
    private static IEnumerable<ReadySceneTrace> TaskTrace(IEnumerable<SceneTaskTrace> facts, int instructions, int updates)
        => facts.Select(f => new ReadySceneTrace(ReadySceneTraceKind.TaskLedger, f.Tick, TaskTrace: f, InstructionBudget: instructions, UpdateBudget: updates));
    private static ReadySceneResult Rebase(ReadySceneState original, ReadySceneResult failed)
        => ReadySceneResult.Reject(original, failed.Rejection, failed.Diagnostic!, failed.TaskRejection, failed.FailureUpdateIndex, failed.FailureOffset);
    private static ReadySceneResult EventFailure(ReadySceneState state, SceneEventResult failed)
        => ReadySceneResult.Reject(state, failed.Rejection == SceneEventRejection.ArithmeticDomainExceeded
            ? BannerSceneRejection.ArithmeticDomainExceeded : BannerSceneRejection.TaskLedgerRejected,
            failed.Diagnostic!, failed.TaskRejection, failed.FailureUpdateIndex, failed.FailureOffset);
    private static ReadySceneResult LedgerFailure(ReadySceneState state, SceneTaskResult failed)
        => TaskFailure(state, failed.Rejection, failed.Diagnostic!, failed.FailureUpdateIndex);
    private static ReadySceneResult TaskFailure(ReadySceneState state, SceneTaskRejection reason, string diagnostic, int? index = null)
        => ReadySceneResult.Reject(state, BannerSceneRejection.TaskLedgerRejected, diagnostic, reason, index);
    private static ReadySceneResult Reject(ReadySceneState state, BannerSceneRejection reason, string diagnostic)
        => ReadySceneResult.Reject(state, reason, diagnostic);
}
