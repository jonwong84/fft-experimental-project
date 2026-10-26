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

/// <summary>Pure settled Throw Stone transition; no obstruction, falling, rewards, animation or save format.</summary>
/// <remarks>Source trace/limits: docs/architecture/zod-108-throw-stone-action.md, decomp 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class TargetedAbilityBattleSimulator
{
    public static AttackBattleResult Apply(AttackBattleSnapshot state, UseTargetedAbilityCommand command, TargetedAbilityBattleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(profile);
        var actor = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.ActorId);
        if (actor is null) return Reject(AttackCommandRejection.UnknownActor);
        var target = state.Combatants.FirstOrDefault(unit => unit.UnitId == command.TargetId);
        if (target is null) return Reject(AttackCommandRejection.UnknownTarget);
        if (state.TurnBattle.Turns.ActiveUnitId != actor.UnitId) return Reject(AttackCommandRejection.InactiveActor);
        if (state.TurnBattle.Turns.Units.Single(unit => unit.Id == actor.UnitId).ActionTaken)
            return Reject(AttackCommandRejection.ActionAlreadyTaken);
        if (actor.UnitId == target.UnitId) return Reject(AttackCommandRejection.SelfTarget);
        if (target.Status == AttackUnitStatus.Dead) return Reject(AttackCommandRejection.DeadTarget);
        if (!profile.Abilities.TryGetValue(command.AbilityId, out var ability)) return Reject(AttackCommandRejection.UnknownAbility);
        if (!profile.Units.TryGetValue(actor.UnitId, out var grant) || !grant.Abilities.Contains(ability.Id))
            return Reject(AttackCommandRejection.AbilityNotGranted);
        try { profile.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return Reject(AttackCommandRejection.UnsupportedSetup); }

        state.TurnBattle.Battle.TryGetUnit(actor.UnitId, out var actorBody);
        state.TurnBattle.Battle.TryGetUnit(target.UnitId, out var targetBody);
        TilePosition from = actorBody!.Position, to = targetBody!.Position;
        long dx = (long)to.X - from.X, dy = (long)to.Y - from.Y;
        if (Math.Abs(dx) + Math.Abs(dy) > ability.Range) return Reject(AttackCommandRejection.OutOfRange);
        var action = TurnBattleSimulator.ConfirmAct(state.TurnBattle, actor.UnitId);
        if (!action.Accepted) return new(false, AttackCommandRejection.None, action.TurnRejection, action.MovementRejection, state, []);

        var rng = state.TurnBattle.Turns.RngState;
        uint Draw()
        {
            var draw = new FftBiosRandom().Next(rng); rng = draw.State; return draw.Value;
        }
        bool hit = true, attempted = false;
        int damage = 0;
        try
        {
            int classEvade = IsFront(dx, dy, target.Facing) ? target.ClassPhysicalEvade : 0;
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
        events.Add(new ThrowStoneResolvedEvent(actor.UnitId, ability.Id, target.UnitId, hit, damage, attempted));
        if (hp != target.Hp) events.Add(new AttackHpChangedEvent(target.UnitId, target.Hp, hp));
        if (nextTarget.Status != target.Status) events.Add(new AttackStatusChangedEvent(target.UnitId, target.Status, nextTarget.Status));
        if (ko) events.Add(new AttackKnockedOutEvent(target.UnitId, nextTarget.KoCount));
        if (destination != to) events.Add(new ThrowStoneKnockbackEvent(target.UnitId, to, destination));
        return new(true, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, next, events.ToImmutableArray());

        AttackBattleResult Reject(AttackCommandRejection reason) =>
            new(false, reason, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
    }

    private static bool IsFront(long dx, long dy, BattleFacing facing)
    {
        bool horizontal = dx > 0 && facing == BattleFacing.West || dx < 0 && facing == BattleFacing.East;
        bool vertical = dy > 0 && facing == BattleFacing.South || dy < 0 && facing == BattleFacing.North;
        return Math.Abs(dx) > Math.Abs(dy) ? horizontal : Math.Abs(dy) > Math.Abs(dx) ? vertical : horizontal || vertical;
    }
}
