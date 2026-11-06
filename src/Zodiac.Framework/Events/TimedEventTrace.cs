using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record TimedEventTrace(TimedEventTraceKind Kind, int Offset, ConditionalEventOperation? Operation,
    ConditionalEventVariables Before, ConditionalEventVariables After, int NextOffset,
    int RemainingWaitTicks, long Tick, bool Exited);
