using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum TimedEventRejection
{
    None, AlreadyExited, AwaitingTick, NotWaiting, ArithmeticDomainExceeded,
    InstructionBudgetExceeded, InvalidInstructionBudget, InvalidTickCount, UnusedTicks,
}
