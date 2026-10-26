using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Zodiac.Framework;

namespace Fft.Client.Presentation;

public enum BattleInteractionMode { Inspect, Move, Attack }

/// <summary>Presentation choices around pure scenario commands; no independent simulation rules.</summary>
public sealed class BattleInteraction
{
    private readonly TwoUnitBattleDefinition definition;
    public AssetId? SelfAbilityId { get; }
    public bool HasSelfAbility => SelfAbilityId is not null;
    public bool CanUseSelfAbility => CanProbe && SelfAbilityId is { } ability &&
        TwoUnitBattleScenario.Apply(State, new TwoUnitBattleCommand.UseAbility(SelectedUnit!.Value, ability)).Accepted;
    public TwoUnitBattleState State { get; private set; }
    public BattleUnitId? SelectedUnit { get; private set; }
    public BattleInteractionMode Mode { get; private set; }
    public ImmutableArray<TwoUnitBattleCommand> Journal { get; private set; } = [];
    public TwoUnitBattleResult? LastResult { get; private set; }
    public string Status { get; private set; } = "ADVANCE TO START";
    public bool StatusIsError { get; private set; }

    public BattleInteraction(TwoUnitBattleDefinition definition, AssetId? selfAbilityId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        this.definition = definition;
        if (selfAbilityId is { } ability && (definition.ImmediateAbilities is null || !definition.ImmediateAbilities.Abilities.ContainsKey(ability)))
            throw new ArgumentException("The self action must resolve an explicitly selected executable ability.", nameof(selfAbilityId));
        SelfAbilityId = selfAbilityId;
        State = definition.Start();
    }

    public void Select(BattleUnitId unitId)
    {
        if (!State.Battle.Combatants.Any(unit => unit.UnitId == unitId))
        {
            SelectedUnit = null; Status = "UNKNOWN UNIT"; StatusIsError = true; return;
        }
        SelectedUnit = unitId;
        StatusIsError = false;
        Status = $"{Name(unitId)} SELECTED / {TurnStatus()}";
    }

    public void SetMode(BattleInteractionMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
        StatusIsError = false;
        Status = mode switch
        {
            BattleInteractionMode.Move => "MOVE / CLICK DESTINATION",
            BattleInteractionMode.Attack => "ATTACK / CLICK TARGET",
            _ => TurnStatus(),
        };
    }

    public void ClickTile(TilePosition tile)
    {
        var occupant = State.Battle.TurnBattle.Battle.Units.FirstOrDefault(unit => unit.Position == tile);
        if (Mode == BattleInteractionMode.Inspect)
        {
            if (occupant is not null) Select(occupant.Id);
            else { SelectedUnit = null; Status = "NO UNIT SELECTED"; StatusIsError = false; }
            return;
        }
        if (SelectedUnit is not { } selected) { Status = "SELECT A UNIT FIRST"; StatusIsError = true; return; }
        if (Mode == BattleInteractionMode.Move) Submit(new TwoUnitBattleCommand.Move(selected, tile));
        else if (occupant is null) { Status = "NO TARGET ON TILE"; StatusIsError = true; }
        else Submit(new TwoUnitBattleCommand.Attack(selected, occupant.Id));
    }

    public void Advance() => Submit(new TwoUnitBattleCommand.Advance());

    public void UseSelfAbility()
    {
        if (SelfAbilityId is not { } ability) return;
        if (SelectedUnit is not { } selected) { Status = "SELECT A UNIT FIRST"; StatusIsError = true; return; }
        Submit(new TwoUnitBattleCommand.UseAbility(selected, ability));
    }

    public void Wait()
    {
        if (SelectedUnit is { } selected) Submit(new TwoUnitBattleCommand.Wait(selected));
        else { Status = "SELECT A UNIT FIRST"; StatusIsError = true; }
    }

    public void Restart()
    {
        State = definition.Start(); SelectedUnit = null; Mode = BattleInteractionMode.Inspect;
        Journal = []; LastResult = null; Status = "ADVANCE TO START"; StatusIsError = false;
    }

