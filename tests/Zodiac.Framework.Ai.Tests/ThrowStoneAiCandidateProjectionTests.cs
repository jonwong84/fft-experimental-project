using Xunit;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ThrowStoneAiCandidateProjectionTests
{
    [Fact]
    public void Fresh_formula_and_clock_preserve_literal_stay_move_CT_HP_and_retained_draws()
    {
        foreach (bool move in new[] { false, true })
        {
            var state = State(); var before = Snapshot(state); var destination = move ? new TilePosition(2, 1, 0) : new(1, 1, 0);
            var result = Evaluate(state, Candidate(destination)); var o = Success(result);
            Assert.Equal((100, 10, move ? 15 : 35), (o.HitPercent, o.NormalDamage, o.TurnEndCt));
            Assert.Equal(new FftRandomState(4182499122, 12), result.RngState);
            Assert.Equal((B, 1ul, 21ul), (o.MoveOpportunityUnitId, o.ElapsedTicks, o.Ticks));
            Assert.Equal(31051u, Assert.Single(o.UpkeepPicks).RawRoll);
            var actor = o.Units.Single(u => u.UnitId == A); var target = o.Units.Single(u => u.UnitId == B);
            Assert.Equal(move ? 20 : 40, actor.Ct); Assert.Equal(0, target.Ct);
            Assert.True(actor.ActionTaken); Assert.Equal(move, actor.MovementTaken); Assert.Equal(destination, actor.Position);
            Assert.Equal(40, target.Hp); Assert.Equal(new TilePosition(4, 1, 0), target.Position);
            Assert.Equal(BattleFacing.West, target.Facing); Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Candidate_order_selects_the_correct_formula_origin_without_publishing_a_Move()
    {
        var state = State(target: new(6, 1, 0));
        Atomic(Evaluate(state, Candidate(new(2, 1, 0), actBeforeMove: true)), ThrowStoneAiCandidateRejection.OutOfRange);
        Success(Evaluate(state, Candidate(new(2, 1, 0), actBeforeMove: false)));
        state = State(move: 4, evade: 75);
        var before = Snapshot(state);
        Assert.Equal(25, Success(Evaluate(state, Candidate(new(4, 2, 0), actBeforeMove: true))).HitPercent);
        Assert.Equal(100, Success(Evaluate(state, Candidate(new(4, 2, 0), actBeforeMove: false))).HitPercent);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Zero_accuracy_stops_before_the_clock_and_projected_KO_gets_corpse_upkeep_without_knockback()
    {
        var zero = Evaluate(State(evade: 100, ticks: ulong.MaxValue));
        Assert.Equal(ThrowStoneAiCandidateDisposition.NotValuable, zero.Disposition); Assert.Null(zero.Outcome);
        Assert.Equal(new FftRandomState(3295386429, 11), zero.RngState);
        var state = State(hp: 10); var before = Snapshot(state); var result = Evaluate(state); var o = Success(result);
        Assert.Equal(new FftRandomState(2516284547, 13), result.RngState);
        Assert.Equal(new[] { B, A }, o.UpkeepPicks.Select(p => p.UnitId)); Assert.Equal(new[] { true, false }, o.UpkeepPicks.Select(p => p.Dead));
        Assert.Equal(13ul, o.ElapsedTicks); Assert.Equal(33ul, o.Ticks);
        var target = o.Units.Single(u => u.UnitId == B);
        Assert.Equal((0, AttackUnitStatus.Dead, (byte)3, 60), (target.Hp, target.Status, target.DeathCounter, target.Ct));
        Assert.Equal(new TilePosition(4, 1, 0), target.Position); Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Exact_budgets_and_late_clock_failure_return_no_partial_candidate_or_RNG()
    {
        var state = State(ticks: ulong.MaxValue - 1); var last = new FftRandomState(1, ulong.MaxValue - 5);
        var fits = Evaluate(state, rng: last); Assert.Equal(ulong.MaxValue, Success(fits).Ticks); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var shortBudget = last with { DrawCount = ulong.MaxValue - 4 }; var before = Snapshot(state);
        Atomic(Evaluate(state, rng: shortBudget), ThrowStoneAiCandidateRejection.RandomDrawCountExhausted, shortBudget);
        Atomic(Evaluate(State(ticks: ulong.MaxValue)), ThrowStoneAiCandidateRejection.TickCountExhausted);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Baseline_admission_grants_and_both_actor_order_replay_preserve_live_state()
    {
        Atomic(Evaluate(State(hp: 10), Candidate(new(4, 1, 0))), ThrowStoneAiCandidateRejection.InvalidCandidate);
        Atomic(Evaluate(State(moved: true), Candidate(new(2, 1, 0))), ThrowStoneAiCandidateRejection.InvalidCandidate);
        Atomic(Evaluate(State(acted: true)), ThrowStoneAiCandidateRejection.UnavailableAction);
        Atomic(Evaluate(State(hp: 0)), ThrowStoneAiCandidateRejection.TerminalInput);
        var grant = Evaluate(State(), profile: CandidateProfile(false)); Atomic(grant, ThrowStoneAiCandidateRejection.FormulaRejected);
        Assert.Equal(AttackCommandRejection.AbilityNotGranted, grant.FormulaRejection);
        foreach (bool second in new[] { false, true })
        {
            var state = State(second: second); var before = Snapshot(state); var candidate = Candidate(second: second);
            var result = Evaluate(state, candidate); Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, candidate)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(second: second, reverse: true), candidate)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    internal static ThrowStoneAiCandidateResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiCandidate? candidate = null,
        ThrowStoneAiCandidateProfile? profile = null, FftRandomState? rng = null) =>
        ThrowStoneAiCandidateProjection.Evaluate(state, candidate ?? Candidate(), profile ?? CandidateProfile(), rng ?? Decision);
    private static ThrowStoneAiCandidateOutcome Success(ThrowStoneAiCandidateResult result)
    { Assert.Equal(ThrowStoneAiCandidateDisposition.Projected, result.Disposition); Assert.Equal(ThrowStoneAiCandidateRejection.None, result.Rejection);
        return Assert.IsType<ThrowStoneAiCandidateOutcome>(result.Outcome); }
    private static void Atomic(ThrowStoneAiCandidateResult result, ThrowStoneAiCandidateRejection reason, FftRandomState? rng = null)
    { Assert.Equal(ThrowStoneAiCandidateDisposition.Rejected, result.Disposition); Assert.Equal(reason, result.Rejection);
        Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState); }
}
