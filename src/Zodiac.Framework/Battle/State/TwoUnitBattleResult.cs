using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record TwoUnitBattleResult(bool Accepted, TwoUnitBattleRejection ScenarioRejection,
    AttackCommandRejection AttackRejection, TurnCycleRejection TurnRejection,
    BattleTransitionRejection MovementRejection, TwoUnitBattleState State, ImmutableArray<TwoUnitBattleEvent> Events);
