using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum SceneEventRejection
{
    None, InvalidInstructionBudget, AlreadyExited, AwaitingTick, NotWaiting,
    InstructionBudgetExceeded, ArithmeticDomainExceeded, TaskLedgerRejected,
}

public enum SceneEventTraceKind
{
    Instruction, ZeroWait, FrameWaitEntered, FrameWaitTick, FrameWaitResumed,
    BarrierWaitEntered, BarrierWaitResumed, TaskLedger,
}

public sealed record SceneEventTrace(SceneEventTraceKind Kind, long Tick, int Offset,
    ConditionalEventVariables Before, ConditionalEventVariables After, int NextOffset,
    int RemainingWaitTicks, bool Exited, ConditionalEventOperation? Operation = null, SceneTaskTrace? TaskTrace = null);

public sealed class SceneEventResult
{
    public SceneEventState State { get; }
    public ImmutableArray<SceneEventTrace> Trace { get; }
    public SceneEventRejection Rejection { get; }
    public SceneTaskRejection TaskRejection { get; }
    public string? Diagnostic { get; }
    public int? FailureOffset { get; }
    public int? FailureUpdateIndex { get; }
    public int InstructionsExecuted { get; }
    public int AcceptedTaskUpdates { get; }
    public int TicksConsumed { get; }
    public bool Accepted => Rejection == SceneEventRejection.None;

    private SceneEventResult(SceneEventState state, ImmutableArray<SceneEventTrace> trace,
        SceneEventRejection rejection, SceneTaskRejection taskRejection, string? diagnostic, int? offset, int? updateIndex,
        int instructions, int updates, int ticks)
        => (State, Trace, Rejection, TaskRejection, Diagnostic, FailureOffset, FailureUpdateIndex, InstructionsExecuted, AcceptedTaskUpdates, TicksConsumed) =
            (state, trace, rejection, taskRejection, diagnostic, offset, updateIndex, instructions, updates, ticks);

    internal static SceneEventResult Accept(SceneEventState state, ImmutableArray<SceneEventTrace> trace, int instructions, int updates, int ticks)
        => new(state, trace, SceneEventRejection.None, SceneTaskRejection.None, null, null, null, instructions, updates, ticks);
    internal static SceneEventResult Reject(SceneEventState state, SceneEventRejection reason, string diagnostic, int offset,
        SceneTaskRejection taskReason = SceneTaskRejection.None, int? updateIndex = null)
        => new(state, [], reason, taskReason, diagnostic, offset, updateIndex, 0, 0, 0);
}
