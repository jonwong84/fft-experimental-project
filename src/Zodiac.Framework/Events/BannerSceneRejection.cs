using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum BannerSceneRejection
{
    None, InvalidId, InvalidDefinition, UnsupportedCondition, InvalidHoldFrames, EffectAlreadyAttached,
    EffectNotAttached, MissingCircleSample, InvalidManagedTaskBinding, ManagedTaskUpdateForbidden,
    InvalidInstructionBudget, AlreadyExited, AwaitingTick, NotWaiting, InstructionBudgetExceeded,
    ArithmeticDomainExceeded, TaskLedgerRejected, EventNotExited, InvalidProgram, InvalidVariables,
}
