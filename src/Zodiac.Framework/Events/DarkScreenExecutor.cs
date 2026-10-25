using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Atomic event-before-worker proof. Only this driver can publish its retained dark-screen task facts.</summary>
public static class DarkScreenExecutor
{
    public static DarkScreenResult RunUntilBlocked(DarkScreenSceneState state, int instructionBudget,
        int updateBudget = SceneTaskState.MaxUpdatesPerCommand)
    {
        ArgumentNullException.ThrowIfNull(state);
        var invalid = Validate(state, instructionBudget, updateBudget);
        if (invalid is not null) return invalid;
        if (state.Event.Exited) return EventFailure(state, SceneEventRejection.AlreadyExited, "Interpreter has exited.");
        if (state.Event.Suspension != SceneEventSuspension.Ready)
            return EventFailure(state, SceneEventRejection.AwaitingTick, "Suspended interpreter requires an explicit tick.");
        return Pump(state, instructionBudget, updateBudget);
    }

    public static DarkScreenResult AdvanceTick(DarkScreenSceneState state, ImmutableArray<SceneTaskUpdate> unmanagedUpdates,
        int instructionBudget, int updateBudget = SceneTaskState.MaxUpdatesPerCommand)
    {
        ArgumentNullException.ThrowIfNull(state);
        var invalid = Validate(state, instructionBudget, updateBudget);
        if (invalid is not null) return invalid;
        if (unmanagedUpdates.IsDefault) return TaskFailure(state, SceneTaskRejection.InvalidUpdateBatch, "An initialized batch is required.");
        if (unmanagedUpdates.Length > updateBudget) return TaskFailure(state, SceneTaskRejection.UpdateBudgetExceeded, "Supplied updates exceed the budget.");
        for (int i = 0; i < unmanagedUpdates.Length; i++)
        {
            var reason = SceneTaskLedger.ValidateUpdate(unmanagedUpdates[i]);
            if (reason != SceneTaskRejection.None) return TaskFailure(state, reason, "Invalid task update.", i);
            if (unmanagedUpdates[i] is SceneTaskUpdate.Start { Family: SceneTaskFamily.DarkScreen } ||
                state.Worker is { } worker && BannerSceneExecutor.ReferencedId(unmanagedUpdates[i]) == worker.TaskId)
                return Forbidden(state, i);
        }
        if (!state.Event.Exited && state.Event.Suspension == SceneEventSuspension.Ready)
            return EventFailure(state, SceneEventRejection.NotWaiting, "Run the Ready interpreter before advancing.");

        // Parent polls preceding published facts. Candidate event signals and worker facts are never visible to this poll.
        var resumed = SceneEventExecutor.ResumeTick(state.Event, [], 0);
        if (!resumed.Accepted) return EventFailure(state, resumed);
        var current = state.With(resumed.State, state.Worker);
        var trace = ImmutableArray.CreateBuilder<DarkScreenTrace>();
        trace.AddRange(Wrap(resumed.Trace, instructionBudget, updateBudget));
        int instructions = 0, updates = 0;
        if (!current.Event.Exited && current.Event.Suspension == SceneEventSuspension.Ready)
        {
            var pumped = Pump(current, instructionBudget, updateBudget);
            if (!pumped.Accepted) return Rebase(state, pumped);
            current = pumped.State;
            trace.AddRange(pumped.Trace);
            (instructions, updates) = (pumped.InstructionsExecuted, pumped.AcceptedTaskUpdates);
        }
        // A supplied fact cannot predict and take over an identity born in this transaction.
        for (int i = 0; i < unmanagedUpdates.Length; i++)
            if (current.Worker is { } managed && BannerSceneExecutor.ReferencedId(unmanagedUpdates[i]) == managed.TaskId)
                return Forbidden(state, i);
        var applied = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks, unmanagedUpdates, updateBudget - updates);
        if (!applied.Accepted) return LedgerFailure(state, applied);
        current = current.With(BannerSceneExecutor.WithTasks(current.Event, applied.State), current.Worker);
        trace.AddRange(Wrap(applied.Trace, instructionBudget, updateBudget));
        updates += applied.AcceptedUpdates;

