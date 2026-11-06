using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TurnEndedEvent(BattleUnitId UnitId, int CtBeforeCompletion, int CtAfterCompletion) : TurnCycleEvent;
