using Zodiac.Framework.Random;

namespace Zodiac.Framework;

internal enum RosterDecisionChoices { Any, BothUnused, ActUsedMoveUnused }

/// <summary>Opaque command proposal issued by exactly one open Framework planning invocation.</summary>
public sealed class RosterDecision<T>
{
    internal RosterDecisionContext Owner { get; }
    internal BattleUnitId ActorId { get; }
    internal RandomState Rng { get; }
    internal RosterDecisionChoices Choices { get; }
    internal Func<RosterFacingTurnState, RosterDecisionResult<T>>? Application { get; }
    internal Func<AttackBattleSnapshot, AttackCommandRejection>? Admission { get; }
    internal RosterDecision(RosterDecisionContext owner, BattleUnitId actorId, RandomState rng, RosterDecisionChoices choices,
        Func<RosterFacingTurnState, RosterDecisionResult<T>>? application,
        Func<AttackBattleSnapshot, AttackCommandRejection>? admission = null)
    { Owner = owner; ActorId = actorId; Rng = rng; Choices = choices; Application = application; Admission = admission; }
}
