using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record AttackBattleResult(bool Accepted, AttackCommandRejection AttackRejection,
    TurnCycleRejection TurnRejection, BattleTransitionRejection MovementRejection,
    AttackBattleSnapshot State, ImmutableArray<AttackResultEvent> Events);
