using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Typed facing intent. Choosing an enemy explicitly certifies its opposing role in this closed profile.</summary>
public abstract record FacingChoice
{
    private FacingChoice() { }
    public sealed record Fixed(BattleFacing Facing) : FacingChoice;
    public sealed record KeepCurrent : FacingChoice;
    public sealed record ChooseClosedEnemy(BattleUnitId EnemyId) : FacingChoice;
}

/// <summary>Opaque in-memory authorization for a fully settled, automatically ended turn; not a save token.</summary>
public sealed class FacingCompletionReceipt
{
    public BattleUnitId ActorId { get; }
    public ulong Revision { get; }
    internal AttackBattleSnapshot ExpectedBattle { get; }
    internal object Lineage { get; }

    internal FacingCompletionReceipt(BattleUnitId actorId, ulong revision, AttackBattleSnapshot battle, object lineage)
    { ActorId = actorId; Revision = revision; ExpectedBattle = battle; Lineage = lineage; }
}

/// <summary>Opt-in immutable command boundary. Only Begin and accepted transitions issue production states.</summary>
public sealed class FacingTurnState
{
    public AttackBattleSnapshot Battle { get; }
    public FlatSwordBattleProfile Profile { get; }
    public ulong Revision { get; }
    public FacingCompletionReceipt? PendingFacing { get; }
    internal object Lineage { get; }

    internal FacingTurnState(AttackBattleSnapshot battle, FlatSwordBattleProfile profile, ulong revision,
        FacingCompletionReceipt? pendingFacing, object lineage)
    { Battle = battle; Profile = profile; Revision = revision; PendingFacing = pendingFacing; Lineage = lineage; }
}

public enum FacingTurnRejection
{
    None, FacingPending, MissingCompletion, StaleCompletion, WrongActor, InactiveActor,
    UnknownActor, InconsistentCompletion, RevisionExhausted, UnsupportedFacing, NoLivingEnemy,
}

public sealed record FacingChoiceResult(bool Accepted, FacingTurnRejection Rejection, BattleFacing? Facing);
public sealed record FacingSettledEvent(BattleUnitId ActorId, BattleFacing Before, BattleFacing After) : AttackResultEvent;
public sealed record FacingTurnResult(bool Accepted, FacingTurnRejection FacingRejection,
    AttackCommandRejection AttackRejection, TurnCycleRejection TurnRejection,
    BattleTransitionRejection MovementRejection, FacingTurnState State, ImmutableArray<AttackResultEvent> Events);

/// <summary>Bounded ordinary facing settlement; no AI search, path heading, decision-RNG commit or save activation.</summary>
/// <remarks>Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-118/119.</remarks>
public static class FacingTurnBattle
{
    /// <summary>Start a fresh certified setup, never attach an active/ended checkpoint or caller-issued receipt.</summary>
    public static FacingTurnState Begin(AttackBattleSnapshot battle, FlatSwordBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(battle);
        ArgumentNullException.ThrowIfNull(profile);
        profile.ValidateState(battle);
        battle.ValidateInitialSetup();
        var turns = battle.TurnBattle.Turns;
        if (battle.TurnBattle.Battle.Map.Width > 128 || battle.TurnBattle.Battle.Map.Depth > 128 ||
            turns.Phase != TurnCyclePhase.AddCt || turns.Ticks != 0 ||
            turns.Units.Any(u => u.HasTurn || u.MovementTaken || u.ActionTaken))
            throw new NotSupportedException("Facing sessions require a fresh AddCt setup, zero elapsed ticks/choices and coordinates below signed-byte wrap.");
        return new(battle, profile, 0, null, new object());
    }

    public static FacingTurnResult Advance(FacingTurnState state) => Delegate(state, () => AttackBattleSimulator.Advance(state.Battle));

