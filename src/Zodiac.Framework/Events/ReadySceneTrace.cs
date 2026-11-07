using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record ReadySceneTrace(ReadySceneTraceKind Kind, long Tick, ReadyDisplayState? Before = null,
    ReadyDisplayState? After = null, SceneEventTrace? EventTrace = null, SceneTaskTrace? TaskTrace = null,
    bool? CirclePressed = null, ReadyCancellation? Cancellation = null, int? SourceOffset = null,
    int? InstructionBudget = null, int? UpdateBudget = null, SceneEventState? StartedEvent = null);
