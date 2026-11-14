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
