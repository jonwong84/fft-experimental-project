namespace Fft.Core.Events;

/// <summary>Exactly four proof variables. Admission and every write are checked against the proof domain.</summary>
public readonly record struct ConditionalEventVariables(int V0, int V1, int V2, int V3)
{
    internal bool InDomain => V0 is >= 0 and <= ConditionalEventProgram.MaxVariableValue &&
        V1 is >= 0 and <= ConditionalEventProgram.MaxVariableValue &&
        V2 is >= 0 and <= ConditionalEventProgram.MaxVariableValue &&
        V3 is >= 0 and <= ConditionalEventProgram.MaxVariableValue;

    public int this[int index] => index switch
    {
        0 => V0, 1 => V1, 2 => V2, 3 => V3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    internal ConditionalEventVariables Set(int index, int value) => index switch
    {
        0 => this with { V0 = value }, 1 => this with { V1 = value },
        2 => this with { V2 = value }, 3 => this with { V3 = value },
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
}

/// <summary>Immutable instruction-boundary snapshot bound to its immutable program.</summary>
public sealed class ConditionalEventState
{
    public ConditionalEventProgram Program { get; }
    public int Offset { get; }
    public ConditionalEventVariables Variables { get; }
    public bool Exited { get; }

    internal ConditionalEventState(ConditionalEventProgram program, int offset, ConditionalEventVariables variables, bool exited)
        => (Program, Offset, Variables, Exited) = (program, offset, variables, exited);
}
