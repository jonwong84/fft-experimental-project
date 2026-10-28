using System.Collections.Immutable;

namespace Zodiac.Framework;

internal enum FacingMoveIntentKind { Rejected, Attack, Wait }

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
    internal FftRandomState Rng { get; }
    internal T? Diagnostic { get; }
    internal FacingMoveDecision(FacingMoveDecisionContext owner, FacingMoveIntentKind kind, FftRandomState rng,
        T? diagnostic, MoveUnitCommand? move = null, FlatOrdinaryMovementProfile? profile = null,
        AttackUnitCommand? attack = null, BoundedSwordWeapon? weapon = null, FacingChoice? facing = null)
    { Owner = owner; Kind = kind; Rng = rng; Diagnostic = diagnostic; Move = move; Profile = profile; Attack = attack; Weapon = weapon; Facing = facing; }
}

public sealed class FacingMoveDecisionContext
{
    private bool closed;
    public AttackBattleSnapshot Battle { get; }
    public BattleUnitId ActorId { get; }
    public FftRandomState RngState => Battle.TurnBattle.Turns.RngState;
    internal FacingMoveDecisionContext(AttackBattleSnapshot battle, BattleUnitId actorId) { Battle = battle; ActorId = actorId; }
    internal void Close() => closed = true;
    private void CheckOpen()
    { if (closed) throw new InvalidOperationException("The Move planning invocation has ended."); }
    public FacingMoveDecision<T> Reject<T>()
    { CheckOpen(); return new(this, FacingMoveIntentKind.Rejected, RngState, default); }
    public FacingMoveDecision<T> PrepareMoveThenAttack<T>(MoveUnitCommand move, FlatOrdinaryMovementProfile profile,
        AttackUnitCommand attack, BoundedSwordWeapon? weapon, FftRandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(move); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(attack);
        return new(this, FacingMoveIntentKind.Attack, rng, diagnostic, move, profile, attack, weapon);
    }
    public FacingMoveDecision<T> PrepareMoveThenWait<T>(MoveUnitCommand move, FlatOrdinaryMovementProfile profile,
        FacingChoice facing, FftRandomState rng, T diagnostic)
    {
        CheckOpen(); ArgumentNullException.ThrowIfNull(move); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(facing);
        return new(this, FacingMoveIntentKind.Wait, rng, diagnostic, move, profile, facing: facing);
    }
}

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
    internal FacingMoveContinuation(FacingTurnState state, BattleUnitId actorId, FacingMoveIntentKind kind,
        AttackUnitCommand? attack, BoundedSwordWeapon? weapon, FacingChoice? facing)
    { ExpectedState = state; ExpectedBattle = state.Battle; ActorId = actorId; Revision = state.Revision; Lineage = state.Lineage;
        Kind = kind; Attack = attack; Weapon = weapon; Facing = facing; }
}

public enum FacingMoveRejection
{
    None, AuthorityRejected, UnavailableChoices, DecisionRejected, InvalidDecision, InvalidIntent,
    InvalidRandomProgress, MoveRejected, MissingContinuation, StaleContinuation, WrongActor, CommandRejected
}
public sealed record FacingMoveResult<T>(FacingMoveRejection Rejection, FacingTurnRejection FacingRejection,
    TurnCycleRejection TurnRejection, FlatMovementRejection PathRejection, FacingTurnState State,
    ImmutableArray<AttackResultEvent> Events, FlatMovementPath? Path, FacingMoveContinuation? Continuation, T? Diagnostic)
{
    public bool Accepted => Rejection == FacingMoveRejection.None;
}
public sealed record FacingMoveContinueResult(FacingMoveRejection Rejection, FacingTurnResult Transition)
{
    public bool Accepted => Rejection == FacingMoveRejection.None && Transition.Accepted;
    public FacingTurnState State => Transition.State;
    public ImmutableArray<AttackResultEvent> Events => Transition.Events;
}

