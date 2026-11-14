using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record DashResolvedEvent(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId, int Damage, bool KnockbackAttempted) : AttackResultEvent;
