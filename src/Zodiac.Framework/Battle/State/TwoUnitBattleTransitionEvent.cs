using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TwoUnitBattleTransitionEvent(AttackResultEvent Event) : TwoUnitBattleEvent;
