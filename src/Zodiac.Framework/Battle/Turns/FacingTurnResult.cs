using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record FacingTurnResult(bool Accepted, FacingTurnRejection FacingRejection,
    AttackCommandRejection AttackRejection, TurnCycleRejection TurnRejection,
    BattleTransitionRejection MovementRejection, FacingTurnState State, ImmutableArray<AttackResultEvent> Events);
