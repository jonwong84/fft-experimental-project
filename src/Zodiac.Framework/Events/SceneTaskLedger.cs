using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Pure scene task/barrier proof. A rejected command preserves the exact input and publishes no trace.</summary>
public static class SceneTaskLedger
{
    public static SceneTaskResult ApplyUpdates(SceneTaskState state, ImmutableArray<SceneTaskUpdate> updates,
        int updateBudget = SceneTaskState.MaxUpdatesPerCommand)
        => Update(state, updates, updateBudget, advanceTick: false);

    /// <summary>Enter Pending without polling, even for an empty scene or terminal tasks.</summary>
    public static SceneTaskResult BeginBarrier(SceneTaskState state, SceneTaskClass selector,
        AssetId consumerId, int sourceOffset, AssetId continuationId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(selector))
            return SceneTaskResult.Reject(state, SceneTaskRejection.UnsupportedTaskClass, "Unsupported BATTLE proof task class.");
        if (!SceneTaskState.ValidId(consumerId) || !SceneTaskState.ValidId(continuationId))
            return SceneTaskResult.Reject(state, SceneTaskRejection.InvalidId, "Consumer and continuation IDs must be nondefault and at most 256 characters.");
        if (sourceOffset < 0)
            return SceneTaskResult.Reject(state, SceneTaskRejection.InvalidSourceOffset, "Source offset must be nonnegative.");
        if (state.BarrierStatus == SceneBarrierStatus.Pending)
            return SceneTaskResult.Reject(state, SceneTaskRejection.BarrierAlreadyPending, "Only one barrier consumer is supported.");
        var barrier = new SceneBarrier(SceneBarrierStatus.Pending, selector, consumerId, sourceOffset, continuationId, state.Tick);
        return SceneTaskResult.Accept(new(state.SceneRunId, state.Tick, state.NextSequence, state.Tasks, barrier),
            [new(SceneTaskTraceKind.BarrierEntered, state.Tick, null, null, barrier, [])], 0, 0);
    }

    /// <summary>Advance exactly one logical tick, apply the entire ordered batch, then poll the current scene-wide class set.</summary>
    public static SceneTaskResult AdvanceTick(SceneTaskState state, ImmutableArray<SceneTaskUpdate> updates,
        int updateBudget = SceneTaskState.MaxUpdatesPerCommand)
        => Update(state, updates, updateBudget, advanceTick: true);

    private static SceneTaskResult Update(SceneTaskState state, ImmutableArray<SceneTaskUpdate> updates,
        int budget, bool advanceTick)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (updates.IsDefault)
            return SceneTaskResult.Reject(state, SceneTaskRejection.InvalidUpdateBatch, "An initialized immutable update batch is required.");
        if (budget is < 0 or > SceneTaskState.MaxUpdatesPerCommand)
            return SceneTaskResult.Reject(state, SceneTaskRejection.InvalidUpdateBudget, "Update budget must be in 0..64.");
        if (updates.Length > budget)
            return SceneTaskResult.Reject(state, SceneTaskRejection.UpdateBudgetExceeded, "Update batch exceeds the command budget.");
        // Preflight the entire envelope before looking up any instance or applying candidate changes.
        for (int index = 0; index < updates.Length; index++)
        {
            var invalid = ValidateUpdate(updates[index]);
            if (invalid != SceneTaskRejection.None)
                return SceneTaskResult.Reject(state, invalid, "Invalid task update payload.", index);
        }
        if (!advanceTick && state.BarrierStatus == SceneBarrierStatus.Pending)
            return SceneTaskResult.Reject(state, SceneTaskRejection.AwaitingTick, "Pending task facts must be supplied through AdvanceTick.");

        var tasks = state.Tasks.ToBuilder();
        var trace = ImmutableArray.CreateBuilder<SceneTaskTrace>();
        long tick = state.Tick;
        long sequence = state.NextSequence;
        int live = state.LiveTasks;
        int? currentIndex = null;
        try
        {
            if (advanceTick)
            {
                tick = checked(tick + 1);
                trace.Add(new(SceneTaskTraceKind.TickAdvanced, tick, null, null, null, []));
            }
            for (int index = 0; index < updates.Length; index++)
            {
                currentIndex = index;
                if (updates[index] is SceneTaskUpdate.Start start)
                {
                    if (tasks.Count >= SceneTaskState.MaxTotalStarted)
                        return SceneTaskResult.Reject(state, SceneTaskRejection.TotalTaskLimitExceeded, "Scene start/tombstone limit reached.", index);
                    if (live >= SceneTaskState.MaxLiveTasks)
                        return SceneTaskResult.Reject(state, SceneTaskRejection.LiveTaskLimitExceeded, "Live task limit reached at this update.", index);
                    var task = new SceneTask(new(state.SceneRunId, sequence), start.OwnerId, start.Family,
                        start.Family == SceneTaskFamily.DarkScreen ? SceneTaskPhase.Entering : SceneTaskPhase.Running, 0);
                    sequence = checked(sequence + 1);
                    tasks.Add(task);
                    live++;
                    trace.Add(new(SceneTaskTraceKind.TaskStarted, tick, null, task, null, []));
                    continue;
                }

                var (id, revision) = Reference(updates[index]);
                if (id.SceneRunId != state.SceneRunId)
                    return SceneTaskResult.Reject(state, SceneTaskRejection.WrongScene, "Task belongs to another scene run.", index);
                int taskIndex = -1;
                for (int i = 0; i < tasks.Count; i++)
                    if (tasks[i].Id == id) { taskIndex = i; break; }
                if (taskIndex < 0)
                    return SceneTaskResult.Reject(state, SceneTaskRejection.UnknownTask, "Task instance does not exist in this scene.", index);
                var before = tasks[taskIndex];
                if (before.IsTerminal)
                    return SceneTaskResult.Reject(state, SceneTaskRejection.DuplicateTerminalUpdate, "Task is already terminal; tombstones cannot be revived.", index);
                if (before.Revision != revision)
                    return SceneTaskResult.Reject(state, SceneTaskRejection.StaleTaskRevision, "Expected task revision differs from the current revision.", index);

                SceneTaskPhase target;
                SceneTaskTraceKind kind;
                SceneTaskCancellationReason? cancellation = null;
                switch (updates[index])
                {
                    case SceneTaskUpdate.Phase phase when before.Family == SceneTaskFamily.DarkScreen &&
                        (before.Phase, phase.Target) is (SceneTaskPhase.Entering, SceneTaskPhase.Holding) or (SceneTaskPhase.Holding, SceneTaskPhase.Leaving):
                        target = phase.Target;
                        kind = SceneTaskTraceKind.TaskPhaseChanged;
                        break;
                    case SceneTaskUpdate.Complete when
                        (before.Family == SceneTaskFamily.DarkScreen && before.Phase == SceneTaskPhase.Leaving) ||
                        (before.Family is SceneTaskFamily.Camera or SceneTaskFamily.ConditionDisplay && before.Phase == SceneTaskPhase.Running):
                        target = SceneTaskPhase.Completed;
                        kind = SceneTaskTraceKind.TaskCompleted;
                        break;
                    case SceneTaskUpdate.Cancel cancel:
                        target = SceneTaskPhase.Cancelled;
                        kind = SceneTaskTraceKind.TaskCancelled;
                        cancellation = cancel.Reason;
                        break;
                    default:
                        return SceneTaskResult.Reject(state, SceneTaskRejection.IllegalTaskTransition, "Transition is not allowed for this family/phase.", index);
                }
                var after = before with { Phase = target, Revision = checked(before.Revision + 1), CancellationReason = cancellation };
                tasks[taskIndex] = after;
                if (after.IsTerminal) live--;
                trace.Add(new(kind, tick, before, after, null, []));
            }
        }
        catch (OverflowException)
        {
            return SceneTaskResult.Reject(state, SceneTaskRejection.CounterOverflow, "A logical counter cannot represent the next value.", currentIndex);
        }

        var barrier = state.Barrier;
        if (advanceTick && barrier?.Status == SceneBarrierStatus.Pending)
        {
            var matches = tasks.Where(task => task.ActiveClass == barrier.Selector).Select(task => task.Id)
                .OrderBy(id => id.Sequence).ToImmutableArray();
            trace.Add(new(SceneTaskTraceKind.BarrierPolled, tick, null, null, barrier, matches));
            if (matches.IsEmpty)
            {
                barrier = barrier with { Status = SceneBarrierStatus.Released };
                trace.Add(new(SceneTaskTraceKind.BarrierReleased, tick, null, null, barrier, []));
            }
        }
        return SceneTaskResult.Accept(new(state.SceneRunId, tick, sequence, tasks.ToImmutable(), barrier),
            trace.ToImmutable(), updates.Length, advanceTick ? 1 : 0);
    }

    private static (SceneTaskId Id, long Revision) Reference(SceneTaskUpdate update) => update switch
    {
        SceneTaskUpdate.Phase phase => (phase.TaskId, phase.ExpectedRevision),
        SceneTaskUpdate.Complete complete => (complete.TaskId, complete.ExpectedRevision),
        SceneTaskUpdate.Cancel cancel => (cancel.TaskId, cancel.ExpectedRevision),
        _ => throw new InvalidOperationException("Expected a prevalidated referenced update."),
    };

    private static SceneTaskRejection ValidateUpdate(SceneTaskUpdate? update)
    {
        if (update is null) return SceneTaskRejection.InvalidUpdate;
        if (update is SceneTaskUpdate.Start start)
        {
            if (!Enum.IsDefined(start.Family)) return SceneTaskRejection.UnsupportedTaskFamily;
            return SceneTaskState.ValidId(start.OwnerId) ? SceneTaskRejection.None : SceneTaskRejection.InvalidId;
        }
        if (update is not (SceneTaskUpdate.Phase or SceneTaskUpdate.Complete or SceneTaskUpdate.Cancel))
            return SceneTaskRejection.InvalidUpdate;
        var (id, revision) = Reference(update);
        if (!SceneTaskState.ValidId(id.SceneRunId)) return SceneTaskRejection.InvalidId;
        if (id.Sequence < 1 || revision < 0) return SceneTaskRejection.InvalidUpdate;
        if (update is SceneTaskUpdate.Phase phase && !Enum.IsDefined(phase.Target) ||
            update is SceneTaskUpdate.Cancel cancel && !Enum.IsDefined(cancel.Reason))
            return SceneTaskRejection.InvalidUpdate;
        return SceneTaskRejection.None;
    }
}
