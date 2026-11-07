using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record DarkScreenTrace(DarkScreenTraceKind Kind, long Tick, int InstructionBudget, int UpdateBudget,
    int? Offset = null, DarkScreenState? Before = null, DarkScreenState? After = null,
    SceneEventTrace? EventTrace = null, SceneTaskTrace? TaskTrace = null);
