using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>A trusted composition-root assertion. It carries logical identities, never artwork or I/O.</summary>
public sealed record PreloadedBannerBinding(AssetId DefinitionId, AssetId ArtworkId, AssetId OwnerId);

/// <summary>Typed admission, independent of raw opcodes and prepared-content formats.</summary>
public sealed record DisplayGeneralBanner(GeneralBannerDefinition Definition, AssetId ArtworkId, AssetId OwnerId);

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

/// <summary>Separate authored event-before-worker domain; existing scene programs cannot admit effects.</summary>
public sealed class OrderedBannerProgram
{
    public AssetId Id => Body.Id;
    public ImmutableArray<OrderedBannerInstruction> Instructions { get; }
    internal SceneEventProgram Body { get; }
    internal DisplayGeneralBanner? Display { get; }
    internal int? DisplayOffset { get; }

    public OrderedBannerProgram(AssetId id, IEnumerable<OrderedBannerInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        Instructions = instructions.Take(ConditionalEventProgram.MaxInstructions + 1).ToImmutableArray();
        if (Instructions.Any(i => i is null)) throw new ArgumentException("Instructions must be nonnull.", nameof(instructions));
        var displays = Instructions.Where(i => i.Display is not null).ToArray();
        if (displays.Length > 1) throw new ArgumentException("Only one syntactic banner admission is supported.", nameof(instructions));
        if (displays.Length == 1)
        {
            Display = displays[0].Display!;
            DisplayOffset = displays[0].Offset;
            if (Display.Definition is not { } definition || !SceneTaskState.ValidId(definition.Id) ||
                !SceneTaskState.ValidId(Display.ArtworkId) || !SceneTaskState.ValidId(Display.OwnerId) ||
                definition.Condition != GeneralBannerDefinition.SupportedCondition ||
                definition.HoldFrames is < 0 or > GeneralBannerDefinition.MaxHoldFrames)
                throw new ArgumentException("Admission requires logical definition/artwork/owner IDs, condition 8 and hold 0..255.", nameof(instructions));
        }
        Body = new(id, Instructions.Select(i => i.SceneInstruction));
    }

    public OrderedBannerState Start(ConditionalEventVariables variables, SceneTaskState tasks, AssetId consumerId,
        PreloadedBannerBinding? readyBinding)
        => new(this, Body.Start(variables, tasks, consumerId), null, readyBinding);
}

/// <summary>One scene retains its banner and binding across consumer lifetimes, including terminal tombstones.</summary>
public sealed class OrderedBannerState
{
    public OrderedBannerProgram Program { get; }
    public SceneEventState Event { get; }
    public GeneralBannerState? Banner { get; }
    public PreloadedBannerBinding? ReadyBinding { get; }
    internal BannerSceneState Scene => new(Event, Banner);

    internal OrderedBannerState(OrderedBannerProgram program, SceneEventState sceneEvent,
        GeneralBannerState? banner, PreloadedBannerBinding? readyBinding)
        => (Program, Event, Banner, ReadyBinding) = (program, sceneEvent, banner, readyBinding);

    internal OrderedBannerState With(SceneEventState sceneEvent, GeneralBannerState? banner)
        => new(Program, sceneEvent, banner, ReadyBinding);
}
