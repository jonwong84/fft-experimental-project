using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Explicit projection of Throw Stone's closed range-4, Y=2 immediate damage/knockback rule.</summary>
public sealed record BoundedThrowStoneAbility
{
    public AssetId Id { get; }
    public int Range => 4;
    public BoundedThrowStoneAbility(AssetId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("An ability ID is required.", nameof(id));
        Id = id;
    }
}

/// <summary>Explicit learned targeted actions; equipment PA bonuses are outside this profile.</summary>
public sealed class TargetedAbilityUnitGrant
{
    public BattleUnitId UnitId { get; }
    public ImmutableHashSet<AssetId> Abilities { get; }

    public TargetedAbilityUnitGrant(BattleUnitId unitId, IEnumerable<AssetId> abilities, int equipmentPhysicalAttackBonus)
    {
        if (string.IsNullOrWhiteSpace(unitId.Value)) throw new ArgumentException("A unit ID is required.", nameof(unitId));
        if (equipmentPhysicalAttackBonus != 0) throw new NotSupportedException("This profile requires zero equipment PA bonus.");
        ArgumentNullException.ThrowIfNull(abilities);
        var selected = ImmutableHashSet.CreateBuilder<AssetId>();
        foreach (var id in abilities)
            if (string.IsNullOrWhiteSpace(id.Value) || !selected.Add(id))
                throw new ArgumentException("Grants require distinct explicit ability IDs.", nameof(abilities));
        UnitId = unitId;
        Abilities = selected.ToImmutable();
    }
}

/// <summary>Immutable opt-in for two ordinary units on an explicitly selected open, dry ground surface.</summary>
/// <remarks>The caller certifies the selected surface as ordinary ground; raw original terrain IDs are not inferred.</remarks>
public sealed class TargetedAbilityBattleProfile
{
    public const string RulesId = "fft:rules/throw-stone-flat-v1";
    public TerrainSurfaceId OrdinaryGroundSurface { get; }
    public ImmutableDictionary<AssetId, BoundedThrowStoneAbility> Abilities { get; }
    public ImmutableDictionary<BattleUnitId, TargetedAbilityUnitGrant> Units { get; }

    public TargetedAbilityBattleProfile(TerrainSurfaceId ordinaryGroundSurface,
        IEnumerable<BoundedThrowStoneAbility> abilities, IEnumerable<TargetedAbilityUnitGrant> units)
    {
        if (string.IsNullOrWhiteSpace(ordinaryGroundSurface.Value))
            throw new ArgumentException("Select an ordinary ground surface explicitly.", nameof(ordinaryGroundSurface));
        ArgumentNullException.ThrowIfNull(abilities); ArgumentNullException.ThrowIfNull(units);
        var effects = ImmutableDictionary.CreateBuilder<AssetId, BoundedThrowStoneAbility>();
        foreach (var ability in abilities)
        {
            ArgumentNullException.ThrowIfNull(ability);
            if (!effects.TryAdd(ability.Id, ability)) throw new ArgumentException("Duplicate executable ability.", nameof(abilities));
        }
        var grants = ImmutableDictionary.CreateBuilder<BattleUnitId, TargetedAbilityUnitGrant>();
        foreach (var unit in units)
        {
            ArgumentNullException.ThrowIfNull(unit);
            if (!grants.TryAdd(unit.UnitId, unit)) throw new ArgumentException("Duplicate unit grant binding.", nameof(units));
            if (unit.Abilities.Any(id => !effects.ContainsKey(id))) throw new ArgumentException("Unresolved executable ability grant.", nameof(units));
        }
        OrdinaryGroundSurface = ordinaryGroundSurface;
        Abilities = effects.ToImmutable(); Units = grants.ToImmutable();
    }

    internal void ValidateState(AttackBattleSnapshot state)
    {
        state.ValidateSupportedGeometry();
        if (state.Combatants.Length != 2 || Units.Count != 2 || state.Combatants.Any(unit =>
                !Units.ContainsKey(unit.UnitId) || unit.PhysicalAttack is < 1 or > 99))
            throw new NotSupportedException("Exactly two ordinary zero-bonus units with current PA 1..99 are required.");
        var map = state.TurnBattle.Battle.Map;
        if (map.LayerCount != 1) throw new NotSupportedException("Only an open lower-layer rectangle is supported.");
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, 0), out var tile);
            if (tile.Surface != OrdinaryGroundSurface || tile.MovementFlags != BattleTileMovementFlags.None ||
                tile.Height != 10 || tile.HalfHeight != 0 || tile.WaterDepth != 0 ||
                tile.CeilingDepth != 0 || tile.Slope.ToPackedByte() != 0)
                throw new NotSupportedException("Throw Stone requires clear, dry, flat ordinary ground throughout the map.");
        }
    }
}

