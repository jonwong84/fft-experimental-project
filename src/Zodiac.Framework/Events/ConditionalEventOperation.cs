using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum ConditionalEventOperation
{
    Nop, ZeroVariable, AddImmediate, AddVariable, CompareEqual,
    JumpForwardIfZero, JumpForward, Target, Exit,
}
