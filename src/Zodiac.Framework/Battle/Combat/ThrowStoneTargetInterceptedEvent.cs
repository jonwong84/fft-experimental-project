using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record ThrowStoneTargetInterceptedEvent(BattleUnitId ActorId, AssetId AbilityId,
    BattleUnitId IntendedTargetId, BattleUnitId ActualTargetId) : AttackResultEvent;
