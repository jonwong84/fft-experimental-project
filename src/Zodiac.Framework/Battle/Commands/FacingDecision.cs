using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

internal enum FacingDecisionKind { Rejected, Attack, Wait, TurnEndingMove, TargetedAbility, PlannedMove, ImmediateAbility }

/// <summary>An opaque proposal issued by one planning invocation; never a battle-state or receipt factory.</summary>
public sealed class FacingDecision<T>
{
    internal FacingDecisionContext Owner { get; }
    internal FacingDecisionKind Kind { get; }
    internal RandomState Rng { get; }
    internal AttackUnitCommand? Attack { get; }
    internal BoundedSwordWeapon? Weapon { get; }
    internal FacingChoice? Facing { get; }
    internal MoveUnitCommand? Move { get; }
    internal FlatOrdinaryMovementProfile? MovementProfile { get; }
    internal UseTargetedAbilityCommand? TargetedAbility { get; }
    internal TargetedAbilityBattleProfile? AbilityProfile { get; }
    internal FacingMoveDecision<T>? PlannedMove { get; }
    internal UseImmediateAbilityCommand? ImmediateAbility { get; }
    internal ImmediateAbilityBattleProfile? ImmediateProfile { get; }
    internal T? Diagnostic { get; }
    internal FacingDecision(FacingDecisionContext owner, FacingDecisionKind kind, RandomState rng,
        T? diagnostic, AttackUnitCommand? attack = null, BoundedSwordWeapon? weapon = null, FacingChoice? facing = null,
        MoveUnitCommand? move = null, FlatOrdinaryMovementProfile? movementProfile = null,
        UseTargetedAbilityCommand? targetedAbility = null, TargetedAbilityBattleProfile? abilityProfile = null,
        FacingMoveDecision<T>? plannedMove = null, UseImmediateAbilityCommand? immediateAbility = null,
        ImmediateAbilityBattleProfile? immediateProfile = null)
    { Owner = owner; Kind = kind; Rng = rng; Diagnostic = diagnostic; Attack = attack; Weapon = weapon; Facing = facing;
        Move = move; MovementProfile = movementProfile; TargetedAbility = targetedAbility; AbilityProfile = abilityProfile; PlannedMove = plannedMove;
        ImmediateAbility = immediateAbility; ImmediateProfile = immediateProfile; }
}
