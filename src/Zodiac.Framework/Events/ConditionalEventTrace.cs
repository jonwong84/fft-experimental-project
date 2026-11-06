using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record ConditionalEventTrace(int Offset, ConditionalEventOperation Operation,
    ConditionalEventVariables Before, ConditionalEventVariables After, int NextOffset, bool Exited);
