using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum ReadySceneTraceKind
{
    Event, TaskLedger, EventStarted, EventAdmission, DispatcherInitialized, ChildInitialized,
    DispatcherWaitingInput, DispatcherRelease, DispatcherChildPollPending, DispatcherCleared,
    ChildAdvanced, ChildFadeOut, ChildCleared, AggregateCancelled, Observed,
}
public sealed record ReadySceneTrace(ReadySceneTraceKind Kind, long Tick, ReadyDisplayState? Before = null,
    ReadyDisplayState? After = null, SceneEventTrace? EventTrace = null, SceneTaskTrace? TaskTrace = null,
    bool? CirclePressed = null, ReadyCancellation? Cancellation = null, int? SourceOffset = null,
    int? InstructionBudget = null, int? UpdateBudget = null, SceneEventState? StartedEvent = null);

public sealed class ReadySceneResult
{
    public ReadySceneState State { get; }
    public ImmutableArray<ReadySceneTrace> Trace { get; }
    public BannerSceneRejection Rejection { get; }
    public SceneTaskRejection TaskRejection { get; }
    public string? Diagnostic { get; }
    public int? FailureOffset { get; }
    public int? FailureUpdateIndex { get; }
    public int InstructionsExecuted { get; }
    public int AcceptedTaskUpdates { get; }
    public int AcceptedEffectTicks { get; }
    public int TicksConsumed { get; }
    public bool Accepted => Rejection == BannerSceneRejection.None;
    private ReadySceneResult(ReadySceneState state, ImmutableArray<ReadySceneTrace> trace, BannerSceneRejection rejection,
        SceneTaskRejection taskRejection, string? diagnostic, int? offset, int? index, int instructions, int updates, int effectTicks, int ticks)
        => (State, Trace, Rejection, TaskRejection, Diagnostic, FailureOffset, FailureUpdateIndex, InstructionsExecuted,
            AcceptedTaskUpdates, AcceptedEffectTicks, TicksConsumed) =
            (state, trace, rejection, taskRejection, diagnostic, offset, index, instructions, updates, effectTicks, ticks);
    internal static ReadySceneResult Accept(ReadySceneState state, ImmutableArray<ReadySceneTrace> trace,
        int instructions = 0, int updates = 0, int effectTicks = 0, int ticks = 0)
        => new(state, trace, BannerSceneRejection.None, SceneTaskRejection.None, null, null, null, instructions, updates, effectTicks, ticks);
    internal static ReadySceneResult Reject(ReadySceneState state, BannerSceneRejection reason, string diagnostic,
        SceneTaskRejection taskReason = SceneTaskRejection.None, int? index = null, int? offset = null)
        => new(state, [], reason, taskReason, diagnostic, offset, index, 0, 0, 0, 0);
}
