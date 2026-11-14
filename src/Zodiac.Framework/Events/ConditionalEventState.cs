namespace Zodiac.Framework.Events;

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
