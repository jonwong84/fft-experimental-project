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

public enum FacingDecisionRejection { None, AuthorityRejected, DecisionRejected, InvalidDecision, InvalidRandomProgress, CommandRejected, UnavailableChoices }
public sealed record FacingDecisionResult<T>(FacingDecisionRejection Rejection, FacingTurnResult Transition, T? Diagnostic)
{
    public bool Accepted => Transition.Accepted;
    public FacingTurnState State => Transition.State;
    public ImmutableArray<AttackResultEvent> Events => Transition.Events;
    public FlatMovementPath? Path { get; init; }
    public FlatMovementRejection PathRejection { get; init; }
    public FacingMoveContinuation? Continuation { get; init; }
    public FacingMoveRejection MoveRejection { get; init; }
}

/// <summary>Framework-owned fresh planning, RNG continuity and one authoritative command transaction.</summary>
public static class FacingDecisionBattle
{
    public static FacingDecisionResult<T> Execute<T>(FacingTurnState state, BattleUnitId actorId,
        Func<FacingDecisionContext, FacingDecision<T>> planner)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(planner);
        FacingDecisionResult<T> Reject(FacingDecisionRejection reason, FacingTurnResult? failed = null,
            FlatMovementRejection path = FlatMovementRejection.None) =>
            new(reason, failed is null ? FacingTurnBattle.Reject(state, FacingTurnRejection.None)
                : failed with { State = state, Events = [] }, default) { PathRejection = path };
        var guard = FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, guard));
        var context = new FacingDecisionContext(state.Battle, actorId);
        FacingDecision<T> decision;
        try { decision = planner(context); }
        finally { context.Close(); }
        if (decision is null || !ReferenceEquals(decision.Owner, context)) return Reject(FacingDecisionRejection.InvalidDecision);
        if (decision.Kind == FacingDecisionKind.Rejected) return Reject(FacingDecisionRejection.DecisionRejected);
        if (decision.Kind == FacingDecisionKind.PlannedMove)
        {
            // The same private planner issued this Move proposal. Reuse existing validation/adoption/path/token machinery.
            var moved = FacingMoveDecisionBattle.ApplyFreshMove(state, actorId, context.MoveContext, decision.PlannedMove!);
            var transition = new FacingTurnResult(moved.Accepted, moved.FacingRejection, AttackCommandRejection.None,
                moved.TurnRejection, BattleTransitionRejection.None, moved.State, moved.Events);
            var rejection = moved.Rejection switch
            {
                FacingMoveRejection.None => FacingDecisionRejection.None,
                FacingMoveRejection.InvalidRandomProgress => FacingDecisionRejection.InvalidRandomProgress,
                FacingMoveRejection.InvalidDecision => FacingDecisionRejection.InvalidDecision,
                FacingMoveRejection.UnavailableChoices => FacingDecisionRejection.UnavailableChoices,
                _ => FacingDecisionRejection.CommandRejected
            };
            return new(rejection, transition, moved.Diagnostic) { Path = moved.Path, PathRejection = moved.PathRejection,
                Continuation = moved.Continuation, MoveRejection = moved.Rejection };
        }
        if (!RandomStateGenerator.IsForwardCheckpoint(context.RngState, decision.Rng)) return Reject(FacingDecisionRejection.InvalidRandomProgress);
        if (decision.Kind == FacingDecisionKind.Attack && decision.Attack!.ActorId != actorId)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
        if (decision.Kind == FacingDecisionKind.TargetedAbility && decision.TargetedAbility!.ActorId != actorId)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
        if (decision.Kind == FacingDecisionKind.ImmediateAbility && decision.ImmediateAbility!.ActorId != actorId)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
        if (decision.Kind == FacingDecisionKind.TurnEndingMove)
        {
            if (decision.Move!.UnitId != actorId)
                return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
            var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
            if (actor.MovementTaken || !actor.ActionTaken) return Reject(FacingDecisionRejection.UnavailableChoices);
        }

        var old = state.Battle.TurnBattle;
        var battle = new BattleState(old.Battle.Map, old.Battle.Units, decision.Rng);
        var turns = new TurnCycleState(old.Turns.Units, decision.Rng, old.Turns.Ticks, old.Turns.Phase);
        var staged = new FacingTurnState(new(new(battle, turns), state.Battle.Combatants), state.Profile, state.Revision, null, state.Lineage);
        if (decision.Kind == FacingDecisionKind.TurnEndingMove)
        {
            var moved = FacingTurnPathMovement.MoveAlongPath(staged, decision.Move!, decision.MovementProfile!);
            if (!moved.Accepted)
                return Reject(FacingDecisionRejection.CommandRejected,
                    new(false, moved.FacingRejection, AttackCommandRejection.None, moved.TurnRejection,
                        BattleTransitionRejection.None, state, []), moved.PathRejection);
            // FinishFacing verifies the actual receipt, ended actor, exact battle, lineage and revision.
            var finished = FacingTurnBattle.FinishFacing(moved.State, actorId, moved.State.PendingFacing, decision.Facing!);
            if (!finished.Accepted) return Reject(FacingDecisionRejection.CommandRejected, finished);
            return new(FacingDecisionRejection.None, finished with { Events = moved.Events.AddRange(finished.Events) }, decision.Diagnostic)
                { Path = moved.Path };
        }
        var result = decision.Kind switch
        {
            FacingDecisionKind.Attack => FacingTurnBattle.Attack(staged, decision.Attack!, decision.Weapon),
            FacingDecisionKind.TargetedAbility => FacingTurnBattle.UseTargetedAbility(staged, decision.TargetedAbility!, decision.AbilityProfile!),
            FacingDecisionKind.ImmediateAbility => FacingTurnBattle.UseImmediateAbility(staged, decision.ImmediateAbility!, decision.ImmediateProfile!),
            _ => FacingTurnBattle.WaitWithFacing(staged, actorId, decision.Facing!)
        };
        return result.Accepted ? new(FacingDecisionRejection.None, result, decision.Diagnostic)
            : Reject(FacingDecisionRejection.CommandRejected, result);
    }
}
