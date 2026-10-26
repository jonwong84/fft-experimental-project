using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>
/// Executable projection of one range-1 striking, neutral, empty-proc sword with no active modifiers.
/// Content adapters validate their full definition before creating this projection; it is not a raw item record.
/// </summary>
public sealed record BoundedSwordWeapon
{
    public AssetId Id { get; }
    public int Power { get; }

    public BoundedSwordWeapon(AssetId id, int power)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("An explicit weapon ID is required.", nameof(id));
        if (power is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(power));
        Id = id;
        Power = power;
    }
}

public sealed record AttackBattleResult(bool Accepted, AttackCommandRejection AttackRejection,
    TurnCycleRejection TurnRejection, BattleTransitionRejection MovementRejection,
    AttackBattleSnapshot State, ImmutableArray<AttackResultEvent> Events);

/// <summary>Pure bounded sword/HP/turn transitions; no preview, reactions, knockback or death scheduler.</summary>
/// <remarks>Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; docs/behavior/bounded-sword-implementation.md.</remarks>
public static class AttackBattleSimulator
{
    public static AttackBattleResult Attack(AttackBattleSnapshot state, AttackUnitCommand command, BoundedSwordWeapon? weapon)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        AttackUnitState? actor = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.ActorId);
        if (actor is null) return Reject(state, AttackCommandRejection.UnknownActor);
        AttackUnitState? target = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.TargetId);
        if (target is null) return Reject(state, AttackCommandRejection.UnknownTarget);
        if (state.TurnBattle.Turns.ActiveUnitId != command.ActorId) return Reject(state, AttackCommandRejection.InactiveActor);
        if (state.TurnBattle.Turns.Units.Single(unit => unit.Id == command.ActorId).ActionTaken)
            return Reject(state, AttackCommandRejection.ActionAlreadyTaken);
        if (command.ActorId == command.TargetId) return Reject(state, AttackCommandRejection.SelfTarget);
        if (target.Status == AttackUnitStatus.Dead) return Reject(state, AttackCommandRejection.DeadTarget);
        if (!SupportedGeometry(state) || weapon is null || actor.RightHandWeaponId != weapon.Id)
            return Reject(state, AttackCommandRejection.UnsupportedSetup);

        state.TurnBattle.Battle.TryGetUnit(actor.UnitId, out var actorBody);
        state.TurnBattle.Battle.TryGetUnit(target.UnitId, out var targetBody);
        TilePosition from = actorBody!.Position, to = targetBody!.Position;
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (Math.Abs(dx) + Math.Abs(dy) != 1 || from.Layer != to.Layer)
            return Reject(state, AttackCommandRejection.OutOfRange);
        // Conservative domain restriction BEFORE drawing: valid knockback destinations are not implemented.
        if (state.TurnBattle.Battle.Map.TryGetTile(new(to.X + dx, to.Y + dy, to.Layer), out _))
            return Reject(state, AttackCommandRejection.UnsupportedSetup);
        int baseXa = Math.Max(1, actor.PhysicalAttack); // Neutral zodiac still floors XA to 1.
        if ((2 * baseXa - 1) * weapon.Power > short.MaxValue)
            return Reject(state, AttackCommandRejection.UnsupportedSetup);

        var rng = state.TurnBattle.Turns.RngState;
        uint Draw()
        {
            var draw = new FftBiosRandom().Next(rng);
            rng = draw.State;
            return draw.Value;
        }
        BattleFacing front = dx > 0 ? BattleFacing.West : dx < 0 ? BattleFacing.East : dy > 0 ? BattleFacing.South : BattleFacing.North;
        int classEvade = target.Facing == front ? target.ClassPhysicalEvade : 0;
        bool hit = true, critical = false;
        int damage = 0;
        try
        {
            // Even zero evades draw. With no equipment only the final (class) source can evade.
            foreach (int evade in new[] { 0, 0, 0, classEvade })
                if (Draw() * 100 / 32768 < evade) { hit = false; break; }
            if (hit)
            {
                critical = Draw() * 100 / 32768 < 4;
                int xa = critical ? baseXa + (int)(Draw() * baseXa / 32768) : baseXa;
                damage = Math.Min(999, xa * weapon.Power);
                _ = Draw(); // Conditional 19% proc roll occurs even with an empty type-0/type-128 set.
            }
        }
        catch (OverflowException)
        {
            return Reject(state, AttackCommandRejection.RandomDrawCountExhausted);
        }

        // Source marks action/possible turn completion before resolving damage. Publish all effects together.
        TurnBattleResult action = TurnBattleSimulator.ConfirmAct(state.TurnBattle, actor.UnitId);
        int hp = hit ? Math.Max(0, target.Hp - damage) : target.Hp;
        bool ko = target.Hp > 0 && hp == 0;
        AttackUnitState nextTarget = target.WithResult(hp, target.Facing, ko ? (target.KoCount + 1) & 255 : target.KoCount);
        // Presentation temporarily turns the actor; reward_grant restores living units before control returns.
        // The closed profile cannot kill the actor, so publish only its incoming settled facing (ZOD-112).
        AttackUnitState nextActor = actor.WithResult(actor.Hp, actor.Facing, actor.KoCount);
        var battle = new BattleState(action.State.Battle.Map, action.State.Battle.Units, rng);
        var turns = new TurnCycleState(action.State.Turns.Units, rng, action.State.Turns.Ticks, action.State.Turns.Phase);
        var next = new AttackBattleSnapshot(new(battle, turns), state.Combatants.Select(unit =>
            unit.UnitId == actor.UnitId ? nextActor : unit.UnitId == target.UnitId ? nextTarget : unit));
        var events = action.Events.Select(ev => (AttackResultEvent)new AttackTurnBattleEvent(ev)).ToList();
        events.Add(new AttackResolvedEvent(actor.UnitId, target.UnitId, hit, critical, damage));
        if (hp != target.Hp) events.Add(new AttackHpChangedEvent(target.UnitId, target.Hp, hp));
        if (nextTarget.Status != target.Status) events.Add(new AttackStatusChangedEvent(target.UnitId, target.Status, nextTarget.Status));
        if (ko) events.Add(new AttackKnockedOutEvent(target.UnitId, nextTarget.KoCount));
        return new(true, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, next, events.ToImmutableArray());
    }

    public static AttackBattleResult Advance(AttackBattleSnapshot state) => Transition(state, () => TurnBattleSimulator.Advance(state.TurnBattle), allowDead: false);
    public static AttackBattleResult Move(AttackBattleSnapshot state, MoveUnitCommand command,
        BattleMovementPolicy movementPolicy = BattleMovementPolicy.FlatGridPrototype) =>
        Transition(state, () => TurnBattleSimulator.Move(state.TurnBattle, command, movementPolicy), allowDead: false);
    public static AttackBattleResult Wait(AttackBattleSnapshot state, BattleUnitId unitId) => Transition(state, () => TurnBattleSimulator.Wait(state.TurnBattle, unitId), allowDead: true);
    public static AttackBattleResult ConfirmAct(AttackBattleSnapshot state, BattleUnitId unitId) => Transition(state, () => TurnBattleSimulator.ConfirmAct(state.TurnBattle, unitId), allowDead: false);

    private static AttackBattleResult Transition(AttackBattleSnapshot state, Func<TurnBattleResult> apply, bool allowDead)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!SupportedGeometry(state) || (!allowDead && state.Combatants.Any(unit => unit.Status == AttackUnitStatus.Dead)))
            return Reject(state, AttackCommandRejection.UnsupportedSetup);
        var result = apply();
        if (!result.Accepted) return new(false, AttackCommandRejection.None, result.TurnRejection, result.MovementRejection, state, []);
        var next = state.WithTurnBattle(result.State);
        if (!SupportedGeometry(next)) return Reject(state, AttackCommandRejection.UnsupportedSetup);
        return new(true, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, next,
            result.Events.Select(ev => (AttackResultEvent)new AttackTurnBattleEvent(ev)).ToImmutableArray());
    }

    private static bool SupportedGeometry(AttackBattleSnapshot state)
    {
        try { state.ValidateSupportedGeometry(); return true; }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return false; }
    }

    private static AttackBattleResult Reject(AttackBattleSnapshot state, AttackCommandRejection reason) =>
        new(false, reason, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
}
