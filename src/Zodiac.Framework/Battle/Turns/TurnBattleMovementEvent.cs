using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TurnBattleMovementEvent(BattleMovementEvent Movement) : TurnBattleEvent;
