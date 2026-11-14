using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>One authored straight-line event: general display, E5, Ready display, E5, synthetic exit.</summary>
public sealed class BannerReadyProgram
{
    public AssetId Id { get; }
    public DisplayGeneralBanner General { get; }
    public DisplayReady Ready { get; }
    internal SceneEventProgram Body { get; }
    public BannerReadyProgram(AssetId id, DisplayGeneralBanner general, DisplayReady ready)
    {
        ArgumentNullException.ThrowIfNull(general); ArgumentNullException.ThrowIfNull(ready);
        if (general.Definition is null || ready.Definition is null || general.Definition.Condition != 8 || ready.Definition.Condition != 0 ||
            general.Definition.HoldFrames is < 0 or > 255 || ready.Definition.HoldFrames is < 0 or > 255 ||
            !SceneTaskState.ValidId(general.Definition.Id) || !SceneTaskState.ValidId(ready.Definition.Id) ||
            !SceneTaskState.ValidId(general.ArtworkId) || !SceneTaskState.ValidId(ready.ArtworkId) ||
            !SceneTaskState.ValidId(general.OwnerId) || general.OwnerId != ready.OwnerId ||
            general.Definition.Id == ready.Definition.Id || general.ArtworkId == ready.ArtworkId)
            throw new ArgumentException("Distinct condition-8/0 definitions/artwork, unsigned holds and one logical owner are required.");
        Id = id; General = general; Ready = ready;
        Body = new(id, [new(new ConditionalEventInstruction(0, ConditionalEventOperation.Nop)), new(1, SceneTaskClass.ConditionDisplay),
            new(new ConditionalEventInstruction(2, ConditionalEventOperation.Nop)), new(3, SceneTaskClass.ConditionDisplay),
            new(new ConditionalEventInstruction(4, ConditionalEventOperation.Exit))]);
    }
    public BannerReadyState Start(AssetId sceneRun, AssetId consumer, BannerReadyBinding? binding)
        => new(this, Body.Start(default, SceneTaskState.Create(sceneRun), consumer), null, null, binding);
}
