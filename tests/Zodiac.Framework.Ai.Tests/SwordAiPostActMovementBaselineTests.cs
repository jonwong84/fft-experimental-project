using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-176/180.
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPostActMovementBaselineTests
{
    private static readonly SwordAiMovementBaselineProfile Profile = new(true, true, true, true);

    [Fact]
    public void Actual_hit_and_miss_feed_literal_stay_move_CT_scores_and_one_upkeep_draw()
    {
        foreach (var (evade, hp, score, word, count, raw) in new[] {
            (0, 25, 64, 3535774167u, 25ul, 21183u), (89, 50, 0, 1343201072u, 30ul, 20495u) })
        foreach (bool move in new[] { false, true })
        {
            var state = AfterAct(evade: evade).Battle; var before = Snapshot(state);
            var destination = new TilePosition(2, move ? 2 : 1, 0);
            var result = Evaluate(state, destination); var o = Success(result);
            Assert.Equal(move ? 15 : 35, o.TurnEndCt);
            Assert.Equal(new FftRandomState(word, count), result.RngState);
            Assert.Equal(raw, Assert.Single(o.UpkeepPicks).RawRoll);
            Assert.Equal((B, 1ul, state.TurnBattle.Turns.Ticks + 1), (o.MoveOpportunityUnitId, o.ElapsedTicks, o.Ticks));
            var actor = o.Units.Single(u => u.UnitId == A); var target = o.Units.Single(u => u.UnitId == B);
            Assert.Equal(move ? 20 : 40, actor.Ct); Assert.Equal(0, target.Ct);
            Assert.Equal(destination, actor.Position); Assert.Equal(new TilePosition(1, 1, 0), target.Position);
            Assert.True(actor.ActionTaken); Assert.Equal(move, actor.MovementTaken);
            Assert.Equal(hp, target.Hp); Assert.Equal(score, o.Score.Priority); Assert.Equal(100, o.Score.HitPercent);
            Assert.Equal(128, o.Score.Contributions.Single(c => c.UnitId == A).HpTerm);
            Assert.Equal(evade == 0 ? 64 : 128, o.Score.Contributions.Single(c => c.UnitId == B).HpTerm);
            foreach (var combat in state.Combatants)
            {
                var unit = o.Units.Single(u => u.UnitId == combat.UnitId);
                Assert.Equal((combat.Hp, combat.Status, combat.Facing), (unit.Hp, unit.Status, unit.Facing));
            }
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Only_unused_Move_is_refunded_and_CT_is_capped_before_storing()
    {
        foreach (var (ct, stay, move) in new[] { (10, 30, 10), (50, 60, 50), (99, 60, 60) })
        {
            var state = State(moved: false, acted: true, ct: ct, targetCt: 100);
            var staying = Success(Evaluate(state)); var moving = Success(Evaluate(state, new(1, 2, 0)));
            Assert.Equal(stay, staying.TurnEndCt); Assert.Equal(move, moving.TurnEndCt);
            Assert.Equal(stay, staying.Units.Single(u => u.UnitId == A).Ct);
            Assert.Equal(move, moving.Units.Single(u => u.UnitId == A).Ct);
            Assert.Equal(0ul, staying.ElapsedTicks); Assert.Equal(0ul, moving.ElapsedTicks);
        }
        var zero = State(moved: false, acted: true, move: 0, targetCt: 100);
        Assert.Equal(35, Success(Evaluate(zero)).TurnEndCt);
        Atomic(Evaluate(zero, new(1, 2, 0)), SwordAiMovementBaselineRejection.InvalidDestination, LiveRng);
    }

    [Fact]
    public void Actual_Critical_and_knockback_feed_scores_and_restored_geometry()
    {
        var critical = AfterAct(power: 8).Battle; var before = Snapshot(critical); var scored = Success(Evaluate(critical));
        Assert.Equal(135, scored.Score.Priority);
        var enemy = scored.Score.Contributions.Single(c => c.UnitId == B);
        Assert.Equal((25, -32), (enemy.HpTerm, enemy.StatusTerm));
        Assert.Equal((10, AttackUnitStatus.Critical),
            (scored.Units.Single(u => u.UnitId == B).Hp, scored.Units.Single(u => u.UnitId == B).Status));
        Assert.Equal(before, Snapshot(critical));
        var pushed = AfterAct(power: 1, entryWord: 3).Battle; before = Snapshot(pushed);
        var vacated = Evaluate(pushed, new(1, 1, 0)); var o = Success(vacated);
        Assert.Equal(new TilePosition(0, 1, 0), o.Units.Single(u => u.UnitId == B).Position);
        Assert.Equal(new TilePosition(1, 1, 0), o.Units.Single(u => u.UnitId == A).Position);
        Assert.Equal(new FftBiosRandom().Next(pushed.TurnBattle.Turns.RngState).State, vacated.RngState);
        Atomic(Evaluate(pushed, new(0, 1, 0)), SwordAiMovementBaselineRejection.InvalidDestination, pushed.TurnBattle.Turns.RngState);
        Assert.Equal(before, Snapshot(pushed));
    }

    [Fact]
    public void Exact_tick_draw_limits_and_late_failure_restore_incoming_hypothetical_RNG()
    {
        var state = State(moved: false, acted: true, ticks: ulong.MaxValue - 1);
        var last = new FftRandomState(1, ulong.MaxValue - 1); var fits = Evaluate(state, rng: last); var o = Success(fits);
        Assert.Equal(ulong.MaxValue, o.Ticks); Assert.Equal(1ul, o.ElapsedTicks);
        Assert.Equal(new FftRandomState(1103527590, ulong.MaxValue), fits.RngState);
        var exhausted = last with { DrawCount = ulong.MaxValue }; var before = Snapshot(state);
        Atomic(Evaluate(state, rng: exhausted), SwordAiMovementBaselineRejection.RandomDrawCountExhausted, exhausted);
        Assert.Equal(before, Snapshot(state));
        Atomic(Evaluate(State(moved: false, acted: true, ticks: ulong.MaxValue), rng: last),
            SwordAiMovementBaselineRejection.TickCountExhausted, last);
        var ready = Success(Evaluate(State(moved: false, acted: true, ticks: ulong.MaxValue, targetCt: 254), rng: last));
        Assert.Equal(ulong.MaxValue, ready.Ticks); Assert.Equal(0ul, ready.ElapsedTicks);
        Assert.Equal((254, 54), (ready.UpkeepPicks[0].CtBefore, ready.UpkeepPicks[0].CtAfter));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Explicit_post_ACT_admission_retains_legacy_choice_guards_and_source_certificates()
    {
        var actual = AfterAct(); var state = actual.Battle; var rng = state.TurnBattle.Turns.RngState;
        Atomic(SwordAiMovementBaseline.Evaluate(state, new(A, B, new(2, 1, 0)), Candidate, Score, Profile, rng),
            SwordAiMovementBaselineRejection.UnavailableAction, rng);
        Atomic(Evaluate(State(moved: false)), SwordAiMovementBaselineRejection.UnavailableAction, LiveRng);
        var completed = FacingTurnBattle.Move(actual, new(A, new(2, 2, 0))); Assert.True(completed.Accepted);
        Atomic(Evaluate(completed.State.Battle), SwordAiMovementBaselineRejection.UnavailableAction,
            completed.State.Battle.TurnBattle.Turns.RngState);
        Atomic(Evaluate(state, second: true), SwordAiMovementBaselineRejection.UnavailableAction, rng);
        Atomic(Evaluate(state, profile: Profile with { FreshSourceScoreCell = false }),
            SwordAiMovementBaselineRejection.UnsupportedBaselineProfile, rng);
        Atomic(Evaluate(state, new(1, 1, 0)), SwordAiMovementBaselineRejection.InvalidDestination, rng);
        Atomic(Evaluate(state, new(2, 1, 1)), SwordAiMovementBaselineRejection.InvalidDestination, rng);
        Atomic(Evaluate(state, new(2, 3, 0)), SwordAiMovementBaselineRejection.InvalidDestination, rng);
        var ko = AfterAct(power: 10).Battle;
        Atomic(Evaluate(ko), SwordAiMovementBaselineRejection.TerminalInput, ko.TurnBattle.Turns.RngState);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actors_orders_and_carried_preparation_RNG_replay_without_mutating_live_state()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = AfterAct(second: second).Battle; var before = Snapshot(state);
            var result = Evaluate(state, second: second); Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, second: second)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(AfterAct(second: second, reverse: true).Battle, second: second)));
            var prepared = SwordAiPreparation.EvaluateAfterAct(state, Request(second), Candidate, Score, Refresh, Preparation,
                SwordAiScenarioTeam.NonBlue, state.TurnBattle.Turns.RngState);
            Assert.True(prepared.Accepted);
            var carried = Evaluate(state, second: second, rng: prepared.RngState); Success(carried);
            Assert.Equal(new FftBiosRandom().Next(prepared.RngState).State, carried.RngState);
            Assert.Equal(state.TurnBattle.Turns.RngState.DrawCount + 2, carried.RngState.DrawCount);
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static SwordAiMovementBaselineResult Evaluate(AttackBattleSnapshot state, TilePosition? destination = null,
        bool second = false, SwordAiMovementBaselineProfile? profile = null, FftRandomState? rng = null)
    {
        var actor = second ? B : A; state.TurnBattle.Battle.TryGetUnit(actor, out var body);
        return SwordAiMovementBaseline.EvaluateAfterAct(state, new(actor, second ? A : B, destination ?? body!.Position),
            Candidate, Score, profile ?? Profile, rng ?? state.TurnBattle.Turns.RngState);
    }
    private static SwordAiMovementBaselineOutcome Success(SwordAiMovementBaselineResult result)
    { Assert.True(result.Accepted, Serialize(result)); Assert.Equal(SwordAiMovementBaselineRejection.None, result.Rejection);
        return Assert.IsType<SwordAiMovementBaselineOutcome>(result.Outcome); }
    private static void Atomic(SwordAiMovementBaselineResult result, SwordAiMovementBaselineRejection rejection, FftRandomState rng)
    { Assert.False(result.Accepted); Assert.Equal(rejection, result.Rejection); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState); }
}
