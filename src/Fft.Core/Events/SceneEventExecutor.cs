using System.Collections.Immutable;

namespace Fft.Core.Events;

/// <summary>Atomic VM/scene composition. Explicit task facts, not presentation timing, determine barrier progress.</summary>
public static class SceneEventExecutor
{
    public static SceneEventResult RunUntilBlocked(SceneEventState state, int instructionBudget)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ValidBudget(instructionBudget)) return InvalidBudget(state);
        if (state.Exited) return SceneEventResult.Reject(state, SceneEventRejection.AlreadyExited, "Interpreter has exited; retain its scene ledger for the next event.", state.Offset);
        if (state.Suspension != SceneEventSuspension.Ready)
            return SceneEventResult.Reject(state, SceneEventRejection.AwaitingTick, "Suspended interpreter requires an explicit tick.", state.WaitingAtOffset!.Value);
        return Pump(state, instructionBudget);
    }

    /// <summary>Exactly one shared tick: ordered task updates/poll first, then existing suspension/resumption. Exited VMs remain exited.</summary>
    public static SceneEventResult AdvanceTick(SceneEventState state, ImmutableArray<SceneTaskUpdate> updates,
        int instructionBudget, int updateBudget = SceneTaskState.MaxUpdatesPerCommand)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ValidBudget(instructionBudget)) return InvalidBudget(state);
        if (!state.Exited && state.Suspension == SceneEventSuspension.Ready)
            return SceneEventResult.Reject(state, SceneEventRejection.NotWaiting, "Run the ready interpreter to a suspension or exit before advancing its scene.", state.Offset);
        var ledger = SceneTaskLedger.AdvanceTick(state.Tasks, updates, updateBudget);
        if (!ledger.Accepted) return TaskFailure(state, ledger, state.WaitingAtOffset ?? state.Offset);

        var trace = ImmutableArray.CreateBuilder<SceneEventTrace>();
        var current = Copy(state, state.Inner, ledger.State, state.Suspension, state.RemainingWaitTicks, state.WaitingAtOffset);
        AddLedgerTrace(trace, current, ledger.Trace);
        if (state.Exited) return SceneEventResult.Accept(current, trace.ToImmutable(), 0, ledger.AcceptedUpdates, 1);

        int offset = state.WaitingAtOffset!.Value;
        if (state.Suspension == SceneEventSuspension.Frames)
        {
            int remaining = state.RemainingWaitTicks - 1;
            current = Copy(current, current.Inner, current.Tasks,
                remaining == 0 ? SceneEventSuspension.Ready : SceneEventSuspension.Frames,
                remaining, remaining == 0 ? null : offset);
            trace.Add(EventTrace(remaining == 0 ? SceneEventTraceKind.FrameWaitResumed : SceneEventTraceKind.FrameWaitTick,
                offset, state, current));
            if (remaining != 0) return SceneEventResult.Accept(current, trace.ToImmutable(), 0, ledger.AcceptedUpdates, 1);
        }
        else
        {
            if (current.Tasks.BarrierStatus == SceneBarrierStatus.Pending)
                return SceneEventResult.Accept(current, trace.ToImmutable(), 0, ledger.AcceptedUpdates, 1);
            current = Copy(current, current.Inner, current.Tasks, SceneEventSuspension.Ready, 0, null);
            trace.Add(EventTrace(SceneEventTraceKind.BarrierWaitResumed, offset, state, current));
        }

        var resumed = Pump(current, instructionBudget);
        if (!resumed.Accepted)
            return SceneEventResult.Reject(state, resumed.Rejection, resumed.Diagnostic!, resumed.FailureOffset!.Value,
                resumed.TaskRejection, resumed.FailureUpdateIndex);
        trace.AddRange(resumed.Trace);
        return SceneEventResult.Accept(resumed.State, trace.ToImmutable(), resumed.InstructionsExecuted, ledger.AcceptedUpdates, 1);
    }

    private static SceneEventResult Pump(SceneEventState state, int budget)
    {
        var current = state;
        var trace = ImmutableArray.CreateBuilder<SceneEventTrace>();
        for (int index = 0; index < budget; index++)
        {
            var instruction = current.Program.Instructions[current.Program.Body.IndexOf(current.Offset)];
            var ordinary = ConditionalEventExecutor.Step(current.Inner);
            if (!ordinary.Accepted)
                return SceneEventResult.Reject(state, SceneEventRejection.ArithmeticDomainExceeded, ordinary.Diagnostic!, ordinary.FailureOffset!.Value);
            var before = current;
            var suspension = SceneEventSuspension.Ready;
            int remaining = instruction.WaitFrames ?? 0;
            var kind = SceneEventTraceKind.Instruction;
            var tasks = current.Tasks;
            ImmutableArray<SceneTaskTrace> ledgerTrace = [];
            if (instruction.TaskClass is { } selector)
            {
                var entered = SceneTaskLedger.BeginBarrier(tasks, selector, current.ConsumerId, instruction.Offset, current.Program.Id);
                if (!entered.Accepted) return TaskFailure(state, entered, instruction.Offset);
                tasks = entered.State;
                ledgerTrace = entered.Trace;
                suspension = SceneEventSuspension.Barrier;
                kind = SceneEventTraceKind.BarrierWaitEntered;
            }
            else if (instruction.WaitFrames is not null)
            {
                suspension = remaining == 0 ? SceneEventSuspension.Ready : SceneEventSuspension.Frames;
                kind = remaining == 0 ? SceneEventTraceKind.ZeroWait : SceneEventTraceKind.FrameWaitEntered;
            }
            current = Copy(current, ordinary.State, tasks, suspension, remaining,
                suspension == SceneEventSuspension.Ready ? null : instruction.Offset);
            trace.Add(EventTrace(kind, instruction.Offset, before, current, instruction.Operation?.Operation));
            AddLedgerTrace(trace, current, ledgerTrace);
            if (current.Exited || current.Suspension != SceneEventSuspension.Ready)
                return SceneEventResult.Accept(current, trace.ToImmutable(), index + 1, 0, 0);
        }
        return SceneEventResult.Reject(state, SceneEventRejection.InstructionBudgetExceeded,
            $"Instruction budget {budget} exhausted before a suspension or exit at {current.Offset}.", current.Offset);
    }

    private static SceneEventState Copy(SceneEventState state, ConditionalEventState inner, SceneTaskState tasks,
        SceneEventSuspension suspension, int remaining, int? waitingAt)
        => new(state.Program, inner, tasks, state.ConsumerId, suspension, remaining, waitingAt);

    private static SceneEventTrace EventTrace(SceneEventTraceKind kind, int offset, SceneEventState before,
        SceneEventState after, ConditionalEventOperation? operation = null)
        => new(kind, after.Tick, offset, before.Variables, after.Variables, after.Offset, after.RemainingWaitTicks, after.Exited, operation);

    private static void AddLedgerTrace(ImmutableArray<SceneEventTrace>.Builder trace, SceneEventState state, ImmutableArray<SceneTaskTrace> facts)
    {
        foreach (var fact in facts)
            trace.Add(new(SceneEventTraceKind.TaskLedger, fact.Tick, state.WaitingAtOffset ?? state.Offset,
                state.Variables, state.Variables, state.Offset, state.RemainingWaitTicks, state.Exited, TaskTrace: fact));
    }

    private static SceneEventResult TaskFailure(SceneEventState state, SceneTaskResult result, int offset)
        => SceneEventResult.Reject(state, SceneEventRejection.TaskLedgerRejected, result.Diagnostic!, offset, result.Rejection, result.FailureUpdateIndex);
    private static bool ValidBudget(int budget) => budget is >= 0 and <= ConditionalEventProgram.MaxInstructions;
    private static SceneEventResult InvalidBudget(SceneEventState state)
        => SceneEventResult.Reject(state, SceneEventRejection.InvalidInstructionBudget, "Instruction budget must be in 0..8192.", state.WaitingAtOffset ?? state.Offset);
}
