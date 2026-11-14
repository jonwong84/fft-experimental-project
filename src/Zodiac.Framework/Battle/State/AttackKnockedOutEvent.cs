using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record AttackKnockedOutEvent(BattleUnitId UnitId, int KoCount) : AttackResultEvent;
