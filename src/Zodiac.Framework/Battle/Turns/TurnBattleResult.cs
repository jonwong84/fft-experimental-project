using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Rejections preserve the original composed state and emit no events.</summary>
public sealed record TurnBattleResult(bool Accepted, TurnCycleRejection TurnRejection,
    BattleTransitionRejection MovementRejection, TurnBattleState State, ImmutableArray<TurnBattleEvent> Events);
