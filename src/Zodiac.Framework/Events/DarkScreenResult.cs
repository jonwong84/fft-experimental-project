using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum DarkScreenRejection
{
    None, InvalidManagedBinding, WorkerAlreadyAttached, ManagedTaskUpdateForbidden,
    EventRejected, TaskLedgerRejected,
}
public enum DarkScreenTraceKind { Event, TaskLedger, WorkerStarted, ReleaseMatched, ReleaseNoMatch, WorkerAdvanced, WorkerObserved }
public sealed record DarkScreenTrace(DarkScreenTraceKind Kind, long Tick, int InstructionBudget, int UpdateBudget,
    int? Offset = null, DarkScreenState? Before = null, DarkScreenState? After = null,
    SceneEventTrace? EventTrace = null, SceneTaskTrace? TaskTrace = null);

/// <summary>A rejected transaction returns its exact input object and no accepted trace/counters.</summary>
public sealed class DarkScreenResult
{
    public DarkScreenSceneState State { get; }
    public ImmutableArray<DarkScreenTrace> Trace { get; }
    public DarkScreenRejection Rejection { get; }
    public SceneEventRejection EventRejection { get; }
    public SceneTaskRejection TaskRejection { get; }
    public string? Diagnostic { get; }
    public int? FailureOffset { get; }
    public int? FailureUpdateIndex { get; }
    public int InstructionsExecuted { get; }
    public int AcceptedTaskUpdates { get; }
    public int AcceptedWorkerTicks { get; }
    public int TicksConsumed { get; }
    public bool Accepted => Rejection == DarkScreenRejection.None;

    private DarkScreenResult(DarkScreenSceneState state, ImmutableArray<DarkScreenTrace> trace,
        DarkScreenRejection rejection, SceneEventRejection eventRejection, SceneTaskRejection taskRejection,
        string? diagnostic, int? offset, int? index, int instructions, int updates, int workerTicks, int ticks)
        => (State, Trace, Rejection, EventRejection, TaskRejection, Diagnostic, FailureOffset, FailureUpdateIndex,
            InstructionsExecuted, AcceptedTaskUpdates, AcceptedWorkerTicks, TicksConsumed) =
            (state, trace, rejection, eventRejection, taskRejection, diagnostic, offset, index, instructions, updates, workerTicks, ticks);

    internal static DarkScreenResult Accept(DarkScreenSceneState state, ImmutableArray<DarkScreenTrace> trace,
        int instructions = 0, int updates = 0, int workerTicks = 0, int ticks = 0)
        => new(state, trace, DarkScreenRejection.None, SceneEventRejection.None, SceneTaskRejection.None,
            null, null, null, instructions, updates, workerTicks, ticks);

    internal static DarkScreenResult Reject(DarkScreenSceneState state, DarkScreenRejection reason, string diagnostic,
        SceneEventRejection eventReason = SceneEventRejection.None, SceneTaskRejection taskReason = SceneTaskRejection.None,
        int? offset = null, int? index = null)
        => new(state, [], reason, eventReason, taskReason, diagnostic, offset, index, 0, 0, 0, 0);
}
