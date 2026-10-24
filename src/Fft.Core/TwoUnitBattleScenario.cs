using System.Collections.Immutable;

namespace Fft.Core;

/// <summary>Authored two-unit setup. First KO ends this scenario; this is not an original FFT victory rule.</summary>
public sealed class TwoUnitBattleDefinition
{
    public AssetId Id { get; }
    public BattleUnitId FirstUnitId { get; }
    public BattleUnitId SecondUnitId { get; }
    public AttackBattleSnapshot InitialBattle { get; }
    public ImmutableDictionary<AssetId, BoundedSwordWeapon> Weapons { get; }
    public BattleMovementPolicy MovementPolicy => BattleMovementPolicy.OrdinaryAdjacentStepPrototype;

    public TwoUnitBattleDefinition(AssetId id, BattleUnitId firstUnitId, BattleUnitId secondUnitId,
        AttackBattleSnapshot initialBattle, IEnumerable<BoundedSwordWeapon> weapons)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("Scenario ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(initialBattle);
        ArgumentNullException.ThrowIfNull(weapons);
        initialBattle.ValidateInitialSetup();
        if (firstUnitId == secondUnitId || initialBattle.Combatants.Length != 2 ||
            !initialBattle.Combatants.Any(unit => unit.UnitId == firstUnitId) ||
            !initialBattle.Combatants.Any(unit => unit.UnitId == secondUnitId))
            throw new ArgumentException("Scenario requires exactly its two distinct named units.", nameof(initialBattle));
        if (initialBattle.TurnBattle.Turns.Phase != TurnCyclePhase.AddCt ||
            initialBattle.TurnBattle.Turns.Units.Any(unit => unit.HasTurn || unit.MovementTaken || unit.ActionTaken))
            throw new ArgumentException("Scenario must start before turn selection with both choices unused.", nameof(initialBattle));
        var selected = ImmutableDictionary.CreateBuilder<AssetId, BoundedSwordWeapon>();
        foreach (var weapon in weapons)
        {
            ArgumentNullException.ThrowIfNull(weapon);
            if (!selected.TryAdd(weapon.Id, weapon)) throw new ArgumentException($"Duplicate weapon '{weapon.Id}'.", nameof(weapons));
        }
        foreach (var unit in initialBattle.Combatants)
        {
            if (unit.RightHandWeaponId is not { } weaponId || !selected.TryGetValue(weaponId, out var weapon))
                throw new ArgumentException($"Unit '{unit.UnitId}' requires a selected sword.", nameof(weapons));
            if ((2 * Math.Max(1, unit.PhysicalAttack) - 1) * weapon.Power > short.MaxValue)
                throw new NotSupportedException("Scenario sword/PA exceeds the bounded signed-16 damage envelope.");
        }
        // Validate this scenario's selected movement policy at setup, including surfaces outside the
        // initial one-step range. These immutable inputs must remain valid after either unit moves.
        var map = initialBattle.TurnBattle.Battle.Map;
        foreach (var unit in initialBattle.TurnBattle.Battle.Units)
        {
            int maximumCost = 0;
            for (int layer = 0; layer < map.LayerCount; layer++)
            for (int y = 0; y < map.Depth; y++)
            for (int x = 0; x < map.Width; x++)
            {
                map.TryGetTile(new(x, y, layer), out var tile);
                if (tile.IsBlocked) continue;
                if (!unit.MovementProfile.TryGetTerrainCost(tile.Surface, out var cost))
                    throw new ArgumentException($"Unit '{unit.Id}' has no terrain cost for '{tile.Surface}'.", nameof(initialBattle));
                if (!cost.IsBlocked) maximumCost = Math.Max(maximumCost, cost.MovePoints);
            }
            if ((long)unit.Move + maximumCost > int.MaxValue)
                throw new NotSupportedException("Scenario movement costs exceed the safe managed addition envelope.");
            _ = OrdinaryAdjacentStepRange.Calculate(map, unit.Position, unit.MovementProfile,
                initialBattle.TurnBattle.Battle.Units.Where(other => other.Id != unit.Id).Select(other => other.Position).ToHashSet());
        }
        Id = id;
        FirstUnitId = firstUnitId;
        SecondUnitId = secondUnitId;
        InitialBattle = initialBattle;
        Weapons = selected.ToImmutable();
    }

