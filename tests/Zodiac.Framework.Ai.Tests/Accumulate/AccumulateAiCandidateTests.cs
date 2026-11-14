using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AccumulateAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AccumulateAiCandidateTests
{
    [Theory]
    [InlineData(1, 2)] [InlineData(98, 99)] [InlineData(99, 99)]
    public void Source_PA_boundaries_keep_hit100_and_stat_credit_even_without_a_change(int pa, int after)
    {
        var state = Battle(pa: pa); var before = Snapshot(state);
        var result = Score(state); var projection = Success(result);
        Assert.Equal((pa, after, 35), (projection.PhysicalAttackBefore, projection.PhysicalAttackAfter, projection.TurnEndCt));
        Assert.Equal((3, 100), ((int)result.Score!.Priority, (int)result.Score.HitPercent));
        Assert.Equal(new[] { 488, 3 }, result.Score.Contributions.Select(c => (int)c.RunningPriority));
        Assert.All(projection.Units, u => Assert.Equal(50, u.Hp));
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Both_orders_share_self_effect_and_literal_clock_without_a_formula_or_usefulness_draw()
    {
        var state = Battle();
        foreach (bool actFirst in new[] { true, false })
        {
            var result = Score(state, SelfCandidate(move: true, actFirst: actFirst)); var p = Success(result);
            Assert.Equal((5, 6, 15, 1ul, 21ul, B),
                (p.PhysicalAttackBefore, p.PhysicalAttackAfter, p.TurnEndCt, p.ElapsedTicks, p.Ticks, p.MoveOpportunityUnitId));
            Assert.Equal(new RandomState(1103527590, 8), result.RngState);
            Assert.Equal(16838u, Assert.Single(p.UpkeepPicks).RawRoll);
            var actor = p.Units.Single(u => u.UnitId == A);
            Assert.Equal((Destination, 20, true, true), (actor.Position, actor.Ct, actor.MovementTaken, actor.ActionTaken));
            Assert.Equal(0, p.Units.Single(u => u.UnitId == B).Ct);
        }
        Assert.Equal(5, state.Combatants.Single(u => u.UnitId == A).PhysicalAttack);
    }

    [Theory]
    [InlineData(10, 50, -132)] [InlineData(50, 10, 138)]
    public void Absolute_score_uses_unchanged_HP_and_status_without_a_direct_PA_term(int actorHp, int enemyHp, int expected)
    {
        var result = Score(Battle(actorHp: actorHp, enemyHp: enemyHp)); Success(result);
        Assert.Equal(expected, result.Score!.Priority);
        Assert.Equal(-32, result.Score.Contributions.Single(c => c.StatusTerm != 0).StatusTerm);
    }

    [Fact]
    public void Exact_clock_draw_tick_budgets_and_ready_unit_at_tick_limit()
    {
        var last = new RandomState(1, ulong.MaxValue - 1);
        var fits = Score(Battle(ticks: ulong.MaxValue - 1), rng: last); Success(fits);
        Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount); Assert.Equal(ulong.MaxValue, fits.Projection!.Outcome!.Ticks);
        var exhausted = last with { DrawCount = ulong.MaxValue };
        Atomic(Score(Battle(), rng: exhausted), AccumulateAiCandidateRejection.RandomDrawCountExhausted, exhausted);
        Atomic(Score(Battle(ticks: ulong.MaxValue)), AccumulateAiCandidateRejection.TickCountExhausted);
        var ready = Score(Battle(ticks: ulong.MaxValue, targetCt: 100));
        Assert.Equal(0ul, Success(ready).ElapsedTicks);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Grants_effect_domain_active_self_and_geometry_are_required_before_projection()
    {
        var state = Battle();
        Atomic(Score(state, SelfCandidate() with { ActorId = new("missing") }), AccumulateAiCandidateRejection.UnknownActor);
        Atomic(Score(state, SelfCandidate() with { OpponentId = A }), AccumulateAiCandidateRejection.InvalidCandidate);
        Atomic(Score(state, SelfCandidate() with { SelfActiveAndUseful = false }), AccumulateAiCandidateRejection.SelfNotUseful);
        Atomic(Score(state, SelfCandidate() with { AbilityId = new("sample:ability/missing") }), AccumulateAiCandidateRejection.UnknownAbility);
        Atomic(Score(state, profile: Profile(false)), AccumulateAiCandidateRejection.AbilityNotGranted);
        Atomic(Score(Battle(pa: 100)), AccumulateAiCandidateRejection.UnsupportedProfile);
        Atomic(Score(Battle(enemyHp: 0)), AccumulateAiCandidateRejection.TerminalInput);
        Atomic(Score(Battle(acted: true)), AccumulateAiCandidateRejection.UnavailableAction);
        Atomic(Score(Battle(moved: true), SelfCandidate(move: true)), AccumulateAiCandidateRejection.InvalidCandidate);
        Atomic(Score(state, SelfCandidate() with { Destination = new(7, 7, 0) }), AccumulateAiCandidateRejection.InvalidCandidate);
        var unsupportedScore = new AiScoreProfile([new(A, AiScoreFacts.None), new(B, AiScoreFacts.ClosedProfile)]);
        var rejected = AccumulateAiCandidateScoring.Evaluate(state, SelfCandidate(), Profile(), unsupportedScore, Decision);
        Assert.Equal(AiScoringRejection.UnsupportedScoreProfile, rejected.Rejection); Assert.Null(rejected.Projection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_replays_and_collection_order_does_not_change_PA_clock_or_scores()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second); var before = Snapshot(state); var candidate = SelfCandidate(second: second);
            var first = Score(state, candidate); Success(first);
            Assert.Equal(Serialize(first), Serialize(Score(state, candidate)));
            Assert.Equal(Serialize(first), Serialize(Score(Battle(second: second, reverse: true), candidate)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    internal static AccumulateAiScoringResult Score(AttackBattleSnapshot state, AccumulateAiCandidate? candidate = null,
        AccumulateAiCandidateProfile? profile = null, RandomState? rng = null) =>
        AccumulateAiCandidateScoring.Evaluate(state, candidate ?? SelfCandidate(), profile ?? Profile(), Scores(), rng ?? Decision);
    private static AccumulateAiCandidateOutcome Success(AccumulateAiScoringResult result)
    {
        Assert.Equal(AiScoringDisposition.Scored, result.Disposition);
        Assert.True(result.Projection!.Accepted); return Assert.IsType<AccumulateAiCandidateOutcome>(result.Projection.Outcome);
    }
    private static void Atomic(AccumulateAiScoringResult result, AccumulateAiCandidateRejection reason, RandomState? rng = null)
    {
        Assert.Equal(AiScoringDisposition.Rejected, result.Disposition); Assert.Null(result.Score);
        Assert.Equal(reason, result.Projection!.Rejection); Assert.Null(result.Projection.Outcome);
        Assert.Equal(rng ?? Decision, result.RngState);
    }
}
