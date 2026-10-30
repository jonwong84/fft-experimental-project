using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum ConditionalEventRejection { None, AlreadyExited, ArithmeticDomainExceeded, BudgetExceeded, InvalidBudget }

public sealed record ConditionalEventTrace(int Offset, ConditionalEventOperation Operation,
    ConditionalEventVariables Before, ConditionalEventVariables After, int NextOffset, bool Exited);

public sealed class ConditionalEventResult
{
    public bool Accepted => Rejection == ConditionalEventRejection.None;
    public ConditionalEventRejection Rejection { get; }
    public string? Diagnostic { get; }
    public int? FailureOffset { get; }
    public ConditionalEventState State { get; }
    public ImmutableArray<ConditionalEventTrace> Trace { get; }

    private ConditionalEventResult(ConditionalEventState state, ImmutableArray<ConditionalEventTrace> trace,
        ConditionalEventRejection rejection, string? diagnostic, int? failureOffset)
        => (State, Trace, Rejection, Diagnostic, FailureOffset) = (state, trace, rejection, diagnostic, failureOffset);

    internal static ConditionalEventResult Accept(ConditionalEventState state, ImmutableArray<ConditionalEventTrace> trace)
        => new(state, trace, ConditionalEventRejection.None, null, null);
    internal static ConditionalEventResult Reject(ConditionalEventState original, ConditionalEventRejection reason, string message, int offset)
        => new(original, [], reason, message, offset);
}

/// <summary>Pure transitions. Failed Step/Run returns the exact input snapshot and no committed trace.</summary>
public static class ConditionalEventExecutor
{
    public static ConditionalEventResult Step(ConditionalEventState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Exited)
            return ConditionalEventResult.Reject(state, ConditionalEventRejection.AlreadyExited, "Event thread has already exited.", state.Offset);
        var program = state.Program;
        int index = program.IndexOf(state.Offset);
        var instruction = program.Instructions[index];
        var variables = state.Variables;
        bool exited = instruction.Operation == ConditionalEventOperation.Exit;
        int next = exited ? state.Offset : program.Instructions[index + 1].Offset;
        switch (instruction.Operation)
        {
            case ConditionalEventOperation.ZeroVariable:
                variables = variables.Set(instruction.Operand1!.Value, 0);
                break;
            case ConditionalEventOperation.AddImmediate:
            case ConditionalEventOperation.AddVariable:
                int destination = instruction.Operand1!.Value;
                int operand = instruction.Operation == ConditionalEventOperation.AddImmediate
                    ? instruction.Operand2!.Value : variables[instruction.Operand2!.Value];
                long sum = (long)variables[destination] + operand;
                if (sum > ConditionalEventProgram.MaxVariableValue)
                    return ConditionalEventResult.Reject(state, ConditionalEventRejection.ArithmeticDomainExceeded,
                        $"Addition at {state.Offset} exceeds proof variable range 0..{ConditionalEventProgram.MaxVariableValue}.", state.Offset);
                variables = variables.Set(destination, (int)sum);
                break;
            case ConditionalEventOperation.CompareEqual:
                variables = variables with { V0 = variables.V0 == variables.V1 ? 1 : 0 };
                break;
            case ConditionalEventOperation.JumpForwardIfZero:
                if (variables.V0 == 0) next = instruction.TargetOffset!.Value;
                break;
            case ConditionalEventOperation.JumpForward:
                next = instruction.TargetOffset!.Value;
                break;
            case ConditionalEventOperation.Nop:
            case ConditionalEventOperation.Target:
            case ConditionalEventOperation.Exit:
                break;
            default:
                throw new InvalidOperationException("Program admitted an unsupported operation.");
        }
        var result = new ConditionalEventState(program, next, variables, exited);
        return ConditionalEventResult.Accept(result,
            [new(state.Offset, instruction.Operation, state.Variables, variables, next, exited)]);
    }

    /// <summary>Run atomically to explicit thread exit within a caller-supplied instruction budget, including Exit.</summary>
    public static ConditionalEventResult Run(ConditionalEventState state, int instructionBudget)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (instructionBudget is < 0 or > ConditionalEventProgram.MaxInstructions)
            return ConditionalEventResult.Reject(state, ConditionalEventRejection.InvalidBudget,
                $"Instruction budget must be in 0..{ConditionalEventProgram.MaxInstructions}.", state.Offset);
        if (state.Exited) return Step(state);
        var current = state;
        var trace = ImmutableArray.CreateBuilder<ConditionalEventTrace>();
        for (int step = 0; step < instructionBudget; step++)
        {
            var result = Step(current);
            if (!result.Accepted)
                return ConditionalEventResult.Reject(state, result.Rejection, result.Diagnostic!, result.FailureOffset!.Value);
            trace.AddRange(result.Trace);
            current = result.State;
            if (current.Exited) return ConditionalEventResult.Accept(current, trace.ToImmutable());
        }
        return ConditionalEventResult.Reject(state, ConditionalEventRejection.BudgetExceeded,
            $"Instruction budget {instructionBudget} exhausted before thread exit at {current.Offset}.", current.Offset);
    }
}
