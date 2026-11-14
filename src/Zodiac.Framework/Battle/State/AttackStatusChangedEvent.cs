using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record AttackStatusChangedEvent(BattleUnitId UnitId, AttackUnitStatus Before, AttackUnitStatus After) : AttackResultEvent;
