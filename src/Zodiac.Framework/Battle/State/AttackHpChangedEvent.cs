using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record AttackHpChangedEvent(BattleUnitId UnitId, int Before, int After) : AttackResultEvent;