    /// <summary>Fresh lifecycle state over the original immutable setup and RNG; also the explicit restart operation.</summary>
    public TwoUnitBattleState Start() => new(this, InitialBattle, TwoUnitBattleOutcome.InProgress);

    /// <summary>Restores a command-boundary snapshot against this definition's immutable inputs. No I/O or RNG draws.</summary>
    public TwoUnitBattleState Restore(AttackBattleSnapshot battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        var initial = InitialBattle.TurnBattle;
        var current = battle.TurnBattle;
        if (!ReferenceEquals(current.Battle.Map, initial.Battle.Map) ||
            current.Battle.Units.Count != initial.Battle.Units.Count)
            throw new ArgumentException("Restore must resolve the scenario's original map and units.", nameof(battle));
        battle.ValidateSupportedGeometry();
        foreach (var template in initial.Battle.Units)
        {
            if (!current.Battle.TryGetUnit(template.Id, out var unit) ||
                !ReferenceEquals(unit!.MovementProfile, template.MovementProfile))
                throw new ArgumentException($"Restore changed immutable movement inputs for '{template.Id}'.", nameof(battle));
            var turn = current.Turns.Units.Single(item => item.Id == template.Id);
            var originalTurn = initial.Turns.Units.Single(item => item.Id == template.Id);
            var combat = battle.Combatants.Single(item => item.UnitId == template.Id);
            var originalCombat = InitialBattle.Combatants.Single(item => item.UnitId == template.Id);
            if (turn.Speed != originalTurn.Speed || turn.TieKey != originalTurn.TieKey ||
                combat != originalCombat.WithResult(combat.Hp, combat.Facing, combat.KoCount))
                throw new ArgumentException($"Restore changed immutable combat/turn inputs for '{template.Id}'.", nameof(battle));
            int expectedKoCount = combat.Hp == 0 ? (originalCombat.KoCount + 1) & 255 : originalCombat.KoCount;
            if (combat.Hp > originalCombat.Hp || combat.KoCount != expectedKoCount)
                throw new ArgumentException("The bounded scenario has no healing, revival or prior KO processing.", nameof(battle));
            if (current.Turns.Phase == TurnCyclePhase.AddCt &&
                (unit.Position != template.Position || unit.HasMoved != template.HasMoved || turn != originalTurn || combat != originalCombat))
                throw new ArgumentException("AddCt is only a legal checkpoint at the initial setup.", nameof(battle));
        }
        if (current.Turns.Ticks < initial.Turns.Ticks || current.Turns.RngState.DrawCount < initial.Turns.RngState.DrawCount ||
            (current.Turns.Phase == TurnCyclePhase.AddCt &&
                (current.Turns.Ticks != initial.Turns.Ticks || current.Turns.RngState != initial.Turns.RngState)))
            throw new ArgumentException("Restore clock/RNG counters precede the setup or change its initial AddCt checkpoint.", nameof(battle));
        var first = battle.Combatants.Single(unit => unit.UnitId == FirstUnitId);
        var second = battle.Combatants.Single(unit => unit.UnitId == SecondUnitId);
        if (first.Hp == 0 && second.Hp == 0)
            throw new ArgumentException("Both units cannot be dead at a first-KO checkpoint.", nameof(battle));
        var outcome = first.Hp == 0 ? TwoUnitBattleOutcome.SecondUnitWon
            : second.Hp == 0 ? TwoUnitBattleOutcome.FirstUnitWon : TwoUnitBattleOutcome.InProgress;
        return new(this, battle, outcome);
    }
}

public enum TwoUnitBattleOutcome { InProgress, FirstUnitWon, SecondUnitWon }
public enum TwoUnitBattleRejection { None, AlreadyCompleted, UnsupportedCommand }

public sealed class TwoUnitBattleState
{
    public TwoUnitBattleDefinition Definition { get; }
    public AttackBattleSnapshot Battle { get; }
    public TwoUnitBattleOutcome Outcome { get; }
    public BattleUnitId? WinnerId => Outcome switch
    {
        TwoUnitBattleOutcome.FirstUnitWon => Definition.FirstUnitId,
        TwoUnitBattleOutcome.SecondUnitWon => Definition.SecondUnitId,
        _ => null,
    };

    internal TwoUnitBattleState(TwoUnitBattleDefinition definition, AttackBattleSnapshot battle, TwoUnitBattleOutcome outcome)
    { Definition = definition; Battle = battle; Outcome = outcome; }
}