    public ImmutableArray<TilePosition> Moves
    {
        get
        {
            if (!CanProbe) return [];
            var map = State.Battle.TurnBattle.Battle.Map;
            var legal = ImmutableArray.CreateBuilder<TilePosition>();
            for (int layer = 0; layer < map.LayerCount; layer++)
            for (int y = 0; y < map.Depth; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var tile = new TilePosition(x, y, layer);
                if (TwoUnitBattleScenario.Apply(State, new TwoUnitBattleCommand.Move(SelectedUnit!.Value, tile)).Accepted)
                    legal.Add(tile);
            }
            return legal.ToImmutable();
        }
    }

    public ImmutableArray<BattleUnitId> Targets => !CanProbe ? [] : State.Battle.Combatants
        .Where(unit => TwoUnitBattleScenario.Apply(State, new TwoUnitBattleCommand.Attack(SelectedUnit!.Value, unit.UnitId)).Accepted)
        .Select(unit => unit.UnitId).ToImmutableArray();

    private bool CanProbe => State.Outcome == TwoUnitBattleOutcome.InProgress && SelectedUnit is { } selected
        && State.Battle.TurnBattle.Turns.ActiveUnitId == selected;

    private void Submit(TwoUnitBattleCommand command)
    {
        var result = TwoUnitBattleScenario.Apply(State, command);
        Journal = Journal.Add(command); LastResult = result;
        StatusIsError = !result.Accepted;
        if (!result.Accepted)
        {
            string reason = result.ScenarioRejection != TwoUnitBattleRejection.None ? result.ScenarioRejection.ToString()
                : result.AttackRejection != AttackCommandRejection.None ? result.AttackRejection.ToString()
                : result.TurnRejection != TurnCycleRejection.None ? result.TurnRejection.ToString()
                : result.MovementRejection.ToString();
            Status = Regex.Replace(reason, "(?<=[a-z])(?=[A-Z])", " ").ToUpperInvariant();
            return;
        }
        State = result.State;
        Mode = BattleInteractionMode.Inspect;
        if (command is TwoUnitBattleCommand.Advance) SelectedUnit = State.Battle.TurnBattle.Turns.ActiveUnitId;
        if (State.WinnerId is { } winner) { Status = $"{Name(winner)} WINS / FIRST KO"; return; }
        var attack = result.Events.OfType<TwoUnitBattleTransitionEvent>().Select(ev => ev.Event).OfType<AttackResolvedEvent>().FirstOrDefault();
        var abilityResult = result.Events.OfType<TwoUnitBattleTransitionEvent>().Select(ev => ev.Event).OfType<ImmediateAbilityResolvedEvent>().FirstOrDefault();
        Status = abilityResult is not null ? (abilityResult.PhysicalAttackBefore == abilityResult.PhysicalAttackAfter
            ? $"ACCUMULATE / PA {abilityResult.PhysicalAttackAfter} MAX / {TurnStatus()}"
            : $"ACCUMULATE / PA {abilityResult.PhysicalAttackBefore} TO {abilityResult.PhysicalAttackAfter} / {TurnStatus()}")
            : attack is not null ? (attack.Hit ? $"HIT {attack.Damage} / {TurnStatus()}" : $"MISS / {TurnStatus()}")
            : command is TwoUnitBattleCommand.Move ? $"MOVED / {TurnStatus()}"
            : command is TwoUnitBattleCommand.Wait ? $"WAIT / {TurnStatus()}" : TurnStatus();
    }

    private string Name(BattleUnitId unit) => unit == definition.FirstUnitId ? "A" : "B";
    private string TurnStatus() => State.WinnerId is { } winner ? $"{Name(winner)} WINS / FIRST KO"
        : State.Battle.TurnBattle.Turns.ActiveUnitId is { } active ? $"{Name(active)} TURN" : "ADVANCE FOR NEXT TURN";
}
