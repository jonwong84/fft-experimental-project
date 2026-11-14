using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record AttackResolvedEvent(BattleUnitId ActorId, BattleUnitId TargetId, bool Hit, bool Critical, int Damage) : AttackResultEvent;
