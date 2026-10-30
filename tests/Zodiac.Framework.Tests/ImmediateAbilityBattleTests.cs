using Zodiac.Framework;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class ImmediateAbilityBattleTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Ability = new("sample:ability/accumulate");

    [Theory]
    [InlineData(1, 2)] [InlineData(5, 6)] [InlineData(98, 99)] [InlineData(99, 99)]
    public void Source_PA_vectors_publish_one_self_result_preserving_all_other_inputs(int beforePa, int afterPa)
    {
        var before = State(beforePa);
        var result = ImmediateAbilityBattleSimulator.Apply(before, new(A, Ability), Profile());
        Assert.True(result.Accepted);
        Assert.Equal(afterPa, result.State.Combatants[0].PhysicalAttack);
        Assert.Equal(beforePa, before.Combatants[0].PhysicalAttack);
        Assert.Same(before.Combatants[1], result.State.Combatants[1]);
        Assert.Same(before.TurnBattle.Battle.Map, result.State.TurnBattle.Battle.Map);
        Assert.Equal(before.TurnBattle.Battle.Units, result.State.TurnBattle.Battle.Units);
        Assert.Equal(before.TurnBattle.Turns.RngState, result.State.TurnBattle.Turns.RngState);
        Assert.Equal(before.TurnBattle.Turns.Ticks, result.State.TurnBattle.Turns.Ticks);
        Assert.Equal(50, result.State.Combatants[0].Hp);
        Assert.Equal(BattleFacing.South, result.State.Combatants[0].Facing);
        Assert.Equal(0, result.State.Combatants[0].Mp);
        var actor = result.State.TurnBattle.Turns.Units[0];
        Assert.True(actor.ActionTaken); Assert.False(actor.MovementTaken); Assert.True(actor.HasTurn);
        Assert.Equal(TurnCyclePhase.UnitTurn, result.State.TurnBattle.Turns.Phase);
        Assert.Equal(new TurnChoiceConsumedEvent(A, TurnCommandKind.ConfirmAct), Assert.IsType<AttackTurnBattleEvent>(result.Events[0]).Event is TurnBattleClockEvent clock ? clock.Event : null);
        Assert.Equal(new ImmediateAbilityResolvedEvent(A, Ability, beforePa, afterPa), result.Events[1]);
        if (beforePa == afterPa) Assert.Equal(2, result.Events.Length);
        else Assert.Equal(new AttackPhysicalAttackChangedEvent(A, beforePa, afterPa), result.Events[2]);
    }

    [Fact]
    public void Move_consumed_before_action_ends_turn_before_publishing_effect_events()
    {
        var result = ImmediateAbilityBattleSimulator.Apply(State(moved: true), new(A, Ability), Profile());
        Assert.True(result.Accepted);
        Assert.Null(result.State.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(TurnCyclePhase.PickUnit, result.State.TurnBattle.Turns.Phase);
        Assert.Equal(0, result.State.TurnBattle.Turns.Units[0].Ct);
        Assert.IsType<AttackTurnBattleEvent>(result.Events[0]);
        Assert.Equal(new TurnEndedEvent(A, 0, 0), Assert.IsType<TurnBattleClockEvent>(Assert.IsType<AttackTurnBattleEvent>(result.Events[1]).Event).Event);
        Assert.IsType<ImmediateAbilityResolvedEvent>(result.Events[2]);
        Assert.IsType<AttackPhysicalAttackChangedEvent>(result.Events[3]);
    }

    [Theory]
    [InlineData("unknown", AttackCommandRejection.UnknownActor)]
    [InlineData("inactive", AttackCommandRejection.InactiveActor)]
    [InlineData("acted", AttackCommandRejection.ActionAlreadyTaken)]
    [InlineData("ability", AttackCommandRejection.UnknownAbility)]
    [InlineData("grant", AttackCommandRejection.AbilityNotGranted)]
    [InlineData("pa", AttackCommandRejection.UnsupportedSetup)]
    [InlineData("water", AttackCommandRejection.UnsupportedSetup)]
    public void Invalid_commands_are_atomic(string kind, AttackCommandRejection expected)
    {
        var state = State(kind == "pa" ? 0 : 5, acted: kind == "acted", water: kind == "water");
        var command = new UseImmediateAbilityCommand(kind == "unknown" ? new("sample:unit/missing") : kind == "inactive" ? B : A,
            kind == "ability" ? new("sample:ability/unsupported") : Ability);
        var result = ImmediateAbilityBattleSimulator.Apply(state, command, Profile(kind != "grant"));
        Assert.False(result.Accepted); Assert.Equal(expected, result.AttackRejection);
        Assert.Same(state, result.State); Assert.Empty(result.Events);
    }

    [Fact]
    public void Probes_are_repeatable_at_exhausted_RNG_but_later_readiness_still_rejects()
    {
        var input = State(count: ulong.MaxValue);
        var first = ImmediateAbilityBattleSimulator.Apply(input, new(A, Ability), Profile());
        var probe = ImmediateAbilityBattleSimulator.Apply(input, new(A, Ability), Profile());
        Assert.True(first.Accepted); Assert.Equal(first.Events.ToArray(), probe.Events.ToArray());
        Assert.Equal(first.State.Combatants.ToArray(), probe.State.Combatants.ToArray());
        Assert.False(input.TurnBattle.Turns.Units[0].ActionTaken);
        Assert.Equal(5, input.Combatants[0].PhysicalAttack);
        var wait = AttackBattleSimulator.Wait(first.State, A);
        Assert.True(wait.Accepted);
        var advance = AttackBattleSimulator.Advance(wait.State);
        Assert.False(advance.Accepted); Assert.Equal(TurnCycleRejection.RandomDrawCountExhausted, advance.TurnRejection);
        Assert.Same(wait.State, advance.State); Assert.Empty(advance.Events);
    }

    [Theory] [InlineData(0)] [InlineData(100)] [InlineData(int.MaxValue)]
    public void Named_arithmetic_rejects_values_outside_its_source_envelope(int pa) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AccumulateFormula.IncreasePhysicalAttack(pa));

    [Fact]
    public void Grant_projections_snapshot_inputs_and_reject_unsupported_or_ambiguous_bindings()
    {
        var list = new List<AssetId> { Ability };
        var grant = new ImmediateAbilityUnitGrant(A, list, 0); list.Clear();
        Assert.Contains(Ability, grant.Abilities);
        Assert.Throws<NotSupportedException>(() => new ImmediateAbilityUnitGrant(A, [Ability], 1));
        Assert.Throws<ArgumentException>(() => new ImmediateAbilityUnitGrant(A, [Ability, Ability], 0));
        Assert.Throws<ArgumentException>(() => new ImmediateAbilityUnitGrant(A, [default], 0));
        Assert.Throws<ArgumentException>(() => new BoundedImmediateAbility(default));
        Assert.Throws<ArgumentException>(() => new ImmediateAbilityBattleProfile([], [grant]));
        Assert.Throws<ArgumentException>(() => new ImmediateAbilityBattleProfile([new(Ability), new(Ability)], []));
        Assert.Throws<ArgumentException>(() => new ImmediateAbilityBattleProfile([new(Ability)], [grant, grant]));
    }

    private static ImmediateAbilityBattleProfile Profile(bool grant = true) => new([new(Ability)],
        [new(A, grant ? [Ability] : [], 0), new(B, [], 0)]);

    private static AttackBattleSnapshot State(int pa = 5, bool moved = false, bool acted = false, ulong count = 0, bool water = false)
    {
        var rng = new FftRandomState(1, count);
        var tiles = Enumerable.Repeat(new BattleTile(10), 4).ToArray();
        if (water) tiles[0] = new(new TerrainSurfaceId("sample:terrain/ground"), 10, 0, 1, default);
        var map = new BattleMapDefinition(2, 2, 1, tiles);
        var turns = new TurnCycleState([new(A, 10, 0, 0, true, moved, acted), new(B, 10, 100, 1)], rng, phase: TurnCyclePhase.UnitTurn);
        return new(new(new(map, [new(A, new(0, 1, 0), 1, moved), new(B, new(1, 1, 0), 1)], rng), turns),
            [Unit(A, pa), Unit(B, 5)]);
    }

    private static AttackUnitState Unit(BattleUnitId id, int pa) => new(id, 50, 50, 0, 0, pa,
        0, 0, 0, 0, 70, 0, BattleFacing.South, AttackUnitStatus.None, 0, new("sample:item/sword"));
}