/// <summary>Framework-owned fresh planning, actual path Move and exact-session retained Attack/Wait.</summary>
/// <remarks>ZOD-167/168. Each published command is atomic; follow-up failure never rewinds a successful Move.</remarks>
public static class FacingMoveDecisionBattle
{
    public static FacingMoveResult<T> ExecuteMove<T>(FacingTurnState state, BattleUnitId actorId,
        Func<FacingMoveDecisionContext, FacingMoveDecision<T>> planner)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(planner);
        FacingMoveResult<T> Reject(FacingMoveRejection reason, FacingTurnRejection facing = FacingTurnRejection.None,
            TurnCycleRejection turn = TurnCycleRejection.None, FlatMovementRejection path = FlatMovementRejection.None) =>
            new(reason, facing, turn, path, state, [], null, null, default);
        var guard = FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None) return Reject(FacingMoveRejection.AuthorityRejected, guard);
        var actorTurn = state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (actorTurn.MovementTaken || actorTurn.ActionTaken) return Reject(FacingMoveRejection.UnavailableChoices);
        var context = new FacingMoveDecisionContext(state.Battle, actorId);
        FacingMoveDecision<T> decision;
        try { decision = planner(context); }
        finally { context.Close(); }
        if (decision is null || !ReferenceEquals(decision.Owner, context)) return Reject(FacingMoveRejection.InvalidDecision);
        if (decision.Kind == FacingMoveIntentKind.Rejected) return Reject(FacingMoveRejection.DecisionRejected);
        if (decision.Move!.UnitId != actorId || !ValidFollowup(context.Battle, actorId, decision.Kind, decision.Attack, decision.Facing))
            return Reject(FacingMoveRejection.InvalidIntent);
        if (!FftBiosRandom.IsForwardCheckpoint(context.RngState, decision.Rng)) return Reject(FacingMoveRejection.InvalidRandomProgress);

        var old = state.Battle.TurnBattle;
        var battle = new BattleState(old.Battle.Map, old.Battle.Units, decision.Rng);
        var turns = new TurnCycleState(old.Turns.Units, decision.Rng, old.Turns.Ticks, old.Turns.Phase);
        var staged = new FacingTurnState(new(new(battle, turns), state.Battle.Combatants), state.Profile, state.Revision, null, state.Lineage);
        var moved = FacingTurnPathMovement.MoveAlongPath(staged, decision.Move, decision.Profile!);
        if (!moved.Accepted) return Reject(FacingMoveRejection.MoveRejected, moved.FacingRejection, moved.TurnRejection, moved.PathRejection);
        var continuation = new FacingMoveContinuation(moved.State, actorId, decision.Kind, decision.Attack, decision.Weapon, decision.Facing);
        return new(FacingMoveRejection.None, FacingTurnRejection.None, TurnCycleRejection.None, FlatMovementRejection.None,
            moved.State, moved.Events, moved.Path, continuation, decision.Diagnostic);
    }

    public static FacingMoveContinueResult Continue(FacingTurnState state, BattleUnitId actorId, FacingMoveContinuation? continuation)
    {
        ArgumentNullException.ThrowIfNull(state);
        FacingMoveContinueResult Reject(FacingMoveRejection reason, FacingTurnRejection facing = FacingTurnRejection.None) =>
            new(reason, FacingTurnBattle.Reject(state, facing));
        if (continuation is null) return Reject(FacingMoveRejection.MissingContinuation);
        if (!ReferenceEquals(state, continuation.ExpectedState) || !ReferenceEquals(state.Battle, continuation.ExpectedBattle) ||
            !ReferenceEquals(state.Lineage, continuation.Lineage) || state.Revision != continuation.Revision)
            return Reject(FacingMoveRejection.StaleContinuation);
        if (actorId != continuation.ActorId) return Reject(FacingMoveRejection.WrongActor);
        var guard = FacingTurnBattle.ValidateWait(state, actorId);
        if (guard != FacingTurnRejection.None) return Reject(FacingMoveRejection.AuthorityRejected, guard);
        var turn = state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        if (!turn.MovementTaken || turn.ActionTaken) return Reject(FacingMoveRejection.UnavailableChoices);
        var result = continuation.Kind == FacingMoveIntentKind.Attack
            ? FacingTurnBattle.Attack(state, continuation.Attack!, continuation.Weapon)
            : FacingTurnBattle.WaitWithFacing(state, actorId, continuation.Facing!);
        return result.Accepted ? new(FacingMoveRejection.None, result)
            : new(FacingMoveRejection.CommandRejected, result with { State = state, Events = [] });
    }

    private static bool ValidFollowup(AttackBattleSnapshot state, BattleUnitId actorId, FacingMoveIntentKind kind,
        AttackUnitCommand? attack, FacingChoice? facing)
    {
        bool KnownEnemy(BattleUnitId target) => target != actorId && state.Combatants.Any(u => u.UnitId == target && u.Hp > 0);
        return kind switch
        {
            FacingMoveIntentKind.Attack => attack is not null && attack.ActorId == actorId && KnownEnemy(attack.TargetId),
            FacingMoveIntentKind.Wait => facing switch
            {
                FacingChoice.Fixed choice => Enum.IsDefined(choice.Facing),
                FacingChoice.KeepCurrent => true,
                FacingChoice.ChooseClosedEnemy choice => KnownEnemy(choice.EnemyId),
                _ => false
            },
            _ => false
        };
    }
}
