using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public enum DarkScreenTraceKind { Event, TaskLedger, WorkerStarted, ReleaseMatched, ReleaseNoMatch, WorkerAdvanced, WorkerObserved }
