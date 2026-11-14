using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public readonly record struct SceneTaskId(AssetId SceneRunId, long Sequence);
