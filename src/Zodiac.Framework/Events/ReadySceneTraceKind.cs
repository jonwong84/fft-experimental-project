using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum ReadySceneTraceKind
{
    Event, TaskLedger, EventStarted, EventAdmission, DispatcherInitialized, ChildInitialized,
    DispatcherWaitingInput, DispatcherRelease, DispatcherChildPollPending, DispatcherCleared,
    ChildAdvanced, ChildFadeOut, ChildCleared, AggregateCancelled, Observed,
}
