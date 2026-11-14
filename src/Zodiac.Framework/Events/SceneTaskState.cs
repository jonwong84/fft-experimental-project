using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

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
