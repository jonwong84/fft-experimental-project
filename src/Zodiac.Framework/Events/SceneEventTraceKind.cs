using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum SceneEventTraceKind
{
    Instruction, ZeroWait, FrameWaitEntered, FrameWaitTick, FrameWaitResumed,
    BarrierWaitEntered, BarrierWaitResumed, TaskLedger,
}
