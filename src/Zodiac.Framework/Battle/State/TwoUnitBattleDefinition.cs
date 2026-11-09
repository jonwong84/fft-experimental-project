using System.Collections.Immutable;
using Zodiac.Framework.Formulas;

namespace Zodiac.Framework;

/// <summary>Authored two-unit setup. First KO ends this scenario; this is not an original FFT victory rule.</summary>
public sealed class TwoUnitBattleDefinition
{
    public AssetId Id { get; }
    public BattleUnitId FirstUnitId { get; }
    public BattleUnitId SecondUnitId { get; }
    public AttackBattleSnapshot InitialBattle { get; }
    public ImmutableDictionary<AssetId, BoundedSwordWeapon> Weapons { get; }
    public ImmediateAbilityBattleProfile? ImmediateAbilities { get; }
    public TargetedAbilityBattleProfile? TargetedAbilities { get; }
    public BattleMovementPolicy MovementPolicy => BattleMovementPolicy.OrdinaryAdjacentStepPrototype;

    public TwoUnitBattleDefinition(AssetId id, BattleUnitId firstUnitId, BattleUnitId secondUnitId,
        AttackBattleSnapshot initialBattle, IEnumerable<BoundedSwordWeapon> weapons)
        : this(id, firstUnitId, secondUnitId, initialBattle, weapons, null) { }

    public TwoUnitBattleDefinition(AssetId id, BattleUnitId firstUnitId, BattleUnitId secondUnitId,
        AttackBattleSnapshot initialBattle, IEnumerable<BoundedSwordWeapon> weapons, ImmediateAbilityBattleProfile? immediateAbilities)
        : this(id, firstUnitId, secondUnitId, initialBattle, weapons, immediateAbilities, null) { }

    public TwoUnitBattleDefinition(AssetId id, BattleUnitId firstUnitId, BattleUnitId secondUnitId,
        AttackBattleSnapshot initialBattle, IEnumerable<BoundedSwordWeapon> weapons, ImmediateAbilityBattleProfile? immediateAbilities,
        TargetedAbilityBattleProfile? targetedAbilities)
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
            if (!FormulaDamageBounds.FitsSigned16Range(0, (2L * Math.Max(1, unit.PhysicalAttack) - 1) * weapon.Power))
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
        immediateAbilities?.ValidateSetup(initialBattle, Weapons);
        targetedAbilities?.ValidateState(initialBattle);
        if (immediateAbilities is not null && targetedAbilities is not null &&
            immediateAbilities.Abilities.Keys.Any(targetedAbilities.Abilities.ContainsKey))
            throw new ArgumentException("An ability ID cannot select both self and targeted effects.");
        ImmediateAbilities = immediateAbilities;
        TargetedAbilities = targetedAbilities;
    }

    /// <summary>Fresh lifecycle state over the original immutable setup and RNG; also the explicit restart operation.</summary>
    public TwoUnitBattleState Start() => new(this, InitialBattle, TwoUnitBattleOutcome.InProgress);

    /// <summary>Restores a command-boundary snapshot against this definition's immutable inputs. No I/O or RNG draws.</summary>
    public TwoUnitBattleState Restore(AttackBattleSnapshot battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        if (ImmediateAbilities is not null || TargetedAbilities is not null)
            throw new NotSupportedException("Ability-enabled battle restore requires the ZOD-106 persistence profile.");
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
