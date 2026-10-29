using System.Collections.Immutable;

namespace Zodiac.Framework;

internal enum FacingDecisionKind { Rejected, Attack, Wait, TurnEndingMove, TargetedAbility }

/// <summary>An opaque proposal issued by one planning invocation; never a battle-state or receipt factory.</summary>
public sealed class FacingDecision<T>
{
    internal FacingDecisionContext Owner { get; }
    internal FacingDecisionKind Kind { get; }
    internal FftRandomState Rng { get; }
    internal AttackUnitCommand? Attack { get; }
    internal BoundedSwordWeapon? Weapon { get; }
    internal FacingChoice? Facing { get; }
    internal MoveUnitCommand? Move { get; }
    internal FlatOrdinaryMovementProfile? MovementProfile { get; }
    internal UseTargetedAbilityCommand? TargetedAbility { get; }
    internal TargetedAbilityBattleProfile? AbilityProfile { get; }
    internal T? Diagnostic { get; }
    internal FacingDecision(FacingDecisionContext owner, FacingDecisionKind kind, FftRandomState rng,
        T? diagnostic, AttackUnitCommand? attack = null, BoundedSwordWeapon? weapon = null, FacingChoice? facing = null,
        MoveUnitCommand? move = null, FlatOrdinaryMovementProfile? movementProfile = null,
        UseTargetedAbilityCommand? targetedAbility = null, TargetedAbilityBattleProfile? abilityProfile = null)
    { Owner = owner; Kind = kind; Rng = rng; Diagnostic = diagnostic; Attack = attack; Weapon = weapon; Facing = facing;
        Move = move; MovementProfile = movementProfile; TargetedAbility = targetedAbility; AbilityProfile = abilityProfile; }
}

/// <summary>Read-only planning input with invocation-bound proposal factories; valid only during the callback.</summary>
public sealed class FacingDecisionContext
{
    private bool closed;
    public AttackBattleSnapshot Battle { get; }
    public BattleUnitId ActorId { get; }
    public FftRandomState RngState => Battle.TurnBattle.Turns.RngState;
    internal FacingDecisionContext(AttackBattleSnapshot battle, BattleUnitId actorId) { Battle = battle; ActorId = actorId; }
    internal void Close() => closed = true;
    private void CheckOpen()
    { if (closed) throw new InvalidOperationException("The planning invocation has ended."); }
    public FacingDecision<T> Reject<T>()
    { CheckOpen(); return new(this, FacingDecisionKind.Rejected, RngState, default); }
    public FacingDecision<T> PrepareAttack<T>(AttackUnitCommand command, BoundedSwordWeapon? weapon, FftRandomState rng, T diagnostic)
    { CheckOpen(); ArgumentNullException.ThrowIfNull(command); return new(this, FacingDecisionKind.Attack, rng, diagnostic, command, weapon); }
    public FacingDecision<T> PrepareWait<T>(FacingChoice facing, FftRandomState rng, T diagnostic)
    { CheckOpen(); ArgumentNullException.ThrowIfNull(facing); return new(this, FacingDecisionKind.Wait, rng, diagnostic, facing: facing); }

    /// <summary>Prepare one actual targeted ACT; grants/range and current choices are revalidated by Framework.</summary>
    public FacingDecision<T> PrepareTargetedAbility<T>(UseTargetedAbilityCommand command, TargetedAbilityBattleProfile profile,
        FftRandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        return new(this, FacingDecisionKind.TargetedAbility, rng, diagnostic, targetedAbility: command, abilityProfile: profile);
    }

    /// <summary>Prepare an ACT-used/Move-unused confirmed path and settle its actual completion facing atomically.</summary>
    public FacingDecision<T> PrepareTurnEndingMove<T>(MoveUnitCommand command, FlatOrdinaryMovementProfile profile,
        FacingChoice facing, FftRandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(facing);
        return new(this, FacingDecisionKind.TurnEndingMove, rng, diagnostic, facing: facing, move: command, movementProfile: profile);
    }
}

public enum FacingDecisionRejection { None, AuthorityRejected, DecisionRejected, InvalidDecision, InvalidRandomProgress, CommandRejected, UnavailableChoices }
public sealed record FacingDecisionResult<T>(FacingDecisionRejection Rejection, FacingTurnResult Transition, T? Diagnostic)
{
    public bool Accepted => Transition.Accepted;
    public FacingTurnState State => Transition.State;
    public ImmutableArray<AttackResultEvent> Events => Transition.Events;
    public FlatMovementPath? Path { get; init; }
    public FlatMovementRejection PathRejection { get; init; }
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
        if (!FftBiosRandom.IsForwardCheckpoint(context.RngState, decision.Rng)) return Reject(FacingDecisionRejection.InvalidRandomProgress);
        if (decision.Kind == FacingDecisionKind.Attack && decision.Attack!.ActorId != actorId)
            return Reject(FacingDecisionRejection.AuthorityRejected, FacingTurnBattle.Reject(state, FacingTurnRejection.WrongActor));
        if (decision.Kind == FacingDecisionKind.TargetedAbility && decision.TargetedAbility!.ActorId != actorId)
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
            _ => FacingTurnBattle.WaitWithFacing(staged, actorId, decision.Facing!)
        };
        return result.Accepted ? new(FacingDecisionRejection.None, result, decision.Diagnostic)
            : Reject(FacingDecisionRejection.CommandRejected, result);
    }
}
