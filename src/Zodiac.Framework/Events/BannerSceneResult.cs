using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum BannerSceneRejection
{
    None, InvalidId, InvalidDefinition, UnsupportedCondition, InvalidHoldFrames, EffectAlreadyAttached,
    EffectNotAttached, MissingCircleSample, InvalidManagedTaskBinding, ManagedTaskUpdateForbidden,
    InvalidInstructionBudget, AlreadyExited, AwaitingTick, NotWaiting, InstructionBudgetExceeded,
    ArithmeticDomainExceeded, TaskLedgerRejected, EventNotExited, InvalidProgram, InvalidVariables,
}

public enum BannerSceneTraceKind { Event, TaskLedger, EffectAttached, EffectAdvanced, EffectObserved, EventStarted }

/// <summary>Ordered event/task facts plus explicit effect inputs and before/after snapshots. Terminal observations never advance the effect.</summary>
public sealed record BannerSceneTrace(BannerSceneTraceKind Kind, long Tick,
    SceneEventTrace? EventTrace = null, SceneTaskTrace? TaskTrace = null,
    GeneralBannerState? Before = null, GeneralBannerState? After = null, bool? CirclePressed = null,
    BannerCancellation? Cancellation = null, int? InstructionBudget = null, int? UpdateBudget = null,
    SceneEventState? StartedEvent = null);

public sealed class BannerSceneResult
{
    public BannerSceneState State { get; }
    public ImmutableArray<BannerSceneTrace> Trace { get; }
    public BannerSceneRejection Rejection { get; }
    public SceneTaskRejection TaskRejection { get; }
    public string? Diagnostic { get; }
    public int? FailureOffset { get; }
    public int? FailureUpdateIndex { get; }
    public int AcceptedEffectTicks { get; }
    public int AcceptedTaskUpdates { get; }
    public int InstructionsExecuted { get; }
    public int TicksConsumed { get; }
    public bool Accepted => Rejection == BannerSceneRejection.None;

    private BannerSceneResult(BannerSceneState state, ImmutableArray<BannerSceneTrace> trace,
        BannerSceneRejection rejection, SceneTaskRejection taskRejection, string? diagnostic, int? offset, int? updateIndex,
        int effectTicks, int updates, int instructions, int ticks)
        => (State, Trace, Rejection, TaskRejection, Diagnostic, FailureOffset, FailureUpdateIndex, AcceptedEffectTicks, AcceptedTaskUpdates, InstructionsExecuted, TicksConsumed) =
            (state, trace, rejection, taskRejection, diagnostic, offset, updateIndex, effectTicks, updates, instructions, ticks);

    internal static BannerSceneResult Accept(BannerSceneState state, ImmutableArray<BannerSceneTrace> trace,
        int effectTicks = 0, int updates = 0, int instructions = 0, int ticks = 0)
        => new(state, trace, BannerSceneRejection.None, SceneTaskRejection.None, null, null, null, effectTicks, updates, instructions, ticks);
    internal static BannerSceneResult Reject(BannerSceneState state, BannerSceneRejection reason, string diagnostic,
        SceneTaskRejection taskReason = SceneTaskRejection.None, int? updateIndex = null, int? offset = null)
        => new(state, [], reason, taskReason, diagnostic, offset, updateIndex, 0, 0, 0, 0);
}
