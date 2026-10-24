using System.Collections.Immutable;

namespace Fft.Core.Events;

public enum SceneTaskFamily { Camera, DarkScreen, ConditionDisplay }
public enum SceneTaskPhase { Running, Entering, Holding, Leaving, Completed, Cancelled }
public enum SceneTaskClass { Camera = 4, DarkScreenTransition = 54, DarkScreenHold = 55, ConditionDisplay = 56 }
public enum SceneTaskCancellationReason { OwnerAcknowledgedCancellation }
public enum SceneBarrierStatus { Ready, Pending, Released }

public readonly record struct SceneTaskId(AssetId SceneRunId, long Sequence);

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

public sealed record SceneBarrier(SceneBarrierStatus Status, SceneTaskClass Selector,
    AssetId ConsumerId, int SourceOffset, AssetId ContinuationId, long EntryTick);

/// <summary>Immutable BATTLE proof state, independent of any interpreter or worker lifetime.</summary>
public sealed class SceneTaskState
{
    public const int MaxLiveTasks = 64;
    public const int MaxTotalStarted = 256;
    public const int MaxUpdatesPerCommand = 64;
    public const int MaxIdCharacters = 256;

    public AssetId SceneRunId { get; }
    public long Tick { get; }
    public long NextSequence { get; }
    public ImmutableArray<SceneTask> Tasks { get; }
    public int TotalStarted => Tasks.Length;
    public int LiveTasks => Tasks.Count(task => !task.IsTerminal);
    public SceneBarrier? Barrier { get; }
    public SceneBarrierStatus BarrierStatus => Barrier?.Status ?? SceneBarrierStatus.Ready;

    internal SceneTaskState(AssetId sceneRunId, long tick, long nextSequence,
        ImmutableArray<SceneTask> tasks, SceneBarrier? barrier)
        => (SceneRunId, Tick, NextSequence, Tasks, Barrier) = (sceneRunId, tick, nextSequence, tasks, barrier);

    /// <summary>The caller supplies a fresh run ID for a live scene, or its recorded ID for an isolated replay.</summary>
    public static SceneTaskState Create(AssetId sceneRunId)
    {
        if (!ValidId(sceneRunId)) throw new ArgumentException("A nondefault namespaced scene ID of at most 256 characters is required.", nameof(sceneRunId));
        return new(sceneRunId, 0, 1, [], null);
    }

    internal static bool ValidId(AssetId id) => id.Value is { Length: > 0 and <= MaxIdCharacters };
}

/// <summary>Explicit logical facts, not callbacks. Identical starts intentionally allocate distinct identities.</summary>
public abstract record SceneTaskUpdate
{
    private SceneTaskUpdate() { }
    public sealed record Start(SceneTaskFamily Family, AssetId OwnerId) : SceneTaskUpdate;
    public sealed record Phase(SceneTaskId TaskId, long ExpectedRevision, SceneTaskPhase Target) : SceneTaskUpdate;
    public sealed record Complete(SceneTaskId TaskId, long ExpectedRevision) : SceneTaskUpdate;
    public sealed record Cancel(SceneTaskId TaskId, long ExpectedRevision, SceneTaskCancellationReason Reason) : SceneTaskUpdate;
}
