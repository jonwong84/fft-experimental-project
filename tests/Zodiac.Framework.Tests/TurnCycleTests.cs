using Zodiac.Framework;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class TurnCycleTests
{
    private static readonly BattleUnitId A = new("sample:unit/a");
    private static readonly BattleUnitId B = new("sample:unit/b");

    [Theory]
    [InlineData(89, 10, 2, 109, 9)]
    [InlineData(90, 10, 1, 100, 0)]
    [InlineData(91, 10, 1, 101, 1)]
    [InlineData(249, 5, 1, 254, 54)]
    [InlineData(250, 5, 1, 254, 54)]
    [InlineData(254, 255, 1, 254, 54)]
    public void Source_tick_threshold_and_cap_vectors(int ct, int speed, ulong ticks, int readyCt, int remainder)
    {
        var input = new TurnCycleState([new(A, speed, ct, 0)], FftBiosRandom.Seed(1));
        TurnCycleResult result = TurnCycle.Advance(input);
        Assert.True(result.Accepted);
        Assert.Equal(ticks, result.State.Ticks);
        var ready = Assert.IsType<TurnReadyEvent>(result.Events[^1]);
        Assert.Equal(readyCt, ready.CtBeforeSelection);
        Assert.Equal(remainder, ready.CtAfterSelection);
        Assert.Equal(remainder, result.State.Units[0].Ct);
        Assert.Equal((int)ticks, result.Events.OfType<TurnClockAdvancedEvent>().Count());
        Assert.Equal(new FftRandomState(0x41c67ea6, 1), result.State.RngState);
        Assert.Equal(16838u, ready.UpkeepRoll);
        Assert.Equal(ct, input.Units[0].Ct);
        Assert.Equal(0UL, input.Ticks);
        Assert.Equal(FftBiosRandom.Seed(1), input.RngState);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(199, 99)]
    [InlineData(200, 0)]
    [InlineData(254, 54)]
    public void Source_ready_modulo_vectors_select_without_an_extra_tick(int ct, int remainder)
    {
        var input = new TurnCycleState([new(A, 10, ct, 0, movementTaken: true, actionTaken: true)],
            FftBiosRandom.Seed(1), ticks: 123, phase: TurnCyclePhase.PickUnit);
        var result = TurnCycle.Advance(input);
        Assert.Equal(123UL, result.State.Ticks);
        Assert.IsType<TurnReadyEvent>(Assert.Single(result.Events));
        TurnUnitState selected = Assert.Single(result.State.Units);
        Assert.Equal(remainder, selected.Ct);
        Assert.True(selected.HasTurn);
        Assert.False(selected.MovementTaken);
        Assert.False(selected.ActionTaken);
        Assert.Equal(TurnCyclePhase.UnitTurn, result.State.Phase);
    }

    [Fact]
    public void Greatest_CT_precedes_tie_key_and_equal_CT_uses_explicit_key_not_ID_or_input_order()
    {
        var later = new TurnUnitState(A, 10, 100, 9);
        var earlier = new TurnUnitState(B, 10, 100, 1);
        Assert.Equal(B, TurnCycle.Advance(Pick([later, earlier])).State.ActiveUnitId);
        Assert.Equal(B, TurnCycle.Advance(Pick([earlier, later])).State.ActiveUnitId);
        Assert.Equal(A, TurnCycle.Advance(Pick([new(A, 10, 101, 9), earlier])).State.ActiveUnitId);
    }

    [Fact]
    public void Initial_AddCt_and_peer_recheck_PickUnit_are_distinct_phases()
    {
        var unit = new TurnUnitState(A, 10, 100, 0);
        var firstTick = TurnCycle.Advance(new([unit], FftBiosRandom.Seed(1)));
        var peerPick = TurnCycle.Advance(Pick([unit]));
        Assert.Equal(1UL, firstTick.State.Ticks);
        Assert.Equal(10, firstTick.State.Units[0].Ct);
        Assert.Equal(0UL, peerPick.State.Ticks);
        Assert.Equal(0, peerPick.State.Units[0].Ct);
    }

    [Theory]
    [InlineData(0, false, false, 40)]
    [InlineData(0, true, false, 20)]
    [InlineData(0, false, true, 20)]
    [InlineData(0, true, true, 0)]
    [InlineData(19, false, false, 59)]
    [InlineData(20, false, false, 60)]
    [InlineData(21, false, false, 60)]
    [InlineData(39, true, false, 59)]
    [InlineData(40, true, false, 60)]
    [InlineData(41, true, false, 60)]
    [InlineData(60, true, true, 60)]
    [InlineData(61, true, true, 60)]
    [InlineData(99, true, true, 60)]
    [InlineData(54, true, true, 54)]
    [InlineData(54, true, false, 60)]
    [InlineData(54, false, false, 60)]
    public void Source_completion_refunds_and_cap_vectors(int ct, bool moved, bool acted, int expected)
    {
        var unit = new TurnUnitState(A, 10, ct, 0, hasTurn: true, movementTaken: moved,
            actionTaken: moved && acted ? false : acted);
        var input = new TurnCycleState([unit], new(0xfedcba98, 77), 123, TurnCyclePhase.UnitTurn);
        TurnCommandKind command = moved && acted ? TurnCommandKind.ConfirmAct : TurnCommandKind.Wait;
        TurnCycleResult result = TurnCycle.Apply(input, new(A, command));
        Assert.True(result.Accepted);
        Assert.Equal(expected, result.State.Units[0].Ct);
        Assert.False(result.State.Units[0].HasTurn);
        Assert.Null(result.State.ActiveUnitId);
        Assert.Equal(TurnCyclePhase.PickUnit, result.State.Phase);
        Assert.Equal(input.RngState, result.State.RngState);
        Assert.Equal(input.Ticks, result.State.Ticks);
        Assert.Equal(new TurnEndedEvent(A, ct, expected), result.Events[^1]);
        Assert.Equal(ct, input.Units[0].Ct);
        Assert.True(input.Units[0].HasTurn);
    }

    [Theory]
    [InlineData(TurnCommandKind.ConfirmMove, TurnCommandKind.ConfirmAct)]
    [InlineData(TurnCommandKind.ConfirmAct, TurnCommandKind.ConfirmMove)]
    public void Second_choice_automatically_ends_turn_and_repeated_Wait_cannot_refund_again(TurnCommandKind first, TurnCommandKind second)
    {
        TurnCycleState input = Active();
        var one = TurnCycle.Apply(input, new(A, first));
        Assert.True(one.State.Units[0].HasTurn);
        Assert.Equal(5, one.State.Units[0].Ct);
        Assert.Equal(new TurnChoiceConsumedEvent(A, first), Assert.Single(one.Events));
        var both = TurnCycle.Apply(one.State, new(A, second));
        Assert.Collection(both.Events,
            e => Assert.Equal(new TurnChoiceConsumedEvent(A, second), e),
            e => Assert.Equal(new TurnEndedEvent(A, 5, 5), e));
        Assert.True(both.State.Units[0].MovementTaken);
        Assert.True(both.State.Units[0].ActionTaken);
        AssertRejected(both.State, TurnCycle.Apply(both.State, new(A, TurnCommandKind.Wait)), TurnCycleRejection.InactiveUnit);
    }

    [Fact]
    public void Rejected_commands_preserve_input_instance_events_RNG_ticks_and_flags()
    {
        TurnCycleState input = Active();
        AssertRejected(input, TurnCycle.Apply(input, new(B, TurnCommandKind.Wait)), TurnCycleRejection.InactiveUnit);
        AssertRejected(input, TurnCycle.Apply(input, new(new("sample:unit/missing"), TurnCommandKind.ConfirmMove)), TurnCycleRejection.UnknownUnit);
        AssertRejected(input, TurnCycle.Apply(input, new(A, (TurnCommandKind)999)), TurnCycleRejection.UnsupportedCommand);
        AssertRejected(input, TurnCycle.Advance(input), TurnCycleRejection.TurnAlreadyActive);
        var moved = TurnCycle.Apply(input, new(A, TurnCommandKind.ConfirmMove)).State;
        AssertRejected(moved, TurnCycle.Apply(moved, new(A, TurnCommandKind.ConfirmMove)), TurnCycleRejection.MovementAlreadyTaken);
        var acted = TurnCycle.Apply(input, new(A, TurnCommandKind.ConfirmAct)).State;
        AssertRejected(acted, TurnCycle.Apply(acted, new(A, TurnCommandKind.ConfirmAct)), TurnCycleRejection.ActionAlreadyTaken);
        AssertRejected(Pick([new(A, 10, 0, 0)]), TurnCycleRejection.InactiveUnit);
    }

    [Fact]
    public void Counter_exhaustion_is_atomic_but_ready_peer_does_not_require_another_tick()
    {
        var tickExhausted = new TurnCycleState([new(A, 1, 0, 0)], FftBiosRandom.Seed(1), ulong.MaxValue, TurnCyclePhase.PickUnit);
        AssertRejected(tickExhausted, TurnCycle.Advance(tickExhausted), TurnCycleRejection.TickCountExhausted);
        var nearExhausted = new TurnCycleState([new(A, 1, 0, 0)], FftBiosRandom.Seed(1), ulong.MaxValue - 1);
        AssertRejected(nearExhausted, TurnCycle.Advance(nearExhausted), TurnCycleRejection.TickCountExhausted);
        var rngExhausted = new TurnCycleState([new(A, 10, 100, 0)], new(1, ulong.MaxValue), phase: TurnCyclePhase.PickUnit);
        AssertRejected(rngExhausted, TurnCycle.Advance(rngExhausted), TurnCycleRejection.RandomDrawCountExhausted);
        var ready = new TurnCycleState([new(A, 10, 100, 0)], FftBiosRandom.Seed(1), ulong.MaxValue, TurnCyclePhase.PickUnit);
        var result = TurnCycle.Advance(ready);
        Assert.True(result.Accepted);
        Assert.Equal(ulong.MaxValue, result.State.Ticks);
        Assert.Equal(1UL, result.State.RngState.DrawCount);
    }

    [Fact]
    public void Two_unit_source_sequence_replays_explicit_state_events_and_five_upkeep_draws()
    {
        var initial = new TurnCycleState([new(A, 10, 0, 0), new(B, 10, 0, 1)], FftBiosRandom.Seed(1));
        TurnCycleResult[] first = Replay(initial);
        TurnCycleResult[] second = Replay(initial);
        for (int i = 0; i < first.Length; i++)
        {
            Assert.Equal(first[i].State.Units.ToArray(), second[i].State.Units.ToArray());
            Assert.Equal(first[i].State.Ticks, second[i].State.Ticks);
            Assert.Equal(first[i].State.Phase, second[i].State.Phase);
            Assert.Equal(first[i].State.ActiveUnitId, second[i].State.ActiveUnitId);
            Assert.Equal(first[i].State.RngState, second[i].State.RngState);
            Assert.Equal(first[i].Events.ToArray(), second[i].Events.ToArray());
        }
        TurnReadyEvent[] ready = first.SelectMany(step => step.Events).OfType<TurnReadyEvent>().ToArray();
        Assert.Equal(new[] { A, B, A, B, A }, ready.Select(item => item.UnitId));
        // First five seed-1 values/states are committed executable-reference BIOS vectors, not a second RNG transcription.
        Assert.Equal(new uint[] { 16838, 5758, 10113, 17515, 31051 }, ready.Select(item => item.UpkeepRoll));
        Assert.Equal(new ulong[] { 10, 10, 16, 18, 26 }, first.Where(step => step.Events.Any(e => e is TurnReadyEvent)).Select(step => step.State.Ticks));
        Assert.Equal(26, first.SelectMany(step => step.Events).OfType<TurnClockAdvancedEvent>().Count());
        int[][] expectedCt = [[0, 100], [40, 100], [40, 0], [40, 0], [40, 20], [0, 80],
            [0, 80], [0, 80], [20, 0], [20, 0], [20, 20], [0, 100]];
        for (int i = 0; i < expectedCt.Length; i++)
            Assert.Equal(expectedCt[i], first[i].State.Units.Select(unit => unit.Ct));
        Assert.Equal(new[] { 0, 100 }, first[^1].State.Units.Select(unit => unit.Ct));
        Assert.Equal(new FftRandomState(4182499122, 5), first[^1].State.RngState);
        Assert.Equal(0UL, initial.Ticks);
        Assert.Equal(FftBiosRandom.Seed(1), initial.RngState);
        Assert.All(initial.Units, unit => { Assert.Equal(0, unit.Ct); Assert.False(unit.HasTurn); });
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(256, 0)]
    [InlineData(10, -1)]
    [InlineData(10, 255)]
    public void Unsupported_speed_and_CT_are_rejected(int speed, int ct) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TurnUnitState(A, speed, ct, 0));

    [Fact]
    public void Invalid_domain_and_phase_snapshots_are_rejected_and_input_collections_are_copied()
    {
        Assert.Throws<ArgumentException>(() => new TurnUnitState(default, 10, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TurnUnitState(A, 10, 0, -1));
        Assert.Throws<ArgumentException>(() => new TurnUnitState(A, 10, 100, 0, hasTurn: true));
        Assert.Throws<ArgumentException>(() => new TurnUnitState(A, 10, 0, 0, true, true, true));
        Assert.Throws<ArgumentException>(() => Pick([]));
        Assert.Throws<ArgumentNullException>(() => Pick(null!));
        Assert.Throws<ArgumentNullException>(() => Pick([null!]));
        Assert.Throws<ArgumentException>(() => Pick([new(A, 10, 0, 0), new(A, 10, 0, 1)]));
        Assert.Throws<ArgumentException>(() => Pick([new(A, 10, 0, 0), new(B, 10, 0, 0)]));
        Assert.Throws<ArgumentException>(() => Pick([new(A, 10, 0, 0, true)]));
        Assert.Throws<ArgumentException>(() => new TurnCycleState([new(A, 10, 0, 0)], FftBiosRandom.Seed(1), phase: TurnCyclePhase.UnitTurn));
        Assert.Throws<ArgumentException>(() => new TurnCycleState([new(A, 10, 0, 0, true), new(B, 10, 0, 1, true)], FftBiosRandom.Seed(1), phase: TurnCyclePhase.UnitTurn));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TurnCycleState([new(A, 10, 0, 0)], FftBiosRandom.Seed(1), phase: (TurnCyclePhase)999));
        var list = new List<TurnUnitState> { new(A, 10, 0, 0) };
        var state = Pick(list);
        list.Clear();
        Assert.Single(state.Units);
    }

    private static TurnCycleResult[] Replay(TurnCycleState initial)
    {
        var results = new List<TurnCycleResult>();
        TurnCycleState state = initial;
        Step();                            // A ready at tick 10
        Step(A, TurnCommandKind.Wait);      // A CT40
        Step();                            // B ready, no extra tick
        Step(B, TurnCommandKind.ConfirmMove);
        Step(B, TurnCommandKind.Wait);      // B CT20
        Step();                            // A ready at tick16, B80
        Step(A, TurnCommandKind.ConfirmMove);
        Step(A, TurnCommandKind.ConfirmAct);// A autoend CT0
        Step();                            // B ready at tick18, A20
        Step(B, TurnCommandKind.ConfirmAct);
        Step(B, TurnCommandKind.Wait);      // B20
        Step();                            // A wins equal CT at tick26
        return results.ToArray();

        void Step(BattleUnitId? id = null, TurnCommandKind kind = default)
        {
            TurnUnitState[] before = state.Units.ToArray();
            FftRandomState rngBefore = state.RngState;
            TurnCycleResult result = id is { } unit ? TurnCycle.Apply(state, new(unit, kind)) : TurnCycle.Advance(state);
            Assert.True(result.Accepted);
            Assert.Equal(before, state.Units.ToArray());
            Assert.Equal(rngBefore, state.RngState);
            if (id is not null) Assert.Equal(rngBefore, result.State.RngState);
            results.Add(result);
            state = result.State;
        }
    }

    private static TurnCycleState Active() => new([new(A, 10, 5, 0, true), new(B, 10, 10, 1)],
        new FftRandomState(0xfedcba98, 123), 456, TurnCyclePhase.UnitTurn);
    private static TurnCycleState Pick(IEnumerable<TurnUnitState> units) => new(units, FftBiosRandom.Seed(1), phase: TurnCyclePhase.PickUnit);
    private static void AssertRejected(TurnCycleState input, TurnCycleRejection rejection) =>
        AssertRejected(input, TurnCycle.Apply(input, new(A, TurnCommandKind.Wait)), rejection);
    private static void AssertRejected(TurnCycleState input, TurnCycleResult result, TurnCycleRejection rejection)
    {
        Assert.False(result.Accepted);
        Assert.Equal(rejection, result.Rejection);
        Assert.Same(input, result.State);
        Assert.Empty(result.Events);
    }
}
