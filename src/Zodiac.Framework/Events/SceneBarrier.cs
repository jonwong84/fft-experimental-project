using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

public sealed record SceneBarrier(SceneBarrierStatus Status, SceneTaskClass Selector,
    AssetId ConsumerId, int SourceOffset, AssetId ContinuationId, long EntryTick);
