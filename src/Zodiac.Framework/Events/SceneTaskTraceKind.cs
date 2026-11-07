using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum SceneTaskTraceKind
{
    TaskStarted, TaskPhaseChanged, TaskCompleted, TaskCancelled,
    BarrierEntered, TickAdvanced, BarrierPolled, BarrierReleased,
}
