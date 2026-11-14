using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Explicit logical facts, not callbacks. Identical starts intentionally allocate distinct identities.</summary>
public abstract record SceneTaskUpdate
{
    private SceneTaskUpdate() { }
    public sealed record Start(SceneTaskFamily Family, AssetId OwnerId) : SceneTaskUpdate;
    public sealed record Phase(SceneTaskId TaskId, long ExpectedRevision, SceneTaskPhase Target) : SceneTaskUpdate;
    public sealed record Complete(SceneTaskId TaskId, long ExpectedRevision) : SceneTaskUpdate;
    public sealed record Cancel(SceneTaskId TaskId, long ExpectedRevision, SceneTaskCancellationReason Reason) : SceneTaskUpdate;
}
