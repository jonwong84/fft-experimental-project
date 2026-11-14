using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TurnChoiceConsumedEvent(BattleUnitId UnitId, TurnCommandKind Choice) : TurnCycleEvent;
