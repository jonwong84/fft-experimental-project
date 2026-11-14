using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record SceneEventTrace(SceneEventTraceKind Kind, long Tick, int Offset,
    ConditionalEventVariables Before, ConditionalEventVariables After, int NextOffset,
    int RemainingWaitTicks, bool Exited, ConditionalEventOperation? Operation = null, SceneTaskTrace? TaskTrace = null);
