using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record FacingSettledEvent(BattleUnitId ActorId, BattleFacing Before, BattleFacing After) : AttackResultEvent;
