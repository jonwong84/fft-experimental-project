using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed class ReadySceneInstruction
{
    public int Offset => SceneInstruction.Offset;
    public SceneEventInstruction SceneInstruction { get; }
    public DisplayReady? Display { get; }
    public ReadySceneInstruction(SceneEventInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        SceneInstruction = instruction;
    }
    public ReadySceneInstruction(int offset, DisplayReady display)
    {
        ArgumentNullException.ThrowIfNull(display);
        Display = display;
        SceneInstruction = new(new ConditionalEventInstruction(offset, ConditionalEventOperation.Nop));
    }
}
