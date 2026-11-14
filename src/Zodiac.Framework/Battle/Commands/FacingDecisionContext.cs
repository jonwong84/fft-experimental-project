using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Read-only planning input with invocation-bound proposal factories; valid only during the callback.</summary>
public sealed class FacingDecisionContext
{
    private bool closed;
    public AttackBattleSnapshot Battle { get; }
    public BattleUnitId ActorId { get; }
    public RandomState RngState => Battle.TurnBattle.Turns.RngState;
    internal FacingMoveDecisionContext MoveContext { get; }
    internal FacingDecisionContext(AttackBattleSnapshot battle, BattleUnitId actorId)
    { Battle = battle; ActorId = actorId; MoveContext = new(battle, actorId); }
    internal void Close() { closed = true; MoveContext.Close(); }
    private void CheckOpen()
    { if (closed) throw new InvalidOperationException("The planning invocation has ended."); }
    public FacingDecision<T> Reject<T>()
    { CheckOpen(); return new(this, FacingDecisionKind.Rejected, RngState, default); }
    public FacingDecision<T> PrepareAttack<T>(AttackUnitCommand command, BoundedSwordWeapon? weapon, RandomState rng, T diagnostic)
    { CheckOpen(); ArgumentNullException.ThrowIfNull(command); return new(this, FacingDecisionKind.Attack, rng, diagnostic, command, weapon); }
    public FacingDecision<T> PrepareWait<T>(FacingChoice facing, RandomState rng, T diagnostic)
    { CheckOpen(); ArgumentNullException.ThrowIfNull(facing); return new(this, FacingDecisionKind.Wait, rng, diagnostic, facing: facing); }

    /// <summary>Prepare one actual targeted ACT; grants/range and current choices are revalidated by Framework.</summary>
    public FacingDecision<T> PrepareTargetedAbility<T>(UseTargetedAbilityCommand command, TargetedAbilityBattleProfile profile,
        RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        return new(this, FacingDecisionKind.TargetedAbility, rng, diagnostic, targetedAbility: command, abilityProfile: profile);
    }

    /// <summary>Prepare one actual self ACT; Framework revalidates current actor, grants, effect and choices.</summary>
    public FacingDecision<T> PrepareImmediateAbility<T>(UseImmediateAbilityCommand command, ImmediateAbilityBattleProfile profile,
        RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        return new(this, FacingDecisionKind.ImmediateAbility, rng, diagnostic, immediateAbility: command, immediateProfile: profile);
    }

    /// <summary>Prepare an ACT-used/Move-unused confirmed path and settle its actual completion facing atomically.</summary>
    public FacingDecision<T> PrepareTurnEndingMove<T>(MoveUnitCommand command, FlatOrdinaryMovementProfile profile,
        FacingChoice facing, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(facing);
        return new(this, FacingDecisionKind.TurnEndingMove, rng, diagnostic, facing: facing, move: command, movementProfile: profile);
    }

    public FacingDecision<T> PrepareMoveThenAttack<T>(MoveUnitCommand move, FlatOrdinaryMovementProfile profile,
        AttackUnitCommand attack, BoundedSwordWeapon? weapon, RandomState rng, T diagnostic)
    {
        CheckOpen();
        return Wrap(MoveContext.PrepareMoveThenAttack(move, profile, attack, weapon, rng, diagnostic));
    }

    public FacingDecision<T> PrepareMoveThenTargetedAbility<T>(MoveUnitCommand move, FlatOrdinaryMovementProfile profile,
        UseTargetedAbilityCommand command, TargetedAbilityBattleProfile abilityProfile, RandomState rng, T diagnostic)
    {
        CheckOpen();
        return Wrap(MoveContext.PrepareMoveThenTargetedAbility(move, profile, command, abilityProfile, rng, diagnostic));
    }

    public FacingDecision<T> PrepareMoveThenWait<T>(MoveUnitCommand move, FlatOrdinaryMovementProfile profile,
        FacingChoice facing, RandomState rng, T diagnostic)
    {
        CheckOpen();
        return Wrap(MoveContext.PrepareMoveThenWait(move, profile, facing, rng, diagnostic));
    }

    private FacingDecision<T> Wrap<T>(FacingMoveDecision<T> move) =>
        new(this, FacingDecisionKind.PlannedMove, move.Rng, move.Diagnostic, plannedMove: move);
}
