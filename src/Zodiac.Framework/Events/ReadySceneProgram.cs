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

/// <summary>Typed condition-0 domain with fixed event/dispatcher/child order; no raw opcodes or native slots.</summary>
public sealed class ReadySceneProgram
{
    public AssetId Id => Body.Id;
    public ImmutableArray<ReadySceneInstruction> Instructions { get; }
    internal SceneEventProgram Body { get; }
    internal DisplayReady? Display { get; }
    internal int? DisplayOffset { get; }
    public ReadySceneProgram(AssetId id, IEnumerable<ReadySceneInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        Instructions = instructions.Take(ConditionalEventProgram.MaxInstructions + 1).ToImmutableArray();
        if (Instructions.Any(i => i is null)) throw new ArgumentException("Instructions must be nonnull.", nameof(instructions));
        var displays = Instructions.Where(i => i.Display is not null).ToArray();
        if (displays.Length > 1) throw new ArgumentException("Only one Ready admission is selected.", nameof(instructions));
        if (displays.Length == 1)
        {
            Display = displays[0].Display!; DisplayOffset = displays[0].Offset;
            if (Display.Definition is not { } d || !SceneTaskState.ValidId(d.Id) || !SceneTaskState.ValidId(Display.ArtworkId) ||
                !SceneTaskState.ValidId(Display.OwnerId) || d.Condition != 0 || d.HoldFrames is < 0 or > 255)
                throw new ArgumentException("Ready requires logical IDs, condition 0 and hold 0..255.", nameof(instructions));
        }
        Body = new(id, Instructions.Select(i => i.SceneInstruction));
    }
    public ReadySceneState Start(ConditionalEventVariables variables, SceneTaskState tasks, AssetId consumerId, PreloadedBannerBinding? binding)
        => new(this, Body.Start(variables, tasks, consumerId), null, binding);
}

public sealed class ReadySceneState
{
    public ReadySceneProgram Program { get; }
    public SceneEventState Event { get; }
    public ReadyDisplayState? Ready { get; }
    public PreloadedBannerBinding? Binding { get; }
    internal ReadySceneState(ReadySceneProgram program, SceneEventState sceneEvent, ReadyDisplayState? ready, PreloadedBannerBinding? binding)
        => (Program, Event, Ready, Binding) = (program, sceneEvent, ready, binding);
    internal ReadySceneState With(SceneEventState sceneEvent, ReadyDisplayState? ready) => new(Program, sceneEvent, ready, Binding);
}
