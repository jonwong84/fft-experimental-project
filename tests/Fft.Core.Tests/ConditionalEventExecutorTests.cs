using Fft.Core.Events;
using Xunit;

namespace Fft.Core.Tests;

public sealed class ConditionalEventExecutorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(65535)]
    public void Conditional_event_matches_independent_dirty_variable_trace_and_replays(int input)
    {
        var program = Conditional();
        var initial = program.Start(new(7, 8, input, 9));
        var expected = Expected(input);
        var result = ConditionalEventExecutor.Run(initial, 10);
        Assert.True(result.Accepted);
        Assert.True(result.State.Exited);
        Assert.Equal(45, result.State.Offset);
        Assert.Equal(new(7, 8, input, 9), initial.Variables);
        Assert.False(initial.Exited);
        Assert.Equal(4, initial.Offset);
        Assert.Equal(expected, result.Trace.ToArray());
        Assert.Null(result.Diagnostic);
        Assert.Null(result.FailureOffset);

        var replay = ConditionalEventExecutor.Run(program.Start(new(7, 8, input, 9)), 10);
        Assert.Equal(result.State.Variables, replay.State.Variables);
        Assert.Equal(result.Trace.ToArray(), replay.Trace.ToArray());
        var current = initial;
        foreach (var entry in expected)
        {
            var before = current.Variables;
            var step = ConditionalEventExecutor.Step(current);
            Assert.True(step.Accepted);
            Assert.Equal(entry, Assert.Single(step.Trace));
            Assert.Equal(before, current.Variables);
            current = step.State;
        }
        Assert.Equal(result.State.Variables, current.Variables);
        Assert.True(current.Exited);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public void Insufficient_budget_rolls_back_all_candidate_steps(int budget)
    {
        var initial = Conditional().Start(new(7, 8, 1, 9));
        var result = ConditionalEventExecutor.Run(initial, budget);
        AssertRejected(result, initial, ConditionalEventRejection.BudgetExceeded);
        Assert.Contains("budget", result.Diagnostic);
        Assert.Equal(budget == 0 ? 4 : budget == 1 ? 7 : 45, result.FailureOffset);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8193)]
    public void Invalid_budget_is_rejected_before_execution(int budget)
    {
        var initial = Conditional().Start(new(7, 8, 1, 9));
        AssertRejected(ConditionalEventExecutor.Run(initial, budget), initial, ConditionalEventRejection.InvalidBudget);
    }

    [Fact]
    public void Exact_and_maximum_budget_include_the_exit_instruction_and_can_continue_an_immutable_boundary()
    {
        var initial = Conditional().Start(new(7, 8, 1, 9));
        var first = ConditionalEventExecutor.Step(initial);
        var remainder = ConditionalEventExecutor.Run(first.State, 9);
        var whole = ConditionalEventExecutor.Run(initial, ConditionalEventProgram.MaxInstructions);
        Assert.True(remainder.Accepted);
        Assert.Equal(whole.State.Variables, remainder.State.Variables);
        Assert.Equal(whole.Trace.Skip(1).ToArray(), remainder.Trace.ToArray());
        AssertRejected(ConditionalEventExecutor.Step(whole.State), whole.State, ConditionalEventRejection.AlreadyExited);
        AssertRejected(ConditionalEventExecutor.Run(whole.State, 10), whole.State, ConditionalEventRejection.AlreadyExited);
    }

    [Fact]
    public void Arithmetic_domain_failure_never_returns_partial_run_state_or_trace()
    {
        var program = new ConditionalEventProgram(new("sample:event/arithmetic"),
        [new(4, ConditionalEventOperation.ZeroVariable, 1),
         new(7, ConditionalEventOperation.AddImmediate, 2, 65535), new(12, ConditionalEventOperation.Exit)]);
        var initial = program.Start(new(7, 8, 1, 9));
        var result = ConditionalEventExecutor.Run(initial, 3);
        AssertRejected(result, initial, ConditionalEventRejection.ArithmeticDomainExceeded);
        Assert.Equal(7, result.FailureOffset);
        Assert.Contains("0..65535", result.Diagnostic);
        var cleared = ConditionalEventExecutor.Step(initial).State;
        Assert.Equal(0, cleared.Variables.V1);
        AssertRejected(ConditionalEventExecutor.Step(cleared), cleared, ConditionalEventRejection.ArithmeticDomainExceeded);
        var boundary = ConditionalEventExecutor.Run(program.Start(new(7, 8, 0, 9)), 3);
        Assert.True(boundary.Accepted);
        Assert.Equal(65535, boundary.State.Variables.V2);
    }

    [Theory]
    [InlineData(32767, true, 65534)]
    [InlineData(32768, false, 32768)]
    public void Add_variable_reads_alias_before_writing_and_checks_sum(int value, bool accepted, int expected)
    {
        var program = new ConditionalEventProgram(new("sample:event/alias"),
            [new(4, ConditionalEventOperation.AddVariable, 2, 2), new(9, ConditionalEventOperation.Exit)]);
        var initial = program.Start(new(0, 0, value, 0));
        var result = ConditionalEventExecutor.Run(initial, 2);
        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(expected, result.State.Variables.V2);
        if (!accepted) AssertRejected(result, initial, ConditionalEventRejection.ArithmeticDomainExceeded);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(65535, 65535, 1)]
    [InlineData(0, 1, 0)]
    public void Equality_replaces_only_variable_zero(int left, int right, int expected)
    {
        var program = new ConditionalEventProgram(new("sample:event/equal"),
            [new(4, ConditionalEventOperation.CompareEqual), new(5, ConditionalEventOperation.Exit)]);
        var result = ConditionalEventExecutor.Run(program.Start(new(left, right, 3, 4)), 2);
        Assert.Equal(new(expected, right, 3, 4), result.State.Variables);
    }

    [Fact]
    public void Nop_and_target_fall_through_without_writes_and_sequential_exit_stops()
    {
        var program = new ConditionalEventProgram(new("sample:event/nop"),
        [new(4, ConditionalEventOperation.Nop), new(5, ConditionalEventOperation.Target),
         new(7, ConditionalEventOperation.Exit), new(9, ConditionalEventOperation.AddImmediate, 3, 1)]);
        var result = ConditionalEventExecutor.Run(program.Start(new(1, 2, 3, 4)), 3);
        Assert.True(result.Accepted);
        Assert.Equal(new(1, 2, 3, 4), result.State.Variables);
        Assert.Equal(new[] { 4, 5, 7 }, result.Trace.Select(entry => entry.Offset).ToArray());
        Assert.Equal(new[] { 5, 7, 7 }, result.Trace.Select(entry => entry.NextOffset).ToArray());
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, 65536, 0, 0)]
    [InlineData(0, 0, int.MaxValue, 0)]
    [InlineData(0, 0, 0, int.MinValue)]
    public void Initial_domain_is_validated_for_every_variable(int a, int b, int c, int d)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Conditional().Start(new(a, b, c, d)));

    [Theory]
    [InlineData("empty")]
    [InlineData("null-record")]
    [InlineData("unknown-operation")]
    [InlineData("duplicate-offset")]
    [InlineData("negative-offset")]
    [InlineData("variable")]
    [InlineData("source-variable")]
    [InlineData("negative-immediate")]
    [InlineData("large-immediate")]
    [InlineData("extra-operand")]
    [InlineData("missing-target")]
    [InlineData("extra-target")]
    [InlineData("backward-target")]
    [InlineData("unknown-target")]
    [InlineData("fall-off")]
    [InlineData("conditional-fall-off")]
    [InlineData("too-many")]
    public void Malformed_or_unsupported_definitions_fail_before_any_state_exists(string problem)
    {
        ConditionalEventInstruction[] records = problem switch
        {
            "empty" => [],
            "null-record" => [null!],
            "unknown-operation" => [new(4, (ConditionalEventOperation)999)],
            "duplicate-offset" => [new(4, ConditionalEventOperation.Nop), new(4, ConditionalEventOperation.Exit)],
            "negative-offset" => [new(-1, ConditionalEventOperation.Exit)],
            "variable" => [new(4, ConditionalEventOperation.ZeroVariable, 4)],
            "source-variable" => [new(4, ConditionalEventOperation.AddVariable, 0, 0x21)],
            "negative-immediate" => [new(4, ConditionalEventOperation.AddImmediate, 0, -1)],
            "large-immediate" => [new(4, ConditionalEventOperation.AddImmediate, 0, 65536)],
            "extra-operand" => [new(4, ConditionalEventOperation.Exit, 0)],
            "missing-target" => [new(4, ConditionalEventOperation.JumpForward)],
            "extra-target" => [new(4, ConditionalEventOperation.Nop, TargetOffset: 5), new(5, ConditionalEventOperation.Exit)],
            "backward-target" => [new(4, ConditionalEventOperation.JumpForward, TargetOffset: 4)],
            "unknown-target" => [new(4, ConditionalEventOperation.JumpForward, TargetOffset: 6), new(5, ConditionalEventOperation.Exit)],
            "fall-off" => [new(4, ConditionalEventOperation.Nop)],
            "conditional-fall-off" => [new(4, ConditionalEventOperation.JumpForwardIfZero, TargetOffset: 6),
                new(5, ConditionalEventOperation.JumpForward, TargetOffset: 7), new(6, ConditionalEventOperation.Exit), new(7, ConditionalEventOperation.Nop)],
            _ => Enumerable.Range(0, ConditionalEventProgram.MaxInstructions + 1).Select(i => new ConditionalEventInstruction(i, ConditionalEventOperation.Exit)).ToArray(),
        };
        Assert.Throws<ArgumentException>(() => new ConditionalEventProgram(new("sample:event/invalid"), records));
    }

    [Fact]
    public void Program_snapshots_caller_records_and_requires_an_id()
    {
        var records = new List<ConditionalEventInstruction> { new(4, ConditionalEventOperation.Exit) };
        var program = new ConditionalEventProgram(new("sample:event/snapshot"), records);
        records.Clear();
        Assert.Single(program.Instructions);
        Assert.True(ConditionalEventExecutor.Run(program.Start(default), 1).State.Exited);
        Assert.Throws<ArgumentException>(() => new ConditionalEventProgram(default, program.Instructions));
    }

    private static void AssertRejected(ConditionalEventResult result, ConditionalEventState initial, ConditionalEventRejection reason)
    {
        Assert.False(result.Accepted);
        Assert.Equal(reason, result.Rejection);
        Assert.Same(initial, result.State);
        Assert.Empty(result.Trace);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
        Assert.NotNull(result.FailureOffset);
    }

    private static ConditionalEventProgram Conditional() => new(new("sample:event/conditional-outcome-v1"),
    [new(4, ConditionalEventOperation.ZeroVariable, 0), new(7, ConditionalEventOperation.AddVariable, 0, 2),
     new(12, ConditionalEventOperation.ZeroVariable, 1), new(15, ConditionalEventOperation.AddImmediate, 1, 1),
     new(20, ConditionalEventOperation.CompareEqual), new(21, ConditionalEventOperation.JumpForwardIfZero, TargetOffset: 35),
     new(23, ConditionalEventOperation.ZeroVariable, 3), new(26, ConditionalEventOperation.AddImmediate, 3, 1),
     new(31, ConditionalEventOperation.JumpForward, TargetOffset: 45), new(33, ConditionalEventOperation.Target),
     new(35, ConditionalEventOperation.ZeroVariable, 3), new(38, ConditionalEventOperation.AddImmediate, 3, 2),
     new(43, ConditionalEventOperation.Target), new(45, ConditionalEventOperation.Exit)]);

    // Independently authored from the pinned ZOD-040 trace; never computed by executing or inspecting the program.
    private static ConditionalEventTrace[] Expected(int input)
    {
        int equal = input == 1 ? 1 : 0;
        var prefix = new ConditionalEventTrace[]
        {
            new(4, ConditionalEventOperation.ZeroVariable, new(7,8,input,9), new(0,8,input,9), 7, false),
            new(7, ConditionalEventOperation.AddVariable, new(0,8,input,9), new(input,8,input,9), 12, false),
            new(12, ConditionalEventOperation.ZeroVariable, new(input,8,input,9), new(input,0,input,9), 15, false),
            new(15, ConditionalEventOperation.AddImmediate, new(input,0,input,9), new(input,1,input,9), 20, false),
            new(20, ConditionalEventOperation.CompareEqual, new(input,1,input,9), new(equal,1,input,9), 21, false),
            new(21, ConditionalEventOperation.JumpForwardIfZero, new(equal,1,input,9), new(equal,1,input,9), equal == 1 ? 23 : 35, false),
        };
        ConditionalEventTrace[] suffix = input == 1
            ? [new(23, ConditionalEventOperation.ZeroVariable, new(1,1,1,9), new(1,1,1,0), 26, false),
               new(26, ConditionalEventOperation.AddImmediate, new(1,1,1,0), new(1,1,1,1), 31, false),
               new(31, ConditionalEventOperation.JumpForward, new(1,1,1,1), new(1,1,1,1), 45, false),
               new(45, ConditionalEventOperation.Exit, new(1,1,1,1), new(1,1,1,1), 45, true)]
            : [new(35, ConditionalEventOperation.ZeroVariable, new(0,1,input,9), new(0,1,input,0), 38, false),
               new(38, ConditionalEventOperation.AddImmediate, new(0,1,input,0), new(0,1,input,2), 43, false),
               new(43, ConditionalEventOperation.Target, new(0,1,input,2), new(0,1,input,2), 45, false),
               new(45, ConditionalEventOperation.Exit, new(0,1,input,2), new(0,1,input,2), 45, true)];
        return [.. prefix, .. suffix];
    }
}
