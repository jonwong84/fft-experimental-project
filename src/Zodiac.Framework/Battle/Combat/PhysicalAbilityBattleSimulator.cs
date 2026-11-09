using System.Collections.Immutable;
using Zodiac.Framework.CombatRules;
using Zodiac.Framework.Random;
using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

/// <summary>Shared flat-roster physical delivery admission and settlement. Pair-only legacy admission remains separate.</summary>
public static class PhysicalAbilityBattleSimulator
{
    public static AttackBattleResult ApplyRoster(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        PhysicalAbilityBattleProfile profile, FlatRosterBattleProfile roster, FlatRosterProjectileHeights? heights = null)
    {
        var invalid = ValidateRoster(state, command, profile, roster, heights, out var target);
        if (invalid != AttackCommandRejection.None)
            return new(false, invalid, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
        return ApplyPhysical(state, command with { TargetId = target }, profile.Abilities[command.AbilityId], profile.Delivery,
            target == command.TargetId ? null : command.TargetId);
    }

    /// <summary>Pure current-command admission; trajectory work grants neither ACT nor RNG authority.</summary>
    public static AttackCommandRejection ValidateRosterCommand(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        PhysicalAbilityBattleProfile profile, FlatRosterBattleProfile roster, FlatRosterProjectileHeights? heights = null) =>
        ValidateRoster(state, command, profile, roster, heights, out _);

    private static AttackCommandRejection ValidateRoster(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        PhysicalAbilityBattleProfile profile, FlatRosterBattleProfile roster, FlatRosterProjectileHeights? heights,
        out BattleUnitId actualTarget)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(roster);
        if (profile.Delivery == PhysicalAbilityDelivery.DirectProjectile) ArgumentNullException.ThrowIfNull(heights);
        actualTarget = command.TargetId;
        if (!state.Combatants.Any(u => u.UnitId == command.ActorId)) return AttackCommandRejection.UnknownActor;
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == command.TargetId);
        if (target is null) return AttackCommandRejection.UnknownTarget;
        if (state.TurnBattle.Turns.ActiveUnitId != command.ActorId) return AttackCommandRejection.InactiveActor;
        if (state.TurnBattle.Turns.Units.Single(u => u.Id == command.ActorId).ActionTaken) return AttackCommandRejection.ActionAlreadyTaken;
        if (command.ActorId == command.TargetId) return AttackCommandRejection.SelfTarget;
        if (target.Status == AttackUnitStatus.Dead) return AttackCommandRejection.DeadTarget;
        var setup = ValidateRosterAbilitySetup(state, command.ActorId, command.AbilityId, profile, roster);
        if (setup != AttackCommandRejection.None) return setup;
        var origin = state.TurnBattle.Battle.Units.Single(u => u.Id == command.ActorId).Position;
        var to = state.TurnBattle.Battle.Units.Single(u => u.Id == command.TargetId).Position;
        long distance = Math.Abs((long)origin.X - to.X) + Math.Abs((long)origin.Y - to.Y);
        var ability = profile.Abilities[command.AbilityId];
        if (distance > ability.Range || profile.Delivery == PhysicalAbilityDelivery.AdjacentUnit && distance != 1)
            return AttackCommandRejection.OutOfRange;
        if (profile.Delivery == PhysicalAbilityDelivery.DirectProjectile)
        {
            var path = FlatRosterDirectTrajectory.EvaluateToTarget(state, new(command.ActorId, command.TargetId, origin), roster, heights!);
            if (!path.Accepted || path.Outcome!.ResolveTarget(command.TargetId) is not { } resolved)
                return AttackCommandRejection.UnsupportedSetup;
            actualTarget = resolved;
        }
        return AttackCommandRejection.None;
    }

    /// <summary>Effect/grant and complete flat-roster admission, independent of current target range and turn choice.</summary>
    public static AttackCommandRejection ValidateRosterAbilitySetup(AttackBattleSnapshot state, BattleUnitId actorId, AssetId abilityId,
        PhysicalAbilityBattleProfile profile, FlatRosterBattleProfile roster)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(roster);
        if (!state.Combatants.Any(u => u.UnitId == actorId)) return AttackCommandRejection.UnknownActor;
        if (!profile.Abilities.ContainsKey(abilityId)) return AttackCommandRejection.UnknownAbility;
        if (!profile.Units.TryGetValue(actorId, out var grant) || !grant.Abilities.Contains(abilityId)) return AttackCommandRejection.AbilityNotGranted;
        try { roster.ValidateState(state); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException) { return AttackCommandRejection.UnsupportedSetup; }
        return profile.Ground != roster.Ground || !profile.Units.Keys.ToHashSet().SetEquals(roster.Units.Keys) ||
            state.Combatants.Any(u => u.Hp == 0 || u.PhysicalAttack is < 1 or > 99)
            ? AttackCommandRejection.UnsupportedSetup : AttackCommandRejection.None;
    }

    internal static AttackBattleResult ApplyPhysical(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        PhysicalAbilityDefinition ability, PhysicalAbilityDelivery delivery, BattleUnitId? intendedTarget = null)
    {
        bool adjacentEvents = delivery == PhysicalAbilityDelivery.AdjacentUnit;
        var actor = state.Combatants.Single(unit => unit.UnitId == command.ActorId);
        var target = state.Combatants.Single(unit => unit.UnitId == command.TargetId);
        state.TurnBattle.Battle.TryGetUnit(actor.UnitId, out var actorBody);
        state.TurnBattle.Battle.TryGetUnit(target.UnitId, out var targetBody);
        TilePosition from = actorBody!.Position, to = targetBody!.Position;
        long dx = (long)to.X - from.X, dy = (long)to.Y - from.Y;
        var action = TurnBattleSimulator.ConfirmAct(state.TurnBattle, actor.UnitId);
        if (!action.Accepted) return new(false, AttackCommandRejection.None, action.TurnRejection, action.MovementRejection, state, []);

        var calculation = PhysicalDamageRules.Calculate(ability, actor.PhysicalAttack, actor.Brave, target.Brave,
            target.ClassPhysicalEvade, dx, dy, target.Facing, AbilityFormulaMode.Execution, state.TurnBattle.Turns.RngState);
        if (!calculation.Accepted) return Reject(AttackCommandRejection.RandomDrawCountExhausted);
        var result = calculation.Outcome!;
        var rng = calculation.RngState;
        bool hit = result.Hit, attempted = result.KnockbackAttempted;
        int damage = result.Damage;

        int hp = hit ? Math.Max(0, target.Hp - damage) : target.Hp;
        bool ko = target.Hp > 0 && hp == 0;
        TilePosition destination = to;
        if (attempted && !ko)
        {
            destination = KnockbackRules.KnockbackCalculation(action.State.Battle, actor.UnitId, target.UnitId) ?? to;
        }
        var nextTarget = target.WithResult(hp, target.Facing, ko ? (target.KoCount + 1) & 255 : target.KoCount);
        var bodies = action.State.Battle.Units.Select(unit => unit.Id == target.UnitId && destination != to
            ? new BattleUnitState(unit.Id, destination, unit.MovementProfile, unit.HasMoved) : unit);
        var battle = new BattleState(action.State.Battle.Map, bodies, rng);
        var turns = new TurnCycleState(action.State.Turns.Units, rng, action.State.Turns.Ticks, action.State.Turns.Phase);
        var next = new AttackBattleSnapshot(new(battle, turns),
            state.Combatants.Select(unit => unit.UnitId == target.UnitId ? nextTarget : unit));
        var events = action.Events.Select(ev => (AttackResultEvent)new AttackTurnBattleEvent(ev)).ToList();
        if (intendedTarget is { } original) events.Add(new ThrowStoneTargetInterceptedEvent(actor.UnitId, command.AbilityId, original, target.UnitId));
        events.Add(adjacentEvents ? new DashResolvedEvent(actor.UnitId, command.AbilityId, target.UnitId, damage, attempted)
            : new ThrowStoneResolvedEvent(actor.UnitId, command.AbilityId, target.UnitId, hit, damage, attempted));
        if (hp != target.Hp) events.Add(new AttackHpChangedEvent(target.UnitId, target.Hp, hp));
        if (nextTarget.Status != target.Status) events.Add(new AttackStatusChangedEvent(target.UnitId, target.Status, nextTarget.Status));
        if (ko) events.Add(new AttackKnockedOutEvent(target.UnitId, nextTarget.KoCount));
        if (destination != to) events.Add(adjacentEvents ? new DashKnockbackEvent(target.UnitId, to, destination) : new ThrowStoneKnockbackEvent(target.UnitId, to, destination));
        return new(true, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, next, events.ToImmutableArray());

        AttackBattleResult Reject(AttackCommandRejection reason) =>
            new(false, reason, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
    }

}
