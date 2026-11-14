using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record DashKnockbackEvent(BattleUnitId UnitId, TilePosition Before, TilePosition After) : AttackResultEvent;
