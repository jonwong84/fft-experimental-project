using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record ImmediateAbilityResolvedEvent(BattleUnitId ActorId, AssetId AbilityId, int PhysicalAttackBefore, int PhysicalAttackAfter) : AttackResultEvent;
