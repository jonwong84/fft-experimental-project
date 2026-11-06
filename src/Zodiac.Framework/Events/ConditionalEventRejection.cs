using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum ConditionalEventRejection { None, AlreadyExited, ArithmeticDomainExceeded, BudgetExceeded, InvalidBudget }
