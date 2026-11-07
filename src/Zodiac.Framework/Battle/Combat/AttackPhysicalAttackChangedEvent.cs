using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record AttackPhysicalAttackChangedEvent(BattleUnitId UnitId, int Before, int After) : AttackResultEvent;