public abstract record TwoUnitBattleCommand
{
    public sealed record Advance : TwoUnitBattleCommand;
    public sealed record Move(BattleUnitId UnitId, TilePosition Destination) : TwoUnitBattleCommand;
    public sealed record Attack(BattleUnitId ActorId, BattleUnitId TargetId) : TwoUnitBattleCommand;
    public sealed record Wait(BattleUnitId UnitId) : TwoUnitBattleCommand;
}

public abstract record TwoUnitBattleEvent;
public sealed record TwoUnitBattleTransitionEvent(AttackResultEvent Event) : TwoUnitBattleEvent;
public sealed record TwoUnitBattleCompletedEvent(AssetId ScenarioId, BattleUnitId WinnerId, BattleUnitId LoserId) : TwoUnitBattleEvent;

public sealed record TwoUnitBattleResult(bool Accepted, TwoUnitBattleRejection ScenarioRejection,
    AttackCommandRejection AttackRejection, TurnCycleRejection TurnRejection,
    BattleTransitionRejection MovementRejection, TwoUnitBattleState State, ImmutableArray<TwoUnitBattleEvent> Events);

/// <summary>Pure first-KO lifecycle over the established bounded movement, clock and attack rules.</summary>
public static class TwoUnitBattleScenario
{
    public static TwoUnitBattleResult Apply(TwoUnitBattleState state, TwoUnitBattleCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        // Completion takes precedence for every subsequent command; no clock/body/death processing runs.
        if (state.Outcome != TwoUnitBattleOutcome.InProgress) return Reject(state, TwoUnitBattleRejection.AlreadyCompleted);
        AttackBattleResult result;
        switch (command)
        {
            case TwoUnitBattleCommand.Advance:
                result = AttackBattleSimulator.Advance(state.Battle); break;
            case TwoUnitBattleCommand.Move move:
                result = AttackBattleSimulator.Move(state.Battle, new(move.UnitId, move.Destination), state.Definition.MovementPolicy); break;
            case TwoUnitBattleCommand.Wait wait:
                result = AttackBattleSimulator.Wait(state.Battle, wait.UnitId); break;
            case TwoUnitBattleCommand.Attack attack:
                var actor = state.Battle.Combatants.FirstOrDefault(unit => unit.UnitId == attack.ActorId);
                BoundedSwordWeapon? weapon = actor?.RightHandWeaponId is { } weaponId
                    ? state.Definition.Weapons.GetValueOrDefault(weaponId) : null;
                result = AttackBattleSimulator.Attack(state.Battle, new(attack.ActorId, attack.TargetId), weapon); break;
            default: return Reject(state, TwoUnitBattleRejection.UnsupportedCommand);
        }
        if (!result.Accepted)
            return new(false, TwoUnitBattleRejection.None, result.AttackRejection, result.TurnRejection, result.MovementRejection, state, []);

        var first = result.State.Combatants.Single(unit => unit.UnitId == state.Definition.FirstUnitId);
        var second = result.State.Combatants.Single(unit => unit.UnitId == state.Definition.SecondUnitId);
        var outcome = first.Hp == 0 ? TwoUnitBattleOutcome.SecondUnitWon : second.Hp == 0 ? TwoUnitBattleOutcome.FirstUnitWon : TwoUnitBattleOutcome.InProgress;
        var next = new TwoUnitBattleState(state.Definition, result.State, outcome);
        var events = result.Events.Select(ev => (TwoUnitBattleEvent)new TwoUnitBattleTransitionEvent(ev)).ToList();
        if (next.WinnerId is { } winner)
            events.Add(new TwoUnitBattleCompletedEvent(state.Definition.Id, winner,
                winner == state.Definition.FirstUnitId ? state.Definition.SecondUnitId : state.Definition.FirstUnitId));
        return new(true, TwoUnitBattleRejection.None, AttackCommandRejection.None, TurnCycleRejection.None,
            BattleTransitionRejection.None, next, events.ToImmutableArray());
    }

    private static TwoUnitBattleResult Reject(TwoUnitBattleState state, TwoUnitBattleRejection rejection) =>
        new(false, rejection, AttackCommandRejection.None, TurnCycleRejection.None, BattleTransitionRejection.None, state, []);
}
