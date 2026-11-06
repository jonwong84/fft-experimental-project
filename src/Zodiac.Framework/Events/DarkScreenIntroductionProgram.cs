using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Nine authored operations; no original event import, external tasks, restart or standalone dark hold timer.</summary>
public sealed class DarkScreenIntroductionProgram
{
    public AssetId Id => Body.Id;
    public StartDarkScreen Dark { get; }
    public DisplayGeneralBanner General { get; }
    public DisplayReady Ready { get; }
    internal SceneEventProgram Body { get; }

    public DarkScreenIntroductionProgram(AssetId id, StartDarkScreen dark, DisplayGeneralBanner general, DisplayReady ready)
    {
        ArgumentNullException.ThrowIfNull(dark);
        ArgumentNullException.ThrowIfNull(general);
        ArgumentNullException.ThrowIfNull(ready);
        if (dark.Definition is null || general.Definition is null || ready.Definition is null ||
            general.Definition.Condition != 8 || ready.Definition.Condition != 0 ||
            general.Definition.HoldFrames is < 0 or > 255 || ready.Definition.HoldFrames is < 0 or > 255 ||
            dark.OwnerId != general.OwnerId || dark.OwnerId != ready.OwnerId)
            throw new ArgumentException("Selected definitions, unsigned holds and one logical owner are required.");
        AssetId[] ids = [id, dark.Definition.Id, general.Definition.Id, ready.Definition.Id,
            general.ArtworkId, ready.ArtworkId, dark.OwnerId];
        if (ids.Any(value => !SceneTaskState.ValidId(value)) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Distinct bounded program, definition, artwork and owner IDs are required.");
        (Dark, General, Ready) = (dark, general, ready);
        Body = new(id, [Op(0), new(1, SceneTaskClass.DarkScreenTransition), Op(2),
            new(3, SceneTaskClass.ConditionDisplay), Op(4), new(5, SceneTaskClass.ConditionDisplay),
            Op(6), new(7, SceneTaskClass.DarkScreenTransition),
            new(new ConditionalEventInstruction(8, ConditionalEventOperation.Exit))]);
    }

    private static SceneEventInstruction Op(int offset) => new(new ConditionalEventInstruction(offset, ConditionalEventOperation.Nop));
    public DarkScreenIntroductionState Start(AssetId sceneRun, AssetId consumer, DarkScreenIntroductionBinding? binding)
        => new(this, Body.Start(default, SceneTaskState.Create(sceneRun), consumer), null, null, null, binding);
}
