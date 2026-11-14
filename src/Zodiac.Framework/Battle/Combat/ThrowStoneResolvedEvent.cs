using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record ThrowStoneResolvedEvent(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId,
    bool Hit, int Damage, bool KnockbackAttempted) : AttackResultEvent;
