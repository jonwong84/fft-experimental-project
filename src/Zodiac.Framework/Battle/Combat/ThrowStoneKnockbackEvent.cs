using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record ThrowStoneKnockbackEvent(BattleUnitId UnitId, TilePosition Before, TilePosition After) : AttackResultEvent;