        int workerTicks = 0;
        // Newly admitted workers remain at their first yield and cannot step twice on their birth tick.
        if (state.Worker is not null)
        {
            var before = current.Worker!;
            var after = before.Advance(current.Event.Tick);
            trace.Add(new(before.IsTerminal ? DarkScreenTraceKind.WorkerObserved : DarkScreenTraceKind.WorkerAdvanced,
                current.Event.Tick, instructionBudget, updateBudget, Before: before, After: after));
            workerTicks = before.IsTerminal ? 0 : 1;
            if (before.Phase != after.Phase)
            {
                var task = current.Event.Tasks.Tasks.Single(t => t.Id == before.TaskId);
                SceneTaskUpdate change = after.IsTerminal ? new SceneTaskUpdate.Complete(task.Id, task.Revision)
                    : new SceneTaskUpdate.Phase(task.Id, task.Revision, SceneTaskPhase.Holding);
                var published = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks, [change], updateBudget - updates);
                if (!published.Accepted) return LedgerFailure(state, published);
                current = current.With(BannerSceneExecutor.WithTasks(current.Event, published.State), after);
                trace.AddRange(Wrap(published.Trace, instructionBudget, updateBudget));
                updates += published.AcceptedUpdates;
            }
            else current = current.With(current.Event, after);
        }
        return DarkScreenResult.Accept(current, trace.ToImmutable(), instructions, updates, workerTicks, 1);
    }

    private static DarkScreenResult Pump(DarkScreenSceneState state, int instructionBudget, int updateBudget)
    {
        var current = state;
        var trace = ImmutableArray.CreateBuilder<DarkScreenTrace>();
        int updates = 0;
        for (int i = 0; i < instructionBudget; i++)
        {
            var instruction = current.Program.Instructions[current.Program.Body.Body.IndexOf(current.Event.Offset)];
            if (instruction.Start is { } start)
            {
                if (current.Worker is not null)
                    return DarkScreenResult.Reject(state, DarkScreenRejection.WorkerAlreadyAttached, "The retained worker prevents re-admission.", offset: instruction.Offset);
                var admitted = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks,
                    [new SceneTaskUpdate.Start(SceneTaskFamily.DarkScreen, start.OwnerId)], updateBudget - updates);
                if (!admitted.Accepted) return LedgerFailure(state, admitted);
                var worker = DarkScreenState.Start(start.Definition, admitted.State.Tasks[^1].Id, start.OwnerId, current.Event.Tick);
                current = current.With(BannerSceneExecutor.WithTasks(current.Event, admitted.State), worker);
                trace.AddRange(Wrap(admitted.Trace, instructionBudget, updateBudget));
                trace.Add(new(DarkScreenTraceKind.WorkerStarted, current.Event.Tick, instructionBudget, updateBudget,
                    instruction.Offset, After: worker));
                updates++;
            }
            if (instruction.Release)
            {
                var before = current.Worker;
                if (before?.Phase == DarkScreenPhase.Holding)
                {
                    var task = current.Event.Tasks.Tasks.Single(t => t.Id == before.TaskId);
                    var signal = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks,
                        [new SceneTaskUpdate.Phase(task.Id, task.Revision, SceneTaskPhase.Leaving)], updateBudget - updates);
                    if (!signal.Accepted) return LedgerFailure(state, signal);
                    current = current.With(BannerSceneExecutor.WithTasks(current.Event, signal.State), before.SignalRelease());
                    trace.AddRange(Wrap(signal.Trace, instructionBudget, updateBudget));
                    updates++;
                }
                trace.Add(new(current.Worker != before ? DarkScreenTraceKind.ReleaseMatched : DarkScreenTraceKind.ReleaseNoMatch,
                    current.Event.Tick, instructionBudget, updateBudget, instruction.Offset, before, current.Worker));
            }
            var stepped = SceneEventExecutor.StepInstruction(current.Event);
            if (!stepped.Accepted) return EventFailure(state, stepped);
            current = current.With(stepped.State, current.Worker);
            trace.AddRange(Wrap(stepped.Trace, instructionBudget, updateBudget));
            if (current.Event.Exited || current.Event.Suspension != SceneEventSuspension.Ready)
                return DarkScreenResult.Accept(current, trace.ToImmutable(), i + 1, updates);
        }
        return EventFailure(state, SceneEventRejection.InstructionBudgetExceeded,
            "Instruction budget exhausted before suspension or exit.", current.Event.Offset);
    }

    private static DarkScreenResult? Validate(DarkScreenSceneState state, int instructions, int updates)
    {
        if (instructions is < 0 or > ConditionalEventProgram.MaxInstructions)
            return EventFailure(state, SceneEventRejection.InvalidInstructionBudget, "Instruction budget must be in 0..8192.");
        if (updates is < 0 or > SceneTaskState.MaxUpdatesPerCommand)
            return TaskFailure(state, SceneTaskRejection.InvalidUpdateBudget, "Update budget must be in 0..64.");
        if (state.Program.Start is { } start && (state.ReadyBinding is not { } binding ||
            binding.DefinitionId != start.Definition.Id || binding.OwnerId != start.OwnerId))
            return BindingFailure(state);
        var darkTasks = state.Event.Tasks.Tasks.Where(t => t.Family == SceneTaskFamily.DarkScreen);
        if (state.Worker is not { } worker)
            return darkTasks.Any(t => !t.IsTerminal) ? BindingFailure(state) : null;
        if (state.ReadyBinding is not { } ready || ready.DefinitionId != worker.Definition.Id || ready.OwnerId != worker.OwnerId ||
            worker.TaskId.SceneRunId != state.Event.Tasks.SceneRunId || worker.StartTick < 0 || worker.StartTick > worker.LastProcessedTick ||
            worker.LastProcessedTick > state.Event.Tick || (!worker.IsTerminal && worker.LastProcessedTick != state.Event.Tick) ||
            worker.ReleaseYieldPending || darkTasks.Any(t => t.Id != worker.TaskId && !t.IsTerminal))
            return BindingFailure(state);
        var task = darkTasks.SingleOrDefault(t => t.Id == worker.TaskId);
        var expected = worker.Phase switch
        {
            DarkScreenPhase.Entering => (SceneTaskPhase.Entering, 0L),
            DarkScreenPhase.Holding => (SceneTaskPhase.Holding, 1L),
            DarkScreenPhase.Leaving => (SceneTaskPhase.Leaving, 2L),
            _ => (SceneTaskPhase.Completed, 3L),
        };
        if (task is null || task.OwnerId != worker.OwnerId || task.Phase != expected.Item1 ||
            task.Revision != expected.Item2 || task.CancellationReason is not null)
            return BindingFailure(state);
        return null;
    }

    private static IEnumerable<DarkScreenTrace> Wrap(IEnumerable<SceneEventTrace> facts, int instructions, int updates)
        => facts.Select(f => new DarkScreenTrace(DarkScreenTraceKind.Event, f.Tick, instructions, updates, EventTrace: f));
    private static IEnumerable<DarkScreenTrace> Wrap(IEnumerable<SceneTaskTrace> facts, int instructions, int updates)
        => facts.Select(f => new DarkScreenTrace(DarkScreenTraceKind.TaskLedger, f.Tick, instructions, updates, TaskTrace: f));
    private static DarkScreenResult Rebase(DarkScreenSceneState state, DarkScreenResult failure)
        => DarkScreenResult.Reject(state, failure.Rejection, failure.Diagnostic!, failure.EventRejection,
            failure.TaskRejection, failure.FailureOffset, failure.FailureUpdateIndex);
    private static DarkScreenResult EventFailure(DarkScreenSceneState state, SceneEventResult failure)
        => DarkScreenResult.Reject(state, DarkScreenRejection.EventRejected, failure.Diagnostic!, failure.Rejection,
            failure.TaskRejection, failure.FailureOffset, failure.FailureUpdateIndex);
    private static DarkScreenResult EventFailure(DarkScreenSceneState state, SceneEventRejection reason, string diagnostic, int? offset = null)
        => DarkScreenResult.Reject(state, DarkScreenRejection.EventRejected, diagnostic, reason, offset: offset ?? state.Event.Offset);
    private static DarkScreenResult LedgerFailure(DarkScreenSceneState state, SceneTaskResult failure)
        => TaskFailure(state, failure.Rejection, failure.Diagnostic!, failure.FailureUpdateIndex);
    private static DarkScreenResult TaskFailure(DarkScreenSceneState state, SceneTaskRejection reason, string diagnostic, int? index = null)
        => DarkScreenResult.Reject(state, DarkScreenRejection.TaskLedgerRejected, diagnostic, taskReason: reason, index: index);
    private static DarkScreenResult BindingFailure(DarkScreenSceneState state)
        => DarkScreenResult.Reject(state, DarkScreenRejection.InvalidManagedBinding, "Definition, readiness, owner, worker, ledger and clock must agree; no other live dark-screen task is supported.");
    private static DarkScreenResult Forbidden(DarkScreenSceneState state, int index)
        => DarkScreenResult.Reject(state, DarkScreenRejection.ManagedTaskUpdateForbidden,
            "Only the driver may start or mutate its dark-screen task.", index: index);
}
