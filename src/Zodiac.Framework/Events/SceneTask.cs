using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>One scene-owned instance. Terminal records remain as tombstones until the scene is discarded.</summary>
public sealed record SceneTask(SceneTaskId Id, AssetId OwnerId, SceneTaskFamily Family,
    SceneTaskPhase Phase, long Revision, SceneTaskCancellationReason? CancellationReason = null)
{
    public bool IsTerminal => Phase is SceneTaskPhase.Completed or SceneTaskPhase.Cancelled;
    public SceneTaskClass? ActiveClass => Phase switch
    {
        SceneTaskPhase.Running when Family == SceneTaskFamily.Camera => SceneTaskClass.Camera,
        SceneTaskPhase.Running when Family == SceneTaskFamily.ConditionDisplay => SceneTaskClass.ConditionDisplay,
        SceneTaskPhase.Entering or SceneTaskPhase.Leaving when Family == SceneTaskFamily.DarkScreen => SceneTaskClass.DarkScreenTransition,
        SceneTaskPhase.Holding when Family == SceneTaskFamily.DarkScreen => SceneTaskClass.DarkScreenHold,
        _ => null,
    };
}
