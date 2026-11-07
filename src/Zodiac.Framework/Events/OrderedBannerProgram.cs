using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

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
