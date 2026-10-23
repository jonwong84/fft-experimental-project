using Fft.Core.Events;
using Xunit;

namespace Fft.Core.Tests;

public sealed class TimedEventExecutorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(32767)]
    public void Wait_count_has_exact_explicit_resume_boundary_and_batching_preserves_trace(int count)
    {
        var initial = WaitThenAdd(count).Start(new(7, 8, 2, 9));
        var entered = TimedEventExecutor.RunUntilBlocked(initial, 3);
        Assert.True(entered.Accepted);
        Assert.Equal(new(7, 8, 2, 9), initial.Variables);
        Assert.False(initial.IsWaiting);
        Assert.Equal(4, initial.Offset);
        Assert.Equal(0, entered.State.ElapsedTicks);
        Assert.Equal(0, entered.TicksConsumed);
        Assert.Equal(new TimedEventTrace(count == 0 ? TimedEventTraceKind.ZeroWait : TimedEventTraceKind.WaitEntered,
            4, null, initial.Variables, initial.Variables, 7, count, 0, false), entered.Trace[0]);
        if (count == 0)
        {
            Assert.True(entered.State.Exited);
            Assert.False(entered.State.IsWaiting);
            Assert.Equal(10, entered.State.Variables.V3);
            Assert.Equal(3, entered.InstructionsExecuted);
            Assert.Equal(new[] { TimedEventTraceKind.ZeroWait, TimedEventTraceKind.Instruction, TimedEventTraceKind.Instruction }, entered.Trace.Select(t => t.Kind).ToArray());
            return;
        }
        Assert.True(entered.State.IsWaiting);
        Assert.False(entered.State.Exited);
        Assert.Equal(count, entered.State.RemainingWaitTicks);
        Assert.Equal(4, entered.State.WaitingAtOffset);
        Assert.Equal(7, entered.State.Offset); // Continuation is not executed yet.
        Assert.Equal(1, entered.InstructionsExecuted);
        var allTicks = TimedEventExecutor.AdvanceTicks(entered.State, count, 2);
        Assert.True(allTicks.Accepted);
        Assert.True(allTicks.State.Exited);
        Assert.Null(allTicks.State.WaitingAtOffset);
        Assert.Equal(count, allTicks.State.ElapsedTicks);
        Assert.Equal(10, allTicks.State.Variables.V3);
        Assert.Equal(count, allTicks.TicksConsumed);
        Assert.Equal(2, allTicks.InstructionsExecuted);
        Assert.Equal(count + 2, allTicks.Trace.Length);
        Assert.Equal(new TimedEventTrace(TimedEventTraceKind.WaitResumed, 4, null,
            new(7,8,2,9), new(7,8,2,9), 7, 0, count, false), allTicks.Trace[count - 1]);
        Assert.Equal(new TimedEventTrace(TimedEventTraceKind.Instruction, 7, ConditionalEventOperation.AddImmediate,
            new(7,8,2,9), new(7,8,2,10), 12, 0, count, false), allTicks.Trace[count]);
        Assert.Equal(new TimedEventTrace(TimedEventTraceKind.Instruction, 12, ConditionalEventOperation.Exit,
            new(7,8,2,10), new(7,8,2,10), 12, 0, count, true), allTicks.Trace[count + 1]);
        if (count > 1)
        {
            var partial = TimedEventExecutor.AdvanceTicks(entered.State, count - 1, 0);
            Assert.True(partial.Accepted);
            Assert.True(partial.State.IsWaiting);
            Assert.Equal(1, partial.State.RemainingWaitTicks);
            Assert.Equal(count - 1, partial.State.ElapsedTicks);
            Assert.Equal(9, partial.State.Variables.V3);
            Assert.Equal(0, partial.InstructionsExecuted);
            Assert.All(partial.Trace, t => Assert.Equal(TimedEventTraceKind.WaitTick, t.Kind));
            var last = TimedEventExecutor.AdvanceTicks(partial.State, 1, 2);
            Assert.True(last.Accepted);
            Assert.Equal(allTicks.Trace.ToArray(), partial.Trace.Concat(last.Trace).ToArray());
        }
        var replay = TimedEventExecutor.AdvanceTicks(TimedEventExecutor.RunUntilBlocked(WaitThenAdd(count).Start(new(7,8,2,9)), 3).State, count, 2);
        Assert.Equal(allTicks.Trace.ToArray(), replay.Trace.ToArray());
        Assert.Equal(count, entered.State.RemainingWaitTicks); // Original waiting snapshot is unchanged.
        Assert.Equal(0, entered.State.ElapsedTicks);
    }

    [Fact]
    public void Sequential_waits_cannot_consume_the_same_tick_and_zero_wait_does_not_suspend()
    {
        var program = Program(
            new(4, 1), new(7, 0), new(10, 1), Op(13, ConditionalEventOperation.Exit));
        var entered = TimedEventExecutor.RunUntilBlocked(program.Start(default), 1);
        var first = TimedEventExecutor.AdvanceTicks(entered.State, 1, 2);
        Assert.True(first.Accepted);
        Assert.True(first.State.IsWaiting);
        Assert.Equal(1, first.State.RemainingWaitTicks);
        Assert.Equal(10, first.State.WaitingAtOffset);
        Assert.Equal(1, first.State.ElapsedTicks);
        Assert.Equal(new[] { TimedEventTraceKind.WaitResumed, TimedEventTraceKind.ZeroWait, TimedEventTraceKind.WaitEntered }, first.Trace.Select(t => t.Kind).ToArray());
        var second = TimedEventExecutor.AdvanceTicks(first.State, 1, 1);
        Assert.True(second.State.Exited);
        Assert.Equal(2, second.State.ElapsedTicks);
        var combined = TimedEventExecutor.AdvanceTicks(entered.State, 2, 3);
        Assert.True(combined.Accepted);
        Assert.Equal(first.Trace.Concat(second.Trace).ToArray(), combined.Trace.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Instruction_budget_failure_before_wait_rolls_back_all_writes(int budget)
    {
        var initial = Program(Op(4, ConditionalEventOperation.ZeroVariable, 3), new(7, 1), Op(10, ConditionalEventOperation.Exit))
            .Start(new(0,0,0,9));
        var failed = TimedEventExecutor.RunUntilBlocked(initial, budget);
        AssertRejected(failed, initial, TimedEventRejection.InstructionBudgetExceeded);
        Assert.Equal(9, failed.State.Variables.V3);
        Assert.Equal(budget == 0 ? 4 : 7, failed.FailureOffset);
    }

    [Fact]
    public void Resumption_budget_failure_rolls_back_tick_and_candidate_writes()
    {
        var initial = TimedEventExecutor.RunUntilBlocked(WaitThenAdd(1).Start(new(0,0,0,9)), 1).State;
        var failed = TimedEventExecutor.AdvanceTicks(initial, 1, 1); // Add runs, but Exit does not fit.
        AssertRejected(failed, initial, TimedEventRejection.InstructionBudgetExceeded);
        Assert.Equal(12, failed.FailureOffset);
        Assert.Equal(0, failed.State.ElapsedTicks);
        Assert.Equal(1, failed.State.RemainingWaitTicks);
        Assert.Equal(9, failed.State.Variables.V3);
        Assert.True(TimedEventExecutor.AdvanceTicks(initial, 1, 2).Accepted);
    }

    [Fact]
    public void Resumption_budget_is_shared_across_all_requested_ticks()
    {
        var entered = TimedEventExecutor.RunUntilBlocked(Program(new(4,1), new(7,1), Op(10, ConditionalEventOperation.Exit)).Start(default), 1).State;
        AssertRejected(TimedEventExecutor.AdvanceTicks(entered, 2, 1), entered, TimedEventRejection.InstructionBudgetExceeded);
        Assert.True(TimedEventExecutor.AdvanceTicks(entered, 2, 2).State.Exited);
    }

    [Fact]
    public void Arithmetic_failure_after_wait_rolls_back_elapsed_ticks_and_intermediate_writes()
    {
        var entered = TimedEventExecutor.RunUntilBlocked(Program(new(4,1), Op(7, ConditionalEventOperation.ZeroVariable, 1),
            Op(10, ConditionalEventOperation.AddImmediate, 3, 1), Op(15, ConditionalEventOperation.Exit)).Start(new(0,8,0,65535)), 1).State;
        var failed = TimedEventExecutor.AdvanceTicks(entered, 1, 3);
        AssertRejected(failed, entered, TimedEventRejection.ArithmeticDomainExceeded);
        Assert.Equal(10, failed.FailureOffset);
        Assert.Equal(8, failed.State.Variables.V1);
        Assert.Equal(0, failed.State.ElapsedTicks);
    }

    [Fact]
    public void Arithmetic_failure_before_wait_does_not_publish_a_boundary()
    {
        var initial = Program(Op(4, ConditionalEventOperation.AddVariable, 3, 3), new(9,1), Op(12, ConditionalEventOperation.Exit))
            .Start(new(0,0,0,32768));
        AssertRejected(TimedEventExecutor.RunUntilBlocked(initial, 3), initial, TimedEventRejection.ArithmeticDomainExceeded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(32768)]
    public void Tick_input_limits_reject_without_consuming_ticks(int ticks)
    {
        var waiting = TimedEventExecutor.RunUntilBlocked(WaitThenAdd(1).Start(default), 1).State;
        AssertRejected(TimedEventExecutor.AdvanceTicks(waiting, ticks, 2), waiting, TimedEventRejection.InvalidTickCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8193)]
    public void Instruction_budget_limits_apply_to_both_APIs(int budget)
    {
        var initial = WaitThenAdd(1).Start(default);
        AssertRejected(TimedEventExecutor.RunUntilBlocked(initial, budget), initial, TimedEventRejection.InvalidInstructionBudget);
        var waiting = TimedEventExecutor.RunUntilBlocked(initial, 1).State;
        AssertRejected(TimedEventExecutor.AdvanceTicks(waiting, 1, budget), waiting, TimedEventRejection.InvalidInstructionBudget);
    }

    [Fact]
    public void Ready_waiting_and_exited_boundaries_reject_wrong_commands_and_excess_ticks()
    {
        var initial = WaitThenAdd(1).Start(default);
        AssertRejected(TimedEventExecutor.AdvanceTicks(initial, 1, 2), initial, TimedEventRejection.NotWaiting);
        var waiting = TimedEventExecutor.RunUntilBlocked(initial, 1).State;
        AssertRejected(TimedEventExecutor.RunUntilBlocked(waiting, 3), waiting, TimedEventRejection.AwaitingTick);
        AssertRejected(TimedEventExecutor.AdvanceTicks(waiting, 2, 2), waiting, TimedEventRejection.UnusedTicks);
        var exited = TimedEventExecutor.AdvanceTicks(waiting, 1, 2).State;
        AssertRejected(TimedEventExecutor.AdvanceTicks(exited, 1, 2), exited, TimedEventRejection.AlreadyExited);
        AssertRejected(TimedEventExecutor.RunUntilBlocked(exited, 2), exited, TimedEventRejection.AlreadyExited);
    }

    [Fact]
    public void Forward_branches_skip_waits_and_normalized_operations_keep_conditional_semantics()
    {
        var program = Program(Op(4, ConditionalEventOperation.CompareEqual),
            Op(5, ConditionalEventOperation.JumpForwardIfZero, target: 12), new(7,60),
            Op(10, ConditionalEventOperation.JumpForward, target: 15), Op(12, ConditionalEventOperation.Target),
            Op(14, ConditionalEventOperation.Nop), Op(15, ConditionalEventOperation.Exit));
        var falsePath = TimedEventExecutor.RunUntilBlocked(program.Start(new(0,1,0,0)), 5);
        Assert.True(falsePath.State.Exited);
        Assert.Equal(new[] {4,5,12,14,15}, falsePath.Trace.Select(t => t.Offset).ToArray());
        var truePath = TimedEventExecutor.RunUntilBlocked(program.Start(new(1,1,0,0)), 3);
        Assert.True(truePath.State.IsWaiting);
        var resumed = TimedEventExecutor.AdvanceTicks(truePath.State, 60, 2);
        Assert.True(resumed.State.Exited);
        Assert.Equal(1, resumed.State.Variables.V0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32768)]
    public void Out_of_domain_waits_reject_before_a_program_can_be_started(int count)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TimedEventInstruction(4, count));

    [Fact]
    public void Program_admission_keeps_cfg_operand_domain_and_collection_guards()
    {
        Assert.Throws<ArgumentException>(() => Program(new TimedEventInstruction(4,1))); // No explicit Exit.
        Assert.Throws<ArgumentException>(() => Program(new(4,1), Op(4, ConditionalEventOperation.Exit)));
        Assert.Throws<ArgumentException>(() => Program(Op(4, (ConditionalEventOperation)999)));
        Assert.Throws<ArgumentException>(() => Program(Op(4, ConditionalEventOperation.AddVariable, 0, 33), Op(9, ConditionalEventOperation.Exit)));
        Assert.Throws<ArgumentException>(() => Program(new TimedEventInstruction[] { null! }));
        Assert.Throws<ArgumentException>(() => new TimedEventProgram(default, [Op(4, ConditionalEventOperation.Exit)]));
        var records = new List<TimedEventInstruction> {new(4,1), Op(7, ConditionalEventOperation.Exit)};
        var program = new TimedEventProgram(new("sample:event/snapshot"), records);
        records.Clear();
        Assert.Equal(2, program.Instructions.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => program.Start(new(0,0,0,-1)));
    }

    private static TimedEventProgram WaitThenAdd(int count) => Program(new(4, count), Op(7, ConditionalEventOperation.AddImmediate, 3, 1), Op(12, ConditionalEventOperation.Exit));
    private static TimedEventProgram Program(params TimedEventInstruction[] instructions) => new(new("sample:event/timed-proof"), instructions);
    private static TimedEventInstruction Op(int offset, ConditionalEventOperation operation, int? first = null, int? second = null, int? target = null)
        => new(new ConditionalEventInstruction(offset, operation, first, second, target));
    private static void AssertRejected(TimedEventResult result, TimedEventState original, TimedEventRejection reason)
    {
        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Rejection);
        Assert.Same(original, result.State);
        Assert.Empty(result.Trace);
        Assert.Equal(0, result.InstructionsExecuted);
        Assert.Equal(0, result.TicksConsumed);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
        Assert.NotNull(result.FailureOffset);
    }
}
