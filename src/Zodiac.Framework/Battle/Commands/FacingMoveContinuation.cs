using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Opaque in-memory follow-up authority bound to one exact, fully settled Move result; not a save token.</summary>
public sealed class FacingMoveContinuation
{
    internal FacingTurnState ExpectedState { get; }
    internal AttackBattleSnapshot ExpectedBattle { get; }
    internal BattleUnitId ActorId { get; }
    internal ulong Revision { get; }
    internal object Lineage { get; }
    internal FacingMoveIntentKind Kind { get; }
    internal AttackUnitCommand? Attack { get; }
    internal BoundedSwordWeapon? Weapon { get; }
    internal FacingChoice? Facing { get; }
    internal UseTargetedAbilityCommand? TargetedAbility { get; }
    internal TargetedAbilityBattleProfile? AbilityProfile { get; }
    internal FacingMoveContinuation(FacingTurnState state, BattleUnitId actorId, FacingMoveIntentKind kind,
        AttackUnitCommand? attack, BoundedSwordWeapon? weapon, FacingChoice? facing,
        UseTargetedAbilityCommand? targetedAbility = null, TargetedAbilityBattleProfile? abilityProfile = null)
    { ExpectedState = state; ExpectedBattle = state.Battle; ActorId = actorId; Revision = state.Revision; Lineage = state.Lineage;
        Kind = kind; Attack = attack; Weapon = weapon; Facing = facing; TargetedAbility = targetedAbility; AbilityProfile = abilityProfile; }
}