public sealed record UseTargetedAbilityCommand(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId);
public sealed record ThrowStoneResolvedEvent(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId,
    bool Hit, int Damage, bool KnockbackAttempted) : AttackResultEvent;
public sealed record ThrowStoneKnockbackEvent(BattleUnitId UnitId, TilePosition Before, TilePosition After) : AttackResultEvent;
public sealed record ThrowStoneTargetInterceptedEvent(BattleUnitId ActorId, AssetId AbilityId,
    BattleUnitId IntendedTargetId, BattleUnitId ActualTargetId) : AttackResultEvent;

/// <summary>Pure settled Throw Stone with separate pair and flat-roster admission; no falling, rewards, animation or save format.</summary>
/// <remarks>Source trace/limits: docs/architecture/zod-108-throw-stone-action.md, decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class TargetedAbilityBattleSimulator
{
    public static AttackBattleResult Apply(AttackBattleSnapshot state, UseTargetedAbilityCommand command, TargetedAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        var invalid = ValidateCommand(state, command, profile);
        if (invalid != AttackCommandRejection.None) return Reject(invalid);
        return ApplyAdmitted(state, command);
        AttackBattleResult Reject(AttackCommandRejection reason) => new(false, reason, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
    }

    /// <summary>Actual immediate roster Stone, including the source-selected first unit hit. Pair admission remains unchanged.</summary>
    public static AttackBattleResult ApplyRoster(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, FlatRosterBattleProfile roster, FlatRosterProjectileHeights heights)
    {
        var invalid = ValidateRoster(state, command, profile, roster, heights, out var actualTarget);
        if (invalid != AttackCommandRejection.None) return new(false, invalid, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
        return ApplyAdmitted(state, command with { TargetId = actualTarget }, actualTarget == command.TargetId ? null : command.TargetId);
    }

    /// <summary>Pure current-command validation and live projectile resolution; does not consume ACT or RNG.</summary>
    public static AttackCommandRejection ValidateRosterCommand(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, FlatRosterBattleProfile roster, FlatRosterProjectileHeights heights) =>
        ValidateRoster(state, command, profile, roster, heights, out _);

    private static AttackCommandRejection ValidateRoster(AttackBattleSnapshot state, UseTargetedAbilityCommand command,
        TargetedAbilityBattleProfile profile, FlatRosterBattleProfile roster, FlatRosterProjectileHeights heights, out BattleUnitId actualTarget)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(roster); ArgumentNullException.ThrowIfNull(heights); actualTarget = command.TargetId;
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
        if (Math.Abs(origin.X - to.X) + Math.Abs(origin.Y - to.Y) > 4) return AttackCommandRejection.OutOfRange;
        var path = FlatRosterDirectTrajectory.EvaluateToTarget(state, new(command.ActorId, command.TargetId, origin), roster, heights);
        if (!path.Accepted || path.Outcome!.ResolveTarget(command.TargetId) is not { } resolved) return AttackCommandRejection.UnsupportedSetup;
        actualTarget = resolved;
        return AttackCommandRejection.None;
    }

    /// <summary>Pure complete-roster effect/grant admission, independent of origin range/trajectory and current choices.</summary>
    public static AttackCommandRejection ValidateRosterAbilitySetup(AttackBattleSnapshot state, BattleUnitId actorId, AssetId abilityId,
        TargetedAbilityBattleProfile profile, FlatRosterBattleProfile roster)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(roster);
        if (!state.Combatants.Any(u => u.UnitId == actorId)) return AttackCommandRejection.UnknownActor;
        if (!profile.Abilities.ContainsKey(abilityId)) return AttackCommandRejection.UnknownAbility;
        if (!profile.Units.TryGetValue(actorId, out var grant) || !grant.Abilities.Contains(abilityId)) return AttackCommandRejection.AbilityNotGranted;
        try { roster.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return AttackCommandRejection.UnsupportedSetup; }
        return profile.OrdinaryGroundSurface != roster.Ground || profile.Units.Count != roster.Units.Count ||
            !profile.Units.Keys.ToHashSet().SetEquals(roster.Units.Keys) || state.Combatants.Any(u => u.Hp == 0 || u.PhysicalAttack is < 1 or > 99)
            ? AttackCommandRejection.UnsupportedSetup : AttackCommandRejection.None;
    }

    private static AttackBattleResult ApplyAdmitted(AttackBattleSnapshot state, UseTargetedAbilityCommand command, BattleUnitId? intendedTarget = null)
    {
        var actor = state.Combatants.Single(unit => unit.UnitId == command.ActorId);
        var target = state.Combatants.Single(unit => unit.UnitId == command.TargetId);
        state.TurnBattle.Battle.TryGetUnit(actor.UnitId, out var actorBody);
        state.TurnBattle.Battle.TryGetUnit(target.UnitId, out var targetBody);
        TilePosition from = actorBody!.Position, to = targetBody!.Position;
        long dx = (long)to.X - from.X, dy = (long)to.Y - from.Y;
        var action = TurnBattleSimulator.ConfirmAct(state.TurnBattle, actor.UnitId);
        if (!action.Accepted) return new(false, AttackCommandRejection.None, action.TurnRejection, action.MovementRejection, state, []);

        var rng = state.TurnBattle.Turns.RngState;
        uint Draw()
        {
            var draw = new RandomStateGenerator().Next(rng); rng = draw.State; return draw.Value;
        }
        bool hit = true, attempted = false;
        int damage = 0;
        try
        {
            int classEvade = ThrowStoneFormula.IsFront(dx, dy, target.Facing) ? target.ClassPhysicalEvade : 0;
            foreach (int evade in new[] { 0, 0, 0, classEvade })
                if (Draw() * 100 / 32768 < evade) { hit = false; break; }
            if (hit)
            {
                damage = ThrowStoneFormula.Damage(actor.PhysicalAttack, Draw());
                uint attackerRoll = Draw(), targetRoll = Draw();
                attempted = ThrowStoneFormula.AttemptsKnockback(actor.Brave, target.Brave, attackerRoll, targetRoll);
            }
        }
        catch (OverflowException) { return Reject(AttackCommandRejection.RandomDrawCountExhausted); }

        int hp = hit ? Math.Max(0, target.Hp - damage) : target.Hp;
        bool ko = target.Hp > 0 && hp == 0;
        TilePosition destination = to;
        if (attempted && !ko)
        {
            var candidate = Math.Abs(dx) > Math.Abs(dy)
                ? new TilePosition(to.X + Math.Sign(dx), to.Y, 0)
                : new TilePosition(to.X, to.Y + Math.Sign(dy), 0); // Y wins ties.
            if (action.State.Battle.Map.TryGetTile(candidate, out _) &&
                !action.State.Battle.Units.Any(unit => unit.Position == candidate))
                destination = candidate;
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
        events.Add(new ThrowStoneResolvedEvent(actor.UnitId, command.AbilityId, target.UnitId, hit, damage, attempted));
        if (hp != target.Hp) events.Add(new AttackHpChangedEvent(target.UnitId, target.Hp, hp));
        if (nextTarget.Status != target.Status) events.Add(new AttackStatusChangedEvent(target.UnitId, target.Status, nextTarget.Status));
        if (ko) events.Add(new AttackKnockedOutEvent(target.UnitId, nextTarget.KoCount));
        if (destination != to) events.Add(new ThrowStoneKnockbackEvent(target.UnitId, to, destination));
        return new(true, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, next, events.ToImmutableArray());

        AttackBattleResult Reject(AttackCommandRejection reason) =>
            new(false, reason, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
    }

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
        if (Math.Abs(dx) + Math.Abs(dy) > profile.Abilities[command.AbilityId].Range) return AttackCommandRejection.OutOfRange;
        return AttackCommandRejection.None;
    }

    /// <summary>Check an actor's ability binding and the bounded battle setup independently of turn availability and target range.</summary>
    /// <remarks>Pure setup admission for preparation queries. This does not authorize an ACT; ValidateCommand retains its choice/target guards.</remarks>
    public static AttackCommandRejection ValidateAbilitySetup(AttackBattleSnapshot state, BattleUnitId actorId, AssetId abilityId,
        TargetedAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(profile);
        if (!state.Combatants.Any(unit => unit.UnitId == actorId)) return AttackCommandRejection.UnknownActor;
        if (!profile.Abilities.TryGetValue(abilityId, out var ability)) return AttackCommandRejection.UnknownAbility;
        if (!profile.Units.TryGetValue(actorId, out var grant) || !grant.Abilities.Contains(ability.Id))
            return AttackCommandRejection.AbilityNotGranted;
        try { profile.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return AttackCommandRejection.UnsupportedSetup; }
        return AttackCommandRejection.None;
    }

}