    public static FacingTurnResult Move(FacingTurnState state, MoveUnitCommand command,
        BattleMovementPolicy movementPolicy = BattleMovementPolicy.FlatGridPrototype)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Delegate(state, () => AttackBattleSimulator.Move(state.Battle, command, movementPolicy));
    }

    public static FacingTurnResult Attack(FacingTurnState state, AttackUnitCommand command, BoundedSwordWeapon? weapon)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Delegate(state, () => AttackBattleSimulator.Attack(state.Battle, command, weapon, state.Profile));
    }

    /// <summary>Pure query for the current active or pending actor. It consumes neither revision nor receipt, even at counter limits.</summary>
    public static FacingChoiceResult Probe(FacingTurnState state, BattleUnitId actorId, FacingChoice choice)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(choice);
        if (state.PendingFacing is { } pending)
        {
            if (pending.ActorId != actorId) return ChoiceReject(FacingTurnRejection.WrongActor);
        }
        else if (state.Battle.TurnBattle.Turns.ActiveUnitId != actorId) return ChoiceReject(FacingTurnRejection.InactiveActor);
        return Resolve(state, actorId, choice);
    }

    public static FacingTurnResult WaitWithFacing(FacingTurnState state, BattleUnitId actorId, FacingChoice choice)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(choice);
        if (state.PendingFacing is not null) return Reject(state, FacingTurnRejection.FacingPending);
        if (!state.Battle.Combatants.Any(u => u.UnitId == actorId)) return Reject(state, FacingTurnRejection.UnknownActor);
        if (state.Battle.TurnBattle.Turns.ActiveUnitId != actorId) return Reject(state, FacingTurnRejection.InactiveActor);
        if (state.Revision == ulong.MaxValue) return Reject(state, FacingTurnRejection.RevisionExhausted);
        var resolved = Resolve(state, actorId, choice);
        if (!resolved.Accepted) return Reject(state, resolved.Rejection);
        var wait = AttackBattleSimulator.Wait(state.Battle, actorId);
        if (!wait.Accepted) return DelegatedReject(state, wait);
        return Settle(state, wait.State, actorId, resolved.Facing!.Value, wait.Events);
    }

    public static FacingTurnResult FinishFacing(FacingTurnState state, BattleUnitId actorId,
        FacingCompletionReceipt? receipt, FacingChoice choice)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(choice);
        if (state.PendingFacing is not { } pending) return Reject(state, FacingTurnRejection.MissingCompletion);
        if (!ReferenceEquals(receipt, pending) || !ReferenceEquals(pending.ExpectedBattle, state.Battle) ||
            !ReferenceEquals(pending.Lineage, state.Lineage) || pending.Revision != state.Revision)
            return Reject(state, FacingTurnRejection.StaleCompletion);
        if (actorId != pending.ActorId) return Reject(state, FacingTurnRejection.WrongActor);
        if (state.Battle.TurnBattle.Turns.ActiveUnitId is not null) return Reject(state, FacingTurnRejection.InconsistentCompletion);
        if (state.Revision == ulong.MaxValue) return Reject(state, FacingTurnRejection.RevisionExhausted);
        var resolved = Resolve(state, actorId, choice);
        return resolved.Accepted ? Settle(state, state.Battle, actorId, resolved.Facing!.Value, []) : Reject(state, resolved.Rejection);
    }

    private static FacingTurnResult Delegate(FacingTurnState state, Func<AttackBattleResult> apply)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.PendingFacing is not null) return Reject(state, FacingTurnRejection.FacingPending);
        if (state.Revision == ulong.MaxValue) return Reject(state, FacingTurnRejection.RevisionExhausted);
        var result = apply();
        if (!result.Accepted) return DelegatedReject(state, result);
        ulong revision = state.Revision + 1;
        var ended = result.Events.OfType<AttackTurnBattleEvent>().Select(e => e.Event)
            .OfType<TurnBattleClockEvent>().Select(e => e.Event).OfType<TurnEndedEvent>().SingleOrDefault();
        // The legacy result is complete here: never mint from an intermediate TurnEndedEvent during damage resolution.
        var receipt = ended is null ? null : new FacingCompletionReceipt(ended.UnitId, revision, result.State, state.Lineage);
        return Accept(new(result.State, state.Profile, revision, receipt, state.Lineage), result.Events);
    }

    private static FacingChoiceResult Resolve(FacingTurnState state, BattleUnitId actorId, FacingChoice choice)
    {
        var actor = state.Battle.Combatants.FirstOrDefault(u => u.UnitId == actorId);
        if (actor is null || actor.Hp == 0) return ChoiceReject(FacingTurnRejection.UnknownActor);
        switch (choice)
        {
            case FacingChoice.Fixed fixedChoice:
                return Enum.IsDefined(fixedChoice.Facing) ? new(true, FacingTurnRejection.None, fixedChoice.Facing)
                    : ChoiceReject(FacingTurnRejection.UnsupportedFacing);
            case FacingChoice.KeepCurrent:
                return new(true, FacingTurnRejection.None, actor.Facing);
            case FacingChoice.ChooseClosedEnemy enemyChoice:
                var enemy = state.Battle.Combatants.FirstOrDefault(u => u.UnitId == enemyChoice.EnemyId && u.UnitId != actorId);
                if (enemy is null) return ChoiceReject(FacingTurnRejection.UnsupportedFacing);
                if (enemy.Hp == 0) return ChoiceReject(FacingTurnRejection.NoLivingEnemy);
                state.Battle.TurnBattle.Battle.TryGetUnit(actorId, out var from);
                state.Battle.TurnBattle.Battle.TryGetUnit(enemy.UnitId, out var to);
                int dx = to!.Position.X - from!.Position.X, dy = to.Position.Y - from.Position.Y;
                // In the certified open flat rectangle the direction toward its only living enemy is viable,
                // including edges/diagonals. Both source nearest modes select that same enemy; no status/team inference.
                BattleFacing? face = dy - dx <= 0 && dy + dx < 0 ? BattleFacing.South
                    : dy - dx > 0 && dy + dx <= 0 ? BattleFacing.West
                    : dy - dx >= 0 && dy + dx > 0 ? BattleFacing.North
                    : dy - dx < 0 && dy + dx >= 0 ? BattleFacing.East : null;
                return face is null ? ChoiceReject(FacingTurnRejection.UnsupportedFacing) : new(true, FacingTurnRejection.None, face);
            default: return ChoiceReject(FacingTurnRejection.UnsupportedFacing);
        }
    }

    private static FacingTurnResult Settle(FacingTurnState before, AttackBattleSnapshot battle,
        BattleUnitId actorId, BattleFacing face, ImmutableArray<AttackResultEvent> events)
    {
        var actor = battle.Combatants.Single(u => u.UnitId == actorId);
        var next = new AttackBattleSnapshot(battle.TurnBattle, battle.Combatants.Select(u => u.UnitId == actorId
            ? u.WithResult(u.Hp, face, u.KoCount) : u));
        return Accept(new(next, before.Profile, before.Revision + 1, null, before.Lineage),
            events.Add(new FacingSettledEvent(actorId, actor.Facing, face)));
    }

    private static FacingChoiceResult ChoiceReject(FacingTurnRejection rejection) => new(false, rejection, null);
    private static FacingTurnResult Reject(FacingTurnState state, FacingTurnRejection rejection) =>
        new(false, rejection, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
    private static FacingTurnResult DelegatedReject(FacingTurnState state, AttackBattleResult result) =>
        new(false, FacingTurnRejection.None, result.AttackRejection, result.TurnRejection, result.MovementRejection, state, []);
    private static FacingTurnResult Accept(FacingTurnState state, ImmutableArray<AttackResultEvent> events) =>
        new(true, FacingTurnRejection.None, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, state, events);
}
