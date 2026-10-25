using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Pure post-readiness banner driver, publishing effect/VM/ledger changes together or none at all.</summary>
public static class BannerSceneExecutor
{
    public static BannerSceneResult AttachBanner(BannerSceneState state, GeneralBannerDefinition? definition, AssetId ownerId,
        int updateBudget = SceneTaskState.MaxUpdatesPerCommand)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ValidUpdateBudget(updateBudget)) return InvalidUpdateBudget(state);
        if (definition is null) return Reject(state, BannerSceneRejection.InvalidDefinition, "An authored banner definition is required.");
        if (!SceneTaskState.ValidId(definition.Id) || !SceneTaskState.ValidId(ownerId))
            return Reject(state, BannerSceneRejection.InvalidId, "Definition and owner IDs must be nondefault and at most 256 characters.");
        if (definition.Condition != GeneralBannerDefinition.SupportedCondition)
            return Reject(state, BannerSceneRejection.UnsupportedCondition, "Only general condition 8 is selected.");
        if (definition.HoldFrames is < 0 or > GeneralBannerDefinition.MaxHoldFrames)
            return Reject(state, BannerSceneRejection.InvalidHoldFrames, "The selected byte hold argument must be in 0..255.");
        if (state.Banner is not null) return Reject(state, BannerSceneRejection.EffectAlreadyAttached, "One retained effect, including terminal, is supported per scene.");
        if (!state.Event.Exited && state.Event.Suspension != SceneEventSuspension.Ready)
            return Reject(state, BannerSceneRejection.AwaitingTick, "Attach only at a Ready or Exited interpreter boundary.");
        var started = SceneTaskLedger.ApplyUpdates(state.Event.Tasks, [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, ownerId)], updateBudget);
        if (!started.Accepted) return TaskFailure(state, started.Rejection, started.Diagnostic!, started.FailureUpdateIndex);
        var banner = new GeneralBannerState(definition, started.State.Tasks[^1].Id, ownerId,
            state.Event.Tick, state.Event.Tick, GeneralBannerPhase.Revealing, 0, 0, 0, null);
        var sceneEvent = WithTasks(state.Event, started.State);
        return BannerSceneResult.Accept(new(sceneEvent, banner),
            [new(BannerSceneTraceKind.TaskLedger, sceneEvent.Tick, TaskTrace: started.Trace[0], UpdateBudget: updateBudget),
             new(BannerSceneTraceKind.EffectAttached, sceneEvent.Tick, After: banner, UpdateBudget: updateBudget)], updates: 1);
    }

    public static BannerSceneResult RunUntilBlocked(BannerSceneState state, int instructionBudget)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ValidInstructionBudget(instructionBudget)) return InvalidInstructionBudget(state);
        if (!ValidBinding(state)) return BindingFailure(state);
        var result = SceneEventExecutor.RunUntilBlocked(state.Event, instructionBudget);
        if (!result.Accepted) return EventFailure(state, result);
        return BannerSceneResult.Accept(new(result.State, state.Banner),
            Wrap(result.Trace, instructionBudget, null), instructions: result.InstructionsExecuted);
    }

    public static BannerSceneResult AdvanceTick(BannerSceneState state, bool? circlePressed,
        ImmutableArray<SceneTaskUpdate> unmanagedUpdates, int instructionBudget,
        int updateBudget = SceneTaskState.MaxUpdatesPerCommand, BannerCancellation? cancellation = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ValidInstructionBudget(instructionBudget)) return InvalidInstructionBudget(state);
        if (!ValidUpdateBudget(updateBudget)) return InvalidUpdateBudget(state);
        if (unmanagedUpdates.IsDefault)
            return TaskFailure(state, SceneTaskRejection.InvalidUpdateBatch, "An initialized immutable update batch is required.");
        if (unmanagedUpdates.Length > updateBudget)
            return TaskFailure(state, SceneTaskRejection.UpdateBudgetExceeded, "Supplied updates exceed the command budget.");
        if (circlePressed is null) return Reject(state, BannerSceneRejection.MissingCircleSample, "An explicit logical Circle sample, including false, is required.");
        if (!state.Event.Exited && state.Event.Suspension == SceneEventSuspension.Ready)
            return Reject(state, BannerSceneRejection.NotWaiting, "Run the Ready interpreter before ticking the scene.");
        if (state.Banner is not { } before) return Reject(state, BannerSceneRejection.EffectNotAttached, "Attach a ready banner before advancing its effect.");
        if (!ValidBinding(state)) return BindingFailure(state);
        for (int i = 0; i < unmanagedUpdates.Length; i++)
        {
            if (unmanagedUpdates[i] is null)
                return TaskFailure(state, SceneTaskRejection.InvalidUpdate, "Task updates must be nonnull.", i);
            if (ReferencedId(unmanagedUpdates[i]) == before.TaskId)
                return BannerSceneResult.Reject(state, BannerSceneRejection.ManagedTaskUpdateForbidden,
                    "Only the effect driver may update this managed identity, including its tombstone.", updateIndex: i);
        }
        if (cancellation is not null)
        {
            var invalid = ValidateCancellation(state, cancellation);
            if (invalid is not null) return invalid;
        }

        long tick;
        try { tick = checked(state.Event.Tick + 1); }
        catch (OverflowException) { return TaskFailure(state, SceneTaskRejection.CounterOverflow, "Scene tick cannot represent another tick."); }
        var after = before.Advance(tick, circlePressed.Value, cancellation is not null);
        var updates = unmanagedUpdates;
        if (!before.IsTerminal && after.IsTerminal)
        {
            SceneTaskUpdate terminal = after.Phase == GeneralBannerPhase.Cancelled
                ? new SceneTaskUpdate.Cancel(before.TaskId, 0, SceneTaskCancellationReason.OwnerAcknowledgedCancellation)
                : new SceneTaskUpdate.Complete(before.TaskId, 0);
            updates = updates.Add(terminal); // Supplied order first; never move completion ahead to free capacity.
        }
        var result = SceneEventExecutor.AdvanceTick(state.Event, updates, instructionBudget, updateBudget);
        if (!result.Accepted) return EventFailure(state, result);
        var trace = ImmutableArray.CreateBuilder<BannerSceneTrace>();
        trace.Add(new(BannerSceneTraceKind.Event, tick, EventTrace: result.Trace[0], InstructionBudget: instructionBudget, UpdateBudget: updateBudget));
        trace.Add(new(before.IsTerminal ? BannerSceneTraceKind.EffectObserved : BannerSceneTraceKind.EffectAdvanced,
            tick, Before: before, After: after, CirclePressed: circlePressed, Cancellation: cancellation,
            InstructionBudget: instructionBudget, UpdateBudget: updateBudget));
        trace.AddRange(Wrap(result.Trace.Skip(1), instructionBudget, updateBudget));
        return BannerSceneResult.Accept(new(result.State, after), trace.ToImmutable(),
            effectTicks: before.IsTerminal ? 0 : 1, updates: result.AcceptedTaskUpdates,
            instructions: result.InstructionsExecuted, ticks: result.TicksConsumed);
    }

    public static BannerSceneResult StartNextEvent(BannerSceneState state, SceneEventProgram? program,
        ConditionalEventVariables variables, AssetId consumerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Event.Exited) return Reject(state, BannerSceneRejection.EventNotExited, "Only an exited interpreter can be replaced.");
        if (program is null) return Reject(state, BannerSceneRejection.InvalidProgram, "Select a validated scene event program.");
        if (!variables.InDomain) return Reject(state, BannerSceneRejection.InvalidVariables, "Proof variables must be in 0..65535.");
        if (!SceneTaskState.ValidId(consumerId) || consumerId == state.Event.ConsumerId)
            return Reject(state, BannerSceneRejection.InvalidId, "Select a new explicit consumer ID of at most 256 characters.");
        if (!ValidBinding(state)) return BindingFailure(state);
        var started = program.Start(variables, state.Event.Tasks, consumerId);
        return BannerSceneResult.Accept(new(started, state.Banner),
            [new(BannerSceneTraceKind.EventStarted, started.Tick, Before: state.Banner, After: state.Banner, StartedEvent: started)]);
    }

    private static BannerSceneResult? ValidateCancellation(BannerSceneState state, BannerCancellation cancellation)
    {
        if (!SceneTaskState.ValidId(cancellation.TaskId.SceneRunId)) return TaskFailure(state, SceneTaskRejection.InvalidId, "Cancellation requires a scene ID.");
        if (cancellation.TaskId.Sequence < 1 || cancellation.ExpectedRevision < 0)
            return TaskFailure(state, SceneTaskRejection.InvalidUpdate, "Cancellation identity/revision is malformed.");
        if (cancellation.TaskId.SceneRunId != state.Event.Tasks.SceneRunId)
            return TaskFailure(state, SceneTaskRejection.WrongScene, "Cancellation belongs to another scene.");
        var task = state.Event.Tasks.Tasks.FirstOrDefault(t => t.Id == cancellation.TaskId);
        if (task is null) return TaskFailure(state, SceneTaskRejection.UnknownTask, "Cancellation task does not exist.");
        if (task.Id != state.Banner!.TaskId) return BindingFailure(state);
        if (task.IsTerminal) return TaskFailure(state, SceneTaskRejection.DuplicateTerminalUpdate, "Managed task is already terminal.");
        if (task.Revision != cancellation.ExpectedRevision)
            return TaskFailure(state, SceneTaskRejection.StaleTaskRevision, "Cancellation revision differs from the managed task.");
        return null;
    }

    private static bool ValidBinding(BannerSceneState state)
    {
        if (state.Banner is not { } banner) return true;
        if (banner.TaskId.SceneRunId != state.Event.Tasks.SceneRunId || banner.StartTick < 0 || banner.StartTick > banner.LastProcessedTick ||
            banner.LastProcessedTick > state.Event.Tick || (!banner.IsTerminal && banner.LastProcessedTick != state.Event.Tick)) return false;
        var task = state.Event.Tasks.Tasks.FirstOrDefault(t => t.Id == banner.TaskId);
        if (task is null || task.OwnerId != banner.OwnerId || task.Family != SceneTaskFamily.ConditionDisplay) return false;
        return banner.Phase switch
        {
            GeneralBannerPhase.Completed => task.Phase == SceneTaskPhase.Completed && task.Revision == 1 && task.CancellationReason is null,
            GeneralBannerPhase.Cancelled => task.Phase == SceneTaskPhase.Cancelled && task.Revision == 1 &&
                task.CancellationReason == SceneTaskCancellationReason.OwnerAcknowledgedCancellation,
            _ => task.Phase == SceneTaskPhase.Running && task.Revision == 0 && task.CancellationReason is null,
        };
    }

    private static SceneTaskId? ReferencedId(SceneTaskUpdate update) => update switch
    {
        SceneTaskUpdate.Phase phase => phase.TaskId,
        SceneTaskUpdate.Complete complete => complete.TaskId,
        SceneTaskUpdate.Cancel cancel => cancel.TaskId,
        _ => null,
    };
    private static SceneEventState WithTasks(SceneEventState state, SceneTaskState tasks)
        => new(state.Program, state.Inner, tasks, state.ConsumerId, state.Suspension, state.RemainingWaitTicks, state.WaitingAtOffset);
    private static ImmutableArray<BannerSceneTrace> Wrap(IEnumerable<SceneEventTrace> trace, int instructionBudget, int? updateBudget)
        => trace.Select(t => new BannerSceneTrace(BannerSceneTraceKind.Event, t.Tick, EventTrace: t,
            InstructionBudget: instructionBudget, UpdateBudget: updateBudget)).ToImmutableArray();
    private static BannerSceneResult EventFailure(BannerSceneState state, SceneEventResult result)
        => BannerSceneResult.Reject(state, result.Rejection switch
        {
            SceneEventRejection.InvalidInstructionBudget => BannerSceneRejection.InvalidInstructionBudget,
            SceneEventRejection.AlreadyExited => BannerSceneRejection.AlreadyExited,
            SceneEventRejection.AwaitingTick => BannerSceneRejection.AwaitingTick,
            SceneEventRejection.NotWaiting => BannerSceneRejection.NotWaiting,
            SceneEventRejection.InstructionBudgetExceeded => BannerSceneRejection.InstructionBudgetExceeded,
            SceneEventRejection.ArithmeticDomainExceeded => BannerSceneRejection.ArithmeticDomainExceeded,
            _ => BannerSceneRejection.TaskLedgerRejected,
        }, result.Diagnostic!, result.TaskRejection, result.FailureUpdateIndex, result.FailureOffset);
    private static BannerSceneResult Reject(BannerSceneState state, BannerSceneRejection reason, string diagnostic)
        => BannerSceneResult.Reject(state, reason, diagnostic);
    private static BannerSceneResult TaskFailure(BannerSceneState state, SceneTaskRejection reason, string diagnostic, int? index = null)
        => BannerSceneResult.Reject(state, BannerSceneRejection.TaskLedgerRejected, diagnostic, reason, index);
    private static BannerSceneResult BindingFailure(BannerSceneState state)
        => Reject(state, BannerSceneRejection.InvalidManagedTaskBinding, "Managed effect and retained scene task/clock must agree.");
    private static bool ValidInstructionBudget(int budget) => budget is >= 0 and <= ConditionalEventProgram.MaxInstructions;
    private static bool ValidUpdateBudget(int budget) => budget is >= 0 and <= SceneTaskState.MaxUpdatesPerCommand;
    private static BannerSceneResult InvalidInstructionBudget(BannerSceneState state)
        => Reject(state, BannerSceneRejection.InvalidInstructionBudget, "Instruction budget must be in 0..8192.");
    private static BannerSceneResult InvalidUpdateBudget(BannerSceneState state)
        => TaskFailure(state, SceneTaskRejection.InvalidUpdateBudget, "Update budget must be in 0..64.");
}
