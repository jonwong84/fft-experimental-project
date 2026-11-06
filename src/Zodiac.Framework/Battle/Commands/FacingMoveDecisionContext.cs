using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed class FacingMoveDecisionContext
{
    private bool closed;
    public AttackBattleSnapshot Battle { get; }
    public BattleUnitId ActorId { get; }
    public RandomState RngState => Battle.TurnBattle.Turns.RngState;
    internal FacingMoveDecisionContext(AttackBattleSnapshot battle, BattleUnitId actorId) { Battle = battle; ActorId = actorId; }
    internal void Close() => closed = true;
    private void CheckOpen()
    { if (closed) throw new InvalidOperationException("The Move planning invocation has ended."); }
    public FacingMoveDecision<T> Reject<T>()
    { CheckOpen(); return new(this, FacingMoveIntentKind.Rejected, RngState, default); }
    public FacingMoveDecision<T> PrepareMoveThenAttack<T>(MoveUnitCommand move, FlatOrdinaryMovementProfile profile,
        AttackUnitCommand attack, BoundedSwordWeapon? weapon, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(move); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(attack);
        return new(this, FacingMoveIntentKind.Attack, rng, diagnostic, move, profile, attack, weapon);
    }
    public FacingMoveDecision<T> PrepareMoveThenWait<T>(MoveUnitCommand move, FlatOrdinaryMovementProfile profile,
        FacingChoice facing, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(move); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(facing);
        return new(this, FacingMoveIntentKind.Wait, rng, diagnostic, move, profile, facing: facing);
    }

    /// <summary>Freeze a targeted ACT across one confirmed Move; actual range is checked on continuation.</summary>
    public FacingMoveDecision<T> PrepareMoveThenTargetedAbility<T>(MoveUnitCommand move, FlatOrdinaryMovementProfile profile,
        UseTargetedAbilityCommand command, TargetedAbilityBattleProfile abilityProfile, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(move); ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(abilityProfile);
        return new(this, FacingMoveIntentKind.TargetedAbility, rng, diagnostic, move, profile,
            targetedAbility: command, abilityProfile: abilityProfile);
    }
}
