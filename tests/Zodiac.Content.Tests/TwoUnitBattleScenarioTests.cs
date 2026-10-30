using System.Text.Json;
using Zodiac.Content.Samples;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class TwoUnitBattleScenarioTests
{
    private static BattleUnitId A => SyntheticTwoUnitBattle.FirstUnitId;
    private static BattleUnitId B => SyntheticTwoUnitBattle.SecondUnitId;

    [Fact]
    public void Authored_sequence_exercises_adjacent_movement_wait_both_attacks_and_first_KO_completion()
    {
        var definition = SyntheticTwoUnitBattle.Create();
        Assert.Equal(BattleMovementPolicy.OrdinaryAdjacentStepPrototype, definition.MovementPolicy);
        var state = definition.Start();
        var initial = Snapshot(state);
        uint[] words = [1103527590, 1103527590, 3655513600, 2633739833, 2937909499, 2937909499,
            2111915288, 2111915288, 2111915288, 3297241969, 3297241969, 2473755763];
        ulong[] draws = [1, 1, 7, 8, 14, 14, 15, 15, 15, 16, 16, 22];
        ulong[] ticks = [10, 10, 10, 10, 10, 10, 18, 18, 18, 20, 20, 20];
        int[] hpA = [50, 50, 50, 50, 25, 25, 25, 25, 25, 25, 25, 25];
        int[] hpB = [50, 50, 25, 25, 25, 25, 25, 25, 25, 25, 25, 0];
        int[] ctA = [0, 0, 0, 0, 0, 0, 80, 80, 80, 0, 0, 0];
        int[] ctB = [100, 100, 100, 0, 0, 20, 0, 0, 20, 40, 40, 40];
        var events = new List<TwoUnitBattleEvent>();
        for (int i = 0; i < SyntheticTwoUnitBattle.Commands.Length; i++)
        {
            var before = Snapshot(state);
            var result = TwoUnitBattleScenario.Apply(state, SyntheticTwoUnitBattle.Commands[i]);
            Assert.True(result.Accepted);
            Assert.Equal(before, Snapshot(state));
            Assert.NotSame(state, result.State);
            Assert.Equal(new FftRandomState(words[i], draws[i]), result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(result.State.Battle.TurnBattle.Turns.RngState, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(ticks[i], result.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(hpA[i], Combat(result.State, A).Hp); Assert.Equal(hpB[i], Combat(result.State, B).Hp);
            Assert.Equal(ctA[i], result.State.Battle.TurnBattle.Turns.Units.Single(unit => unit.Id == A).Ct);
            Assert.Equal(ctB[i], result.State.Battle.TurnBattle.Turns.Units.Single(unit => unit.Id == B).Ct);
            Assert.Equal(i == 11 ? TwoUnitBattleOutcome.FirstUnitWon : TwoUnitBattleOutcome.InProgress, result.State.Outcome);
            if (i < 11) { Assert.Null(result.State.WinnerId); Assert.Empty(result.Events.OfType<TwoUnitBattleCompletedEvent>()); }
            if (i is 2 or 11)
            {
                Assert.IsType<TurnChoiceConsumedEvent>(Clock(result.Events).ElementAt(0));
                Assert.IsType<TurnEndedEvent>(Clock(result.Events).ElementAt(1));
                Assert.IsType<AttackResolvedEvent>(Assert.IsType<TwoUnitBattleTransitionEvent>(result.Events[2]).Event);
            }
            events.AddRange(result.Events);
            state = result.State;
        }
        Assert.Equal(initial, Snapshot(definition.Start()));
        Assert.Equal(A, state.WinnerId);
        Assert.Equal(new TilePosition(0, 0, 0), state.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == A).Position);
        Assert.Equal(new TilePosition(1, 0, 0), state.Battle.TurnBattle.Battle.Units.Single(unit => unit.Id == B).Position);
        Assert.Equal(AttackUnitStatus.Dead, Combat(state, B).Status); Assert.Equal(1, Combat(state, B).KoCount);
        Assert.Equal(TurnCyclePhase.PickUnit, state.Battle.TurnBattle.Turns.Phase);
        Assert.All(state.Battle.TurnBattle.Turns.Units, unit => Assert.False(unit.HasTurn));
        Assert.Equal(new TwoUnitBattleCompletedEvent(definition.Id, A, B), events[^1]);
        Assert.IsType<AttackKnockedOutEvent>(Assert.IsType<TwoUnitBattleTransitionEvent>(events[^2]).Event);
        Assert.Equal(new[] { new AttackResolvedEvent(A, B, true, false, 25), new AttackResolvedEvent(B, A, true, false, 25), new AttackResolvedEvent(A, B, true, false, 25) },
            events.OfType<TwoUnitBattleTransitionEvent>().Select(ev => ev.Event).OfType<AttackResolvedEvent>().ToArray());
        Assert.Equal(new[] { A, B, B, A }, Clock(events).OfType<TurnReadyEvent>().Select(ev => ev.UnitId));
        Assert.Equal(new uint[] { 16838, 7419, 32225, 17543 }, Clock(events).OfType<TurnReadyEvent>().Select(ev => ev.UpkeepRoll));
        Assert.Equal(Enumerable.Range(1, 20).Select(value => (ulong)value), Clock(events).OfType<TurnClockAdvancedEvent>().Select(ev => ev.Tick));
        Assert.Equal(new[] { 0, 20, 20, 0 }, Clock(events).OfType<TurnEndedEvent>().Select(ev => ev.CtAfterCompletion));
    }

    [Fact]
    public void Restart_replays_every_complete_snapshot_and_ordered_event_identically()
    {
        var definition = SyntheticTwoUnitBattle.Create();
        var first = Replay(definition.Start());
        var restarted = definition.Start();
        Assert.NotSame(first.Final, restarted);
        Assert.Equal(new FftRandomState(1, 0), restarted.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(0ul, restarted.Battle.TurnBattle.Turns.Ticks);
        Assert.All(restarted.Battle.Combatants, unit => Assert.Equal(50, unit.Hp));
        var second = Replay(restarted);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(Snapshot(first.Final), Snapshot(second.Final));
        // Independently composed content produces the same full replay as restarting the first definition.
        Assert.Equal(first.Trace, Replay(SyntheticTwoUnitBattle.Create().Start()).Trace);
    }

    [Fact]
    public void Every_command_after_completion_returns_the_same_terminal_state_without_events_or_draws()
    {
        var completed = Replay(SyntheticTwoUnitBattle.Create().Start()).Final;
        string before = Snapshot(completed);
        TwoUnitBattleCommand[] commands = [new TwoUnitBattleCommand.Advance(), new TwoUnitBattleCommand.Move(A, new(0, 1, 0)),
            new TwoUnitBattleCommand.Wait(A), new TwoUnitBattleCommand.Attack(B, A), new UnknownCommand()];
        foreach (var command in commands)
        {
            var result = TwoUnitBattleScenario.Apply(completed, command);
            Assert.False(result.Accepted); Assert.Equal(TwoUnitBattleRejection.AlreadyCompleted, result.ScenarioRejection);
            Assert.Same(completed, result.State); Assert.Empty(result.Events);
            Assert.Equal(before, Snapshot(completed));
        }
    }

    [Theory]
    [InlineData("range")] [InlineData("occupied")] [InlineData("unreachable")]
    [InlineData("inactive")] [InlineData("unknown")] [InlineData("advance")] [InlineData("command")]
    public void Invalid_commands_do_not_corrupt_the_remaining_scenario(string kind)
    {
        var definition = SyntheticTwoUnitBattle.Create();
        var state = TwoUnitBattleScenario.Apply(definition.Start(), new TwoUnitBattleCommand.Advance()).State;
        var before = Snapshot(state);
        TwoUnitBattleCommand command = kind switch
        {
            "range" => new TwoUnitBattleCommand.Attack(A, B),
            "occupied" => new TwoUnitBattleCommand.Move(A, new(1, 1, 0)),
            "unreachable" => new TwoUnitBattleCommand.Move(A, new(2, 0, 0)),
            "inactive" => new TwoUnitBattleCommand.Wait(B),
            "unknown" => new TwoUnitBattleCommand.Attack(new("sample:unit/missing"), B),
            "advance" => new TwoUnitBattleCommand.Advance(),
            _ => new UnknownCommand(),
        };
        var rejected = TwoUnitBattleScenario.Apply(state, command);
        Assert.False(rejected.Accepted); Assert.Same(state, rejected.State); Assert.Empty(rejected.Events);
        Assert.Equal(before, Snapshot(state));
        if (kind == "range") Assert.Equal(AttackCommandRejection.OutOfRange, rejected.AttackRejection);
        if (kind == "occupied") Assert.Equal(BattleTransitionRejection.DestinationOccupied, rejected.MovementRejection);
        if (kind == "unreachable") Assert.Equal(BattleTransitionRejection.DestinationNotReachable, rejected.MovementRejection);
        if (kind == "inactive") Assert.Equal(TurnCycleRejection.InactiveUnit, rejected.TurnRejection);
        if (kind == "unknown") Assert.Equal(AttackCommandRejection.UnknownActor, rejected.AttackRejection);
        if (kind == "advance") Assert.Equal(TurnCycleRejection.TurnAlreadyActive, rejected.TurnRejection);
        if (kind == "command") Assert.Equal(TwoUnitBattleRejection.UnsupportedCommand, rejected.ScenarioRejection);
        foreach (var valid in SyntheticTwoUnitBattle.Commands.Skip(1))
        {
            var next = TwoUnitBattleScenario.Apply(state, valid); Assert.True(next.Accepted); state = next.State;
        }
        Assert.Equal(TwoUnitBattleOutcome.FirstUnitWon, state.Outcome);
        Assert.Equal(new FftRandomState(2473755763, 22), state.Battle.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Second_unit_can_win_in_an_authored_low_HP_variant()
    {
        var basis = SyntheticTwoUnitBattle.Create();
        var units = basis.InitialBattle.Combatants.Select(unit => unit.UnitId == A
            ? new AttackUnitState(A, 25, 25, 0, 0, unit.PhysicalAttack, unit.ClassPhysicalEvade, 0, 0, 0,
                unit.Brave, unit.ZodiacSign, unit.Facing, AttackUnitStatus.None, 0, unit.RightHandWeaponId) : unit);
        var definition = new TwoUnitBattleDefinition(basis.Id, A, B, new(basis.InitialBattle.TurnBattle, units), basis.Weapons.Values);
        var state = definition.Start(); TwoUnitBattleResult? result = null;
        foreach (var command in SyntheticTwoUnitBattle.Commands.Take(5))
        { result = TwoUnitBattleScenario.Apply(state, command); Assert.True(result.Accepted); state = result.State; }
        Assert.Equal(TwoUnitBattleOutcome.SecondUnitWon, state.Outcome); Assert.Equal(B, state.WinnerId);
        Assert.Equal(new FftRandomState(2937909499, 14), state.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(new TwoUnitBattleCompletedEvent(basis.Id, B, A), result!.Events[^1]);
    }

    [Fact]
    public void Movement_policy_is_preflighted_before_start_including_all_surface_costs_and_numeric_bounds()
    {
        var basis = SyntheticTwoUnitBattle.Create();
        var original = basis.InitialBattle.TurnBattle;
        var surface = new TerrainSurfaceId("sample:terrain/ground");
        foreach (string invalid in new[] { "jump", "missing-cost", "cost-overflow" })
        {
            var profile = new UnitMovementProfile(1, invalid == "jump" ? 256 : 2, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
                invalid == "missing-cost" ? [] : [new(surface, new TerrainMovementCost(invalid == "cost-overflow" ? int.MaxValue : 1))]);
            var units = original.Battle.Units.Select(unit => unit.Id == A ? new BattleUnitState(A, unit.Position, profile) : unit);
            var snapshot = new AttackBattleSnapshot(new(new(original.Battle.Map, units, original.Battle.RngState), original.Turns), basis.InitialBattle.Combatants);
            void Create() => new TwoUnitBattleDefinition(basis.Id, A, B, snapshot, basis.Weapons.Values);
            if (invalid == "missing-cost") Assert.Throws<ArgumentException>(Create);
            else Assert.Throws<NotSupportedException>(Create);
        }
    }

    [Fact]
    public void Missing_surface_cost_outside_initial_move_ranges_is_rejected_at_setup()
    {
        var basis = SyntheticTwoUnitBattle.Create();
        var surface = new TerrainSurfaceId("sample:terrain/ground");
        var missing = new TerrainSurfaceId("sample:terrain/missing");
        var tiles = Enumerable.Repeat(new BattleTile(surface, 10), 12).ToArray();
        tiles[5] = new BattleTile(missing, 10); // Both units are initially too far away to inspect this via range traversal.
        var map = new BattleMapDefinition(6, 2, 1, tiles);
        var original = basis.InitialBattle.TurnBattle;
        var snapshot = new AttackBattleSnapshot(new(new(map, original.Battle.Units, original.Battle.RngState), original.Turns), basis.InitialBattle.Combatants);
        Assert.Contains(missing.Value, Assert.Throws<ArgumentException>(() =>
            new TwoUnitBattleDefinition(basis.Id, A, B, snapshot, basis.Weapons.Values)).Message);
    }

    [Fact]
    public void Setup_rejects_ambiguous_units_weapons_and_nonstartup_state_and_snapshots_caller_collection()
    {
        var basis = SyntheticTwoUnitBattle.Create();
        Assert.Throws<ArgumentException>(() => new TwoUnitBattleDefinition(basis.Id, A, A, basis.InitialBattle, basis.Weapons.Values));
        Assert.Throws<ArgumentException>(() => new TwoUnitBattleDefinition(basis.Id, A, new("sample:unit/absent"), basis.InitialBattle, basis.Weapons.Values));
        Assert.Throws<ArgumentException>(() => new TwoUnitBattleDefinition(basis.Id, A, B, basis.InitialBattle, []));
        var weapon = basis.Weapons.Values.Single();
        Assert.Throws<ArgumentException>(() => new TwoUnitBattleDefinition(basis.Id, A, B, basis.InitialBattle, [weapon, weapon]));
        var active = TwoUnitBattleScenario.Apply(basis.Start(), new TwoUnitBattleCommand.Advance()).State;
        Assert.Throws<ArgumentException>(() => new TwoUnitBattleDefinition(basis.Id, A, B, active.Battle, basis.Weapons.Values));
        var input = new List<BoundedSwordWeapon> { weapon };
        var copied = new TwoUnitBattleDefinition(basis.Id, A, B, basis.InitialBattle, input); input.Clear();
        Assert.Single(copied.Weapons);
        Assert.Equal(TwoUnitBattleOutcome.FirstUnitWon, Replay(copied.Start()).Final.Outcome);
    }

    private sealed record UnknownCommand : TwoUnitBattleCommand;
    private static AttackUnitState Combat(TwoUnitBattleState state, BattleUnitId id) => state.Battle.Combatants.Single(unit => unit.UnitId == id);
    private static IEnumerable<TurnCycleEvent> Clock(IEnumerable<TwoUnitBattleEvent> events) => events
        .OfType<TwoUnitBattleTransitionEvent>().Select(ev => ev.Event).OfType<AttackTurnBattleEvent>()
        .Select(ev => ev.Event).OfType<TurnBattleClockEvent>().Select(ev => ev.Event);

    private static (TwoUnitBattleState Final, string[] Trace, TwoUnitBattleEvent[] Events) Replay(TwoUnitBattleState state)
    {
        var trace = new List<string> { Snapshot(state) }; var events = new List<TwoUnitBattleEvent>();
        foreach (var command in SyntheticTwoUnitBattle.Commands)
        {
            var result = TwoUnitBattleScenario.Apply(state, command); Assert.True(result.Accepted);
            state = result.State; trace.Add(Snapshot(state)); events.AddRange(result.Events);
        }
        return (state, trace.ToArray(), events.ToArray());
    }

    private static string Snapshot(TwoUnitBattleState state)
    {
        var battle = state.Battle.TurnBattle.Battle; var map = battle.Map;
        var tiles = new List<BattleTile>();
        for (int layer = 0; layer < map.LayerCount; layer++)
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++) { map.TryGetTile(new(x, y, layer), out var tile); tiles.Add(tile); }
        return JsonSerializer.Serialize(new
        {
            state.Definition.Id, state.Outcome, state.WinnerId, state.Definition.MovementPolicy,
            weapons = state.Definition.Weapons.Values.OrderBy(weapon => weapon.Id.Value, StringComparer.Ordinal),
            map.Width, map.Depth, map.LayerCount, tiles, battle.RngState, state.Battle.TurnBattle.Turns, state.Battle.Combatants,
            units = battle.Units.Select(unit => new
            {
                unit.Id, unit.Position, unit.HasMoved, unit.MovementProfile.Move, unit.MovementProfile.Jump,
                unit.MovementProfile.BodyHeight, unit.MovementProfile.Mode, unit.MovementProfile.Traits,
                costs = unit.MovementProfile.TerrainCosts.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal).Select(pair => new { id = pair.Key.Value, pair.Value }),
            }),
        });
    }
}
