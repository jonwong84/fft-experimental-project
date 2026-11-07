namespace Zodiac.Framework;

/// <summary>Pure settled Throw Stone with separate pair and flat-roster admission; no falling, rewards, animation or save format.</summary>
/// <remarks>Source trace/limits: docs/architecture/zod-108-throw-stone-action.md, decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class TargetedAbilityBattleSimulator
{
    public static AttackBattleResult Apply(AttackBattleSnapshot state, UseTargetedAbilityCommand command, TargetedAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        var invalid = ValidateCommand(state, command, profile);
        if (invalid != AttackCommandRejection.None) return Reject(invalid);
        return PhysicalAbilityBattleSimulator.ApplyPhysical(state, command, profile.PhysicalProfile.Abilities[command.AbilityId], profile.PhysicalProfile.Delivery);
        AttackBattleResult Reject(AttackCommandRejection reason) => new(false, reason, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
    }

    /// <summary>Actual roster projectile delivery, retaining the source-selected first unit hit.</summary>
    public static AttackBattleResult ApplyRoster(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, FlatRosterBattleProfile roster, FlatRosterProjectileHeights heights) =>
        PhysicalAbilityBattleSimulator.ApplyRoster(state, command, (profile ?? throw new ArgumentNullException(nameof(profile))).PhysicalProfile, roster, heights);

    public static AttackCommandRejection ValidateRosterCommand(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, FlatRosterBattleProfile roster, FlatRosterProjectileHeights heights) =>
        PhysicalAbilityBattleSimulator.ValidateRosterCommand(state, command, (profile ?? throw new ArgumentNullException(nameof(profile))).PhysicalProfile, roster, heights);

    public static AttackCommandRejection ValidateRosterAbilitySetup(AttackBattleSnapshot state, BattleUnitId actorId, AssetId abilityId,
        TargetedAbilityBattleProfile profile, FlatRosterBattleProfile roster) =>
        PhysicalAbilityBattleSimulator.ValidateRosterAbilitySetup(state, actorId, abilityId, (profile ?? throw new ArgumentNullException(nameof(profile))).PhysicalProfile, roster);

    /// <summary>Read-only current-command admission shared by execution and AI projection; grants no authority.</summary>
    public static AttackCommandRejection ValidateCommand(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        var actor = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.ActorId);
        if (actor is null) return AttackCommandRejection.UnknownActor;
        var target = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.TargetId);
        if (target is null) return AttackCommandRejection.UnknownTarget;
        if (state.TurnBattle.Turns.ActiveUnitId != actor.UnitId) return AttackCommandRejection.InactiveActor;
        if (state.TurnBattle.Turns.Units.Single(unit => unit.Id == actor.UnitId).ActionTaken)
            return AttackCommandRejection.ActionAlreadyTaken;
        if (actor.UnitId == target.UnitId) return AttackCommandRejection.SelfTarget;
        if (target.Status == AttackUnitStatus.Dead) return AttackCommandRejection.DeadTarget;
        var setup = ValidateAbilitySetup(state, actor.UnitId, command.AbilityId, profile);
        if (setup != AttackCommandRejection.None) return setup;

        state.TurnBattle.Battle.TryGetUnit(actor.UnitId, out var actorBody);
        state.TurnBattle.Battle.TryGetUnit(target.UnitId, out var targetBody);
        TilePosition from = actorBody!.Position, to = targetBody!.Position;
        long dx = (long)to.X - from.X, dy = (long)to.Y - from.Y;
        if (Math.Abs(dx) + Math.Abs(dy) > profile.PhysicalProfile.Abilities[command.AbilityId].Range) return AttackCommandRejection.OutOfRange;
        return AttackCommandRejection.None;
    }

    /// <summary>Check an actor's ability binding and the bounded battle setup independently of turn availability and target range.</summary>
    /// <remarks>Pure setup admission for preparation queries. This does not authorize an ACT; ValidateCommand retains its choice/target guards.</remarks>
    public static AttackCommandRejection ValidateAbilitySetup(AttackBattleSnapshot state, BattleUnitId actorId, AssetId abilityId,
        TargetedAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(profile);
        if (!state.Combatants.Any(unit => unit.UnitId == actorId)) return AttackCommandRejection.UnknownActor;
        if (!profile.PhysicalProfile.Abilities.TryGetValue(abilityId, out var ability)) return AttackCommandRejection.UnknownAbility;
        if (!profile.Units.TryGetValue(actorId, out var grant) || !grant.Abilities.Contains(ability.Id))
            return AttackCommandRejection.AbilityNotGranted;
        try { profile.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return AttackCommandRejection.UnsupportedSetup; }
        return AttackCommandRejection.None;
    }

}
