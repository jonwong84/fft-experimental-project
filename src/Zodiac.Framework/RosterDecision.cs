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

/// <summary>Read-only live input and concrete command factories. No public constructor or custom command delegate.</summary>
public sealed partial class RosterDecisionContext
{
    private bool closed;
    public AttackBattleSnapshot Battle { get; }
    public FlatRosterBattleProfile Profile { get; }
    public BattleUnitId ActorId { get; }
    public RandomState RngState => Battle.TurnBattle.Turns.RngState;
    internal RosterDecisionContext(RosterFacingTurnState state, BattleUnitId actorId)
    { Battle = state.Battle; Profile = state.Profile; ActorId = actorId; }
    internal void Close() => closed = true;
    private void CheckOpen() { if (closed) throw new InvalidOperationException("The roster planning invocation has ended."); }
    internal RosterDecision<T> Issue<T>(BattleUnitId actor, RandomState rng, RosterDecisionChoices choices,
        Func<RosterFacingTurnState, RosterDecisionResult<T>> application,
        Func<AttackBattleSnapshot, AttackCommandRejection>? admission = null)
    { CheckOpen(); return new(this, actor, rng, choices, application, admission); }
    internal static RosterDecisionResult<T> Applied<T>(RosterFacingResult result, T diagnostic) =>
        new(result.Accepted ? FacingDecisionRejection.None : FacingDecisionRejection.CommandRejected, result, result.Accepted ? diagnostic : default);

    public RosterDecision<T> Reject<T>() { CheckOpen(); return new(this, ActorId, RngState, RosterDecisionChoices.Any, null); }
    public RosterDecision<T> PrepareAttack<T>(AttackUnitCommand command, BoundedSwordWeapon? weapon, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command);
        return Issue(command.ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.Attack(state, command, weapon), diagnostic));
    }
    public RosterDecision<T> PrepareImmediateAbility<T>(UseImmediateAbilityCommand command, ImmediateAbilityBattleProfile profile, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        return Issue(command.ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.UseImmediateAbility(state, command, profile), diagnostic));
    }
    public RosterDecision<T> PrepareTargetedAbility<T>(UseTargetedAbilityCommand command, TargetedAbilityBattleProfile profile,
        FlatRosterProjectileHeights heights, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(heights);
        return Issue(command.ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.UseTargetedAbility(state, command, profile, heights), diagnostic));
    }
    public RosterDecision<T> PrepareWait<T>(BattleFacing? facing, RandomState rng, T diagnostic) =>
        Issue(ActorId, rng, RosterDecisionChoices.Any, state => Applied(RosterFacingTurnBattle.WaitWithFacing(state, ActorId, facing), diagnostic));

    /// <summary>Atomically publish a post-ACT actual path and its receipt-bound explicit facing.</summary>
    public RosterDecision<T> PrepareTurnEndingMove<T>(MoveUnitCommand command, BattleFacing? facing, RandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(command);
        return Issue(command.UnitId, rng, RosterDecisionChoices.ActUsedMoveUnused, state => {
            var move = RosterFacingTurnBattle.Move(state, command);
            if (!move.Accepted) return Applied(move, diagnostic);
            var finished = RosterFacingTurnBattle.FinishFacing(move.State, ActorId, move.State.PendingFacing, facing);
            return finished.Accepted ? Applied(finished with { Events = move.Events.AddRange(finished.Events), Path = move.Path }, diagnostic)
                : Applied(finished, diagnostic);
        });
    }
}

public sealed partial record RosterDecisionResult<T>(FacingDecisionRejection Rejection, RosterFacingResult Transition, T? Diagnostic)
{
    public bool Accepted => Rejection == FacingDecisionRejection.None && Transition.Accepted;
    public RosterFacingTurnState State => Transition.State;
    public System.Collections.Immutable.ImmutableArray<AttackResultEvent> Events => Transition.Events;
    public FlatMovementPath? Path => Transition.Path;
}

/// <summary>Own one fresh planning invocation, verify full-word/count RNG continuity and atomically publish one real roster command.</summary>
/// <remarks>ZOD-310/311; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No cached proposal executor, AI reference or new friend access.</remarks>
public static class RosterDecisionBattle
{
    public static RosterDecisionResult<T> Execute<T>(RosterFacingTurnState state, BattleUnitId actorId,
        Func<RosterDecisionContext, RosterDecision<T>> planner)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(planner);
        RosterDecisionResult<T> Reject(FacingDecisionRejection reason, RosterFacingResult? failure = null) =>
            new(reason, new(false, failure?.FacingRejection ?? FacingTurnRejection.None, failure?.AttackRejection ?? AttackCommandRejection.None,
                failure?.TurnRejection ?? TurnCycleRejection.None, failure?.PathRejection ?? FlatMovementRejection.None, state, []), default);
        RosterFacingResult GuardFailure(FacingTurnRejection reason) => new(false, reason, AttackCommandRejection.None, TurnCycleRejection.None, FlatMovementRejection.None, state, []);
        var guard = FacingReceiptRules.ValidateWait(state.Battle, state.PendingFacing, state.Revision, actorId);
        if (guard != FacingTurnRejection.None) return Reject(FacingDecisionRejection.AuthorityRejected, GuardFailure(guard));
        var context = new RosterDecisionContext(state, actorId); RosterDecision<T> decision;
        try { decision = planner(context); } finally { context.Close(); }
        if (decision is null || !ReferenceEquals(decision.Owner, context)) return Reject(FacingDecisionRejection.InvalidDecision);
        if (decision.Application is null) return Reject(FacingDecisionRejection.DecisionRejected);
        if (decision.ActorId != actorId) return Reject(FacingDecisionRejection.AuthorityRejected, GuardFailure(FacingTurnRejection.WrongActor));
        if (!RandomStateGenerator.IsForwardCheckpoint(context.RngState, decision.Rng)) return Reject(FacingDecisionRejection.InvalidRandomProgress);
        var actor = context.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (decision.Choices == RosterDecisionChoices.BothUnused && (actor.MovementTaken || actor.ActionTaken) ||
            decision.Choices == RosterDecisionChoices.ActUsedMoveUnused && (actor.MovementTaken || !actor.ActionTaken))
            return Reject(FacingDecisionRejection.UnavailableChoices);
        var admission = decision.Admission?.Invoke(context.Battle) ?? AttackCommandRejection.None;
        if (admission != AttackCommandRejection.None) return Reject(FacingDecisionRejection.CommandRejected,
            new(false, FacingTurnRejection.None, admission, TurnCycleRejection.None, FlatMovementRejection.None, state, []));
        var old = state.Battle.TurnBattle;
        var battle = new AttackBattleSnapshot(new(new(old.Battle.Map, old.Battle.Units, decision.Rng),
            new(old.Turns.Units, decision.Rng, old.Turns.Ticks, old.Turns.Phase)), state.Battle.Combatants);
        var staged = new RosterFacingTurnState(battle, state.Profile, state.Revision, null, state.Lineage);
        var applied = decision.Application(staged);
        return applied.Accepted ? applied : Reject(applied.Rejection, applied.Transition);
    }
}
