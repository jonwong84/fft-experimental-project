using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TurnReadyEvent(BattleUnitId UnitId, int CtBeforeSelection, int CtAfterSelection, uint UpkeepRoll) : TurnCycleEvent;
