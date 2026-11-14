using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum SceneEventRejection
{
    None, InvalidInstructionBudget, AlreadyExited, AwaitingTick, NotWaiting,
    InstructionBudgetExceeded, ArithmeticDomainExceeded, TaskLedgerRejected,
}
