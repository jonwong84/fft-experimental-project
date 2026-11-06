using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

internal enum FacingMoveIntentKind { Rejected, Attack, Wait, TargetedAbility }

/// <summary>An invocation-bound planned path Move and frozen follow-up; only the live context can issue one.</summary>
public sealed class FacingMoveDecision<T>
{
    internal FacingMoveDecisionContext Owner { get; }
    internal FacingMoveIntentKind Kind { get; }
    internal MoveUnitCommand? Move { get; }
    internal FlatOrdinaryMovementProfile? Profile { get; }
    internal AttackUnitCommand? Attack { get; }
    internal BoundedSwordWeapon? Weapon { get; }
    internal FacingChoice? Facing { get; }
    internal UseTargetedAbilityCommand? TargetedAbility { get; }
    internal TargetedAbilityBattleProfile? AbilityProfile { get; }
    internal RandomState Rng { get; }
    internal T? Diagnostic { get; }
    internal FacingMoveDecision(FacingMoveDecisionContext owner, FacingMoveIntentKind kind, RandomState rng,
        T? diagnostic, MoveUnitCommand? move = null, FlatOrdinaryMovementProfile? profile = null,
        AttackUnitCommand? attack = null, BoundedSwordWeapon? weapon = null, FacingChoice? facing = null,
        UseTargetedAbilityCommand? targetedAbility = null, TargetedAbilityBattleProfile? abilityProfile = null)
    { Owner = owner; Kind = kind; Rng = rng; Diagnostic = diagnostic; Move = move; Profile = profile; Attack = attack; Weapon = weapon; Facing = facing;
        TargetedAbility = targetedAbility; AbilityProfile = abilityProfile; }
}
