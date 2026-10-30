using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum TimedEventRejection
{
    None, AlreadyExited, AwaitingTick, NotWaiting, ArithmeticDomainExceeded,
    InstructionBudgetExceeded, InvalidInstructionBudget, InvalidTickCount, UnusedTicks,
}

public enum TimedEventTraceKind { Instruction, ZeroWait, WaitEntered, WaitTick, WaitResumed }

public sealed record TimedEventTrace(TimedEventTraceKind Kind, int Offset, ConditionalEventOperation? Operation,
    ConditionalEventVariables Before, ConditionalEventVariables After, int NextOffset,
    int RemainingWaitTicks, long Tick, bool Exited);

public sealed class TimedEventResult
{
    public bool Accepted => Rejection == TimedEventRejection.None;
    public TimedEventRejection Rejection { get; }
    public string? Diagnostic { get; }
    public int? FailureOffset { get; }
    public TimedEventState State { get; }
    public ImmutableArray<TimedEventTrace> Trace { get; }
    public int InstructionsExecuted { get; }
    public int TicksConsumed { get; }

    private TimedEventResult(TimedEventState state, ImmutableArray<TimedEventTrace> trace,
        int instructionsExecuted, int ticksConsumed, TimedEventRejection rejection, string? diagnostic, int? failureOffset)
        => (State, Trace, InstructionsExecuted, TicksConsumed, Rejection, Diagnostic, FailureOffset) =
            (state, trace, instructionsExecuted, ticksConsumed, rejection, diagnostic, failureOffset);

    internal static TimedEventResult Accept(TimedEventState state, ImmutableArray<TimedEventTrace> trace, int instructions, int ticks)
        => new(state, trace, instructions, ticks, TimedEventRejection.None, null, null);
    internal static TimedEventResult Reject(TimedEventState state, TimedEventRejection reason, string message, int offset)
        => new(state, [], 0, 0, reason, message, offset);
}

/// <summary>Pure timed proof. Every call commits all requested work or returns its exact input and an empty trace.</summary>
public static class TimedEventExecutor
{
    /// <summary>Execute ordinary/zero-wait instructions until a positive wait or explicit Exit. Consumes no ticks.</summary>
    public static TimedEventResult RunUntilBlocked(TimedEventState state, int instructionBudget)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ValidBudget(instructionBudget)) return InvalidBudget(state);
        if (state.Exited) return AlreadyExited(state);
        if (state.IsWaiting)
            return TimedEventResult.Reject(state, TimedEventRejection.AwaitingTick, "Pending wait requires explicit scheduler ticks.", state.WaitingAtOffset!.Value);
        return Pump(state, instructionBudget);
    }

    /// <summary>Consume exactly 1..32767 explicit ticks; each completed wait resumes immediately within the shared instruction budget.</summary>
    public static TimedEventResult AdvanceTicks(TimedEventState state, int ticks, int instructionBudget)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ValidBudget(instructionBudget)) return InvalidBudget(state);
        if (ticks is < 1 or > TimedEventProgram.MaxTicksPerCall)
            return TimedEventResult.Reject(state, TimedEventRejection.InvalidTickCount,
                $"Tick count must be in 1..{TimedEventProgram.MaxTicksPerCall}.", state.WaitingAtOffset ?? state.Offset);
        if (state.Exited) return AlreadyExited(state);
        if (!state.IsWaiting)
            return TimedEventResult.Reject(state, TimedEventRejection.NotWaiting, "Run to a pending wait before supplying ticks.", state.Offset);
        var current = state;
        int instructions = 0;
        var trace = ImmutableArray.CreateBuilder<TimedEventTrace>();
        for (int tick = 0; tick < ticks; tick++)
        {
            if (!current.IsWaiting)
                return TimedEventResult.Reject(state, TimedEventRejection.UnusedTicks,
                    "Requested ticks extend past explicit thread exit; no ticks were committed.", current.Offset);
            int waitOffset = current.WaitingAtOffset!.Value;
            int remaining = current.RemainingWaitTicks - 1;
            current = new(current.Program, current.Inner, remaining, remaining == 0 ? null : waitOffset, current.ElapsedTicks + 1);
            trace.Add(new(remaining == 0 ? TimedEventTraceKind.WaitResumed : TimedEventTraceKind.WaitTick,
                waitOffset, null, current.Variables, current.Variables, current.Offset, remaining, current.ElapsedTicks, false));
            if (remaining != 0) continue;
            var resumed = Pump(current, instructionBudget - instructions);
            if (!resumed.Accepted)
                return TimedEventResult.Reject(state, resumed.Rejection, resumed.Diagnostic!, resumed.FailureOffset!.Value);
            instructions += resumed.InstructionsExecuted;
            trace.AddRange(resumed.Trace);
            current = resumed.State;
        }
        return TimedEventResult.Accept(current, trace.ToImmutable(), instructions, ticks);
    }

    private static TimedEventResult Pump(TimedEventState state, int instructionBudget)
    {
        var current = state;
        var trace = ImmutableArray.CreateBuilder<TimedEventTrace>();
        for (int step = 0; step < instructionBudget; step++)
        {
            var instruction = current.Program.Instructions[current.Program.Body.IndexOf(current.Offset)];
            var ordinary = ConditionalEventExecutor.Step(current.Inner);
            if (!ordinary.Accepted)
                return TimedEventResult.Reject(state, TimedEventRejection.ArithmeticDomainExceeded,
                    ordinary.Diagnostic!, ordinary.FailureOffset!.Value);
            var previous = current;
            int remaining = instruction.WaitFrames ?? 0;
            current = new(current.Program, ordinary.State, remaining, remaining == 0 ? null : instruction.Offset, current.ElapsedTicks);
            var kind = instruction.WaitFrames is null ? TimedEventTraceKind.Instruction :
                remaining == 0 ? TimedEventTraceKind.ZeroWait : TimedEventTraceKind.WaitEntered;
            trace.Add(new(kind, instruction.Offset, instruction.Operation?.Operation,
                previous.Variables, current.Variables, current.Offset, remaining, current.ElapsedTicks, current.Exited));
            if (current.Exited || current.IsWaiting)
                return TimedEventResult.Accept(current, trace.ToImmutable(), step + 1, 0);
        }
        return TimedEventResult.Reject(state, TimedEventRejection.InstructionBudgetExceeded,
            $"Instruction budget {instructionBudget} exhausted before a wait or exit at {current.Offset}.", current.Offset);
    }

    private static bool ValidBudget(int budget) => budget is >= 0 and <= ConditionalEventProgram.MaxInstructions;
    private static TimedEventResult InvalidBudget(TimedEventState state) => TimedEventResult.Reject(state,
        TimedEventRejection.InvalidInstructionBudget, $"Instruction budget must be in 0..{ConditionalEventProgram.MaxInstructions}.", state.WaitingAtOffset ?? state.Offset);
    private static TimedEventResult AlreadyExited(TimedEventState state) => TimedEventResult.Reject(state,
        TimedEventRejection.AlreadyExited, "Event thread has already exited.", state.Offset);
}
