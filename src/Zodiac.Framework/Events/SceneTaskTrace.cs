using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Committed facts in command order. Task changes include before/after; barrier facts retain continuation metadata.</summary>
public sealed record SceneTaskTrace(SceneTaskTraceKind Kind, long Tick,
    SceneTask? Before, SceneTask? After, SceneBarrier? Barrier, ImmutableArray<SceneTaskId> MatchingTasks);
