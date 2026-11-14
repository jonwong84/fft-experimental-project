using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum DarkScreenIntroductionRejection
{
    None, InvalidInstructionBudget, InvalidUpdateBudget, InvalidManagedBinding, MissingCircleSample,
    NotWaiting, AwaitingTick, AlreadyExited, InstructionBudgetExceeded, EventRejected, TaskLedgerRejected, InvalidCompletion
}
