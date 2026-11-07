using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TurnBattleClockEvent(TurnCycleEvent Event) : TurnBattleEvent;
