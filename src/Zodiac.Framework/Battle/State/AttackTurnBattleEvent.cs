using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record AttackTurnBattleEvent(TurnBattleEvent Event) : AttackResultEvent;
