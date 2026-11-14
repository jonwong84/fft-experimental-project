using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>A typed admission/release or existing authored scene instruction; never original bytecode.</summary>
public sealed class DarkScreenInstruction
{
    public int Offset => SceneInstruction.Offset;
    public SceneEventInstruction SceneInstruction { get; }
    public StartDarkScreen? Start { get; }
    public bool Release { get; }

    public DarkScreenInstruction(SceneEventInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        SceneInstruction = instruction;
    }

    public DarkScreenInstruction(int offset, StartDarkScreen start)
    {
        ArgumentNullException.ThrowIfNull(start);
        Start = start;
        SceneInstruction = new(new ConditionalEventInstruction(offset, ConditionalEventOperation.Nop));
    }

    /// <summary>A no-match release is an accepted no-op, including before admission and after completion.</summary>
    public DarkScreenInstruction(int releaseOffset)
    {
        Release = true;
        SceneInstruction = new(new ConditionalEventInstruction(releaseOffset, ConditionalEventOperation.Nop));
    }
}
