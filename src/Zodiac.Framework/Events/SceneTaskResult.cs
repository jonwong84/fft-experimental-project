using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum SceneTaskRejection
{
    None, InvalidUpdateBatch, InvalidUpdateBudget, UpdateBudgetExceeded, InvalidUpdate,
    UnsupportedTaskFamily, UnsupportedTaskClass, InvalidId, InvalidSourceOffset,
    BarrierAlreadyPending, AwaitingTick, WrongScene, UnknownTask, DuplicateTerminalUpdate,
    StaleTaskRevision, IllegalTaskTransition, LiveTaskLimitExceeded, TotalTaskLimitExceeded,
    CounterOverflow,
}

public enum SceneTaskTraceKind
{
    TaskStarted, TaskPhaseChanged, TaskCompleted, TaskCancelled,
    BarrierEntered, TickAdvanced, BarrierPolled, BarrierReleased,
}

/// <summary>Committed facts in command order. Task changes include before/after; barrier facts retain continuation metadata.</summary>
public sealed record SceneTaskTrace(SceneTaskTraceKind Kind, long Tick,
    SceneTask? Before, SceneTask? After, SceneBarrier? Barrier, ImmutableArray<SceneTaskId> MatchingTasks);

public sealed class SceneTaskResult
{
    public SceneTaskState State { get; }
    public ImmutableArray<SceneTaskTrace> Trace { get; }
    public SceneTaskRejection Rejection { get; }
    public string? Diagnostic { get; }
    public int? FailureUpdateIndex { get; }
    public int AcceptedUpdates { get; }
    public int TicksConsumed { get; }
    public bool Accepted => Rejection == SceneTaskRejection.None;

    private SceneTaskResult(SceneTaskState state, ImmutableArray<SceneTaskTrace> trace, SceneTaskRejection rejection,
        string? diagnostic, int? failureUpdateIndex, int acceptedUpdates, int ticksConsumed)
        => (State, Trace, Rejection, Diagnostic, FailureUpdateIndex, AcceptedUpdates, TicksConsumed) =
            (state, trace, rejection, diagnostic, failureUpdateIndex, acceptedUpdates, ticksConsumed);

    internal static SceneTaskResult Accept(SceneTaskState state, ImmutableArray<SceneTaskTrace> trace, int updates, int ticks)
        => new(state, trace, SceneTaskRejection.None, null, null, updates, ticks);

    internal static SceneTaskResult Reject(SceneTaskState state, SceneTaskRejection reason, string diagnostic, int? index = null)
        => new(state, [], reason, diagnostic, index, 0, 0);
}
