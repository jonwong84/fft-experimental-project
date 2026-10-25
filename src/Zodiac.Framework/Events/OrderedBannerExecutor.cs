using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Atomic event-before-worker execution of one typed, preloaded general banner.</summary>
public static class OrderedBannerExecutor
{
    public static OrderedBannerResult RunUntilBlocked(OrderedBannerState state, int instructionBudget,
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

    public static OrderedBannerResult AdvanceTick(OrderedBannerState state, bool? circlePressed,
        ImmutableArray<SceneTaskUpdate> unmanagedUpdates, int instructionBudget,
        int updateBudget = SceneTaskState.MaxUpdatesPerCommand, BannerCancellation? cancellation = null)
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
            if (state.Banner is null) return Reject(state, BannerSceneRejection.EffectNotAttached, "No managed banner exists to cancel.");
            var badCancellation = BannerSceneExecutor.ValidateCancellation(state.Scene, cancellation);
            if (badCancellation is not null)
                return OrderedBannerResult.Reject(state, badCancellation.Rejection, badCancellation.Diagnostic!,
                    badCancellation.TaskRejection, badCancellation.FailureUpdateIndex);
        }

        // Poll only facts published by the preceding transaction. No supplied worker update is visible yet.
        var resumed = SceneEventExecutor.ResumeTick(state.Event, [], 0);
        if (!resumed.Accepted) return EventFailure(state, resumed);
        var trace = ImmutableArray.CreateBuilder<BannerSceneTrace>();
        trace.AddRange(Wrap(resumed.Trace, instructionBudget, updateBudget));
        var current = state.With(resumed.State, state.Banner);
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
        // Also protects an identity admitted earlier in this call, even if an external fact predicted its sequence.
        for (int i = 0; i < unmanagedUpdates.Length; i++)
            if (current.Banner is { } managed && BannerSceneExecutor.ReferencedId(unmanagedUpdates[i]) == managed.TaskId)
                return OrderedBannerResult.Reject(state, BannerSceneRejection.ManagedTaskUpdateForbidden,
                    "Only the driver can update its retained managed task.", updateIndex: i);
        var applied = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks, unmanagedUpdates, updateBudget - updates);
        if (!applied.Accepted) return LedgerFailure(state, applied);
        current = current.With(BannerSceneExecutor.WithTasks(current.Event, applied.State), current.Banner);
        updates += applied.AcceptedUpdates;
        trace.AddRange(TaskTrace(applied.Trace, instructionBudget, updateBudget));

        int effectTicks = 0;
        if (state.Banner is { } before)
        {
            var after = before.Advance(current.Event.Tick, circlePressed.Value, cancellation is not null);
            trace.Add(new(before.IsTerminal ? BannerSceneTraceKind.EffectObserved : BannerSceneTraceKind.EffectAdvanced,
                current.Event.Tick, Before: before, After: after, CirclePressed: circlePressed, Cancellation: cancellation,
                InstructionBudget: instructionBudget, UpdateBudget: updateBudget));
            effectTicks = before.IsTerminal ? 0 : 1;
            if (!before.IsTerminal && after.IsTerminal)
            {
                SceneTaskUpdate terminal = after.Phase == GeneralBannerPhase.Cancelled
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
        // If no effect existed before this call, any new effect stays at its first yield. This Circle sample is discarded.
        return OrderedBannerResult.Accept(current, trace.ToImmutable(), instructions, updates, effectTicks, 1);
    }

    public static OrderedBannerResult StartNextEvent(OrderedBannerState state, OrderedBannerProgram? program,
        ConditionalEventVariables variables, AssetId consumerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Event.Exited) return Reject(state, BannerSceneRejection.EventNotExited, "Only an exited interpreter can be replaced.");
        if (program is null) return Reject(state, BannerSceneRejection.InvalidProgram, "A validated ordered program is required.");
        if (!variables.InDomain) return Reject(state, BannerSceneRejection.InvalidVariables, "Proof variables must be in 0..65535.");
        if (!SceneTaskState.ValidId(consumerId) || consumerId == state.Event.ConsumerId)
            return Reject(state, BannerSceneRejection.InvalidId, "A new explicit consumer ID is required.");
        var invalid = Validate(state, 0, 0);
        if (invalid is not null) return invalid;
        var next = new OrderedBannerState(program, program.Body.Start(variables, state.Event.Tasks, consumerId), state.Banner, state.ReadyBinding);
        var invalidNext = Validate(next, 0, 0);
        if (invalidNext is not null) return Rebase(state, invalidNext);
        return OrderedBannerResult.Accept(next,
            [new(BannerSceneTraceKind.EventStarted, next.Event.Tick, Before: state.Banner, After: state.Banner, StartedEvent: next.Event)]);
    }

    private static OrderedBannerResult Pump(OrderedBannerState state, int instructionBudget, int updateBudget)
    {
        var current = state;
        var trace = ImmutableArray.CreateBuilder<BannerSceneTrace>();
        int updates = 0;
        for (int i = 0; i < instructionBudget; i++)
        {
            if (current.Event.Offset == current.Program.DisplayOffset)
            {
                if (current.Banner is not null) return Reject(state, BannerSceneRejection.EffectAlreadyAttached, "A retained effect prevents another admission.");
                var display = current.Program.Display!;
                var started = SceneTaskLedger.ApplyPostVmUpdates(current.Event.Tasks,
                    [new SceneTaskUpdate.Start(SceneTaskFamily.ConditionDisplay, display.OwnerId)], updateBudget - updates);
                if (!started.Accepted) return LedgerFailure(state, started);
                var banner = new GeneralBannerState(display.Definition, started.State.Tasks[^1].Id, display.OwnerId,
                    current.Event.Tick, current.Event.Tick, GeneralBannerPhase.Revealing, 0, 0, 0, null);
                current = current.With(BannerSceneExecutor.WithTasks(current.Event, started.State), banner);
                updates++;
                trace.AddRange(TaskTrace(started.Trace, instructionBudget, updateBudget));
                trace.Add(new(BannerSceneTraceKind.EffectAttached, current.Event.Tick, After: banner,
                    InstructionBudget: instructionBudget, UpdateBudget: updateBudget));
            }
            var step = SceneEventExecutor.StepInstruction(current.Event);
            if (!step.Accepted) return EventFailure(state, step);
            current = current.With(step.State, current.Banner);
            trace.AddRange(Wrap(step.Trace, instructionBudget, updateBudget));
            if (current.Event.Exited || current.Event.Suspension != SceneEventSuspension.Ready)
                return OrderedBannerResult.Accept(current, trace.ToImmutable(), i + 1, updates);
        }
        return OrderedBannerResult.Reject(state, BannerSceneRejection.InstructionBudgetExceeded,
            "Instruction budget exhausted before suspension or exit.", offset: current.Event.Offset);
    }

    private static OrderedBannerResult? Validate(OrderedBannerState state, int instructionBudget, int updateBudget)
    {
        if (instructionBudget is < 0 or > ConditionalEventProgram.MaxInstructions)
            return Reject(state, BannerSceneRejection.InvalidInstructionBudget, "Instruction budget must be in 0..8192.");
        if (updateBudget is < 0 or > SceneTaskState.MaxUpdatesPerCommand)
            return TaskFailure(state, SceneTaskRejection.InvalidUpdateBudget, "Update budget must be in 0..64.");
        if (!BannerSceneExecutor.ValidBinding(state.Scene))
            return Reject(state, BannerSceneRejection.InvalidManagedTaskBinding, "Effect, ledger and clock must agree.");
        if (state.Program.Display is { } display && (state.ReadyBinding is not { } binding ||
            binding.DefinitionId != display.Definition.Id || binding.ArtworkId != display.ArtworkId || binding.OwnerId != display.OwnerId))
            return Reject(state, BannerSceneRejection.InvalidManagedTaskBinding, "A matching preloaded definition/artwork/owner binding is required.");
        if (state.Banner is { } banner && (state.ReadyBinding is not { } retained ||
            retained.DefinitionId != banner.Definition.Id || retained.OwnerId != banner.OwnerId))
            return Reject(state, BannerSceneRejection.InvalidManagedTaskBinding, "The retained effect requires its preloaded binding.");
        return null;
    }

    private static IEnumerable<BannerSceneTrace> Wrap(IEnumerable<SceneEventTrace> facts, int instructionBudget, int updateBudget)
        => facts.Select(f => new BannerSceneTrace(BannerSceneTraceKind.Event, f.Tick, EventTrace: f,
            InstructionBudget: instructionBudget, UpdateBudget: updateBudget));
    private static IEnumerable<BannerSceneTrace> TaskTrace(IEnumerable<SceneTaskTrace> facts, int instructionBudget, int updateBudget)
        => facts.Select(f => new BannerSceneTrace(BannerSceneTraceKind.TaskLedger, f.Tick, TaskTrace: f,
            InstructionBudget: instructionBudget, UpdateBudget: updateBudget));
    private static OrderedBannerResult Rebase(OrderedBannerState original, OrderedBannerResult failed)
        => OrderedBannerResult.Reject(original, failed.Rejection, failed.Diagnostic!, failed.TaskRejection, failed.FailureUpdateIndex, failed.FailureOffset);
    private static OrderedBannerResult EventFailure(OrderedBannerState state, SceneEventResult failed)
        => OrderedBannerResult.Reject(state, failed.Rejection == SceneEventRejection.ArithmeticDomainExceeded
            ? BannerSceneRejection.ArithmeticDomainExceeded : BannerSceneRejection.TaskLedgerRejected,
            failed.Diagnostic!, failed.TaskRejection, failed.FailureUpdateIndex, failed.FailureOffset);
    private static OrderedBannerResult LedgerFailure(OrderedBannerState state, SceneTaskResult failed)
        => TaskFailure(state, failed.Rejection, failed.Diagnostic!, failed.FailureUpdateIndex);
    private static OrderedBannerResult TaskFailure(OrderedBannerState state, SceneTaskRejection reason, string diagnostic, int? index = null)
        => OrderedBannerResult.Reject(state, BannerSceneRejection.TaskLedgerRejected, diagnostic, reason, index);
    private static OrderedBannerResult Reject(OrderedBannerState state, BannerSceneRejection reason, string diagnostic)
        => OrderedBannerResult.Reject(state, reason, diagnostic);
}
