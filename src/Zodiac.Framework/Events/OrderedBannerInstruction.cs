using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed class OrderedBannerInstruction
{
    public int Offset => SceneInstruction.Offset;
    public SceneEventInstruction SceneInstruction { get; }
    public DisplayGeneralBanner? Display { get; }

    public OrderedBannerInstruction(SceneEventInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        SceneInstruction = instruction;
    }

    public OrderedBannerInstruction(int offset, DisplayGeneralBanner display)
    {
        ArgumentNullException.ThrowIfNull(display);
        Display = display;
        SceneInstruction = new(new ConditionalEventInstruction(offset, ConditionalEventOperation.Nop));
    }
}
