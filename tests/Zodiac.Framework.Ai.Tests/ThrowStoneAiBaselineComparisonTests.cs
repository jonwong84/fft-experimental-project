using Xunit;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ThrowStoneAiBaselineComparisonTests
{
    private static readonly AiScoreProfile Score = new([new(A, AiScoreFacts.ClosedProfile), new(B, AiScoreFacts.ClosedProfile)]);
    private static readonly ThrowStoneAiBaselineProfile Profile = new(true, true, true, true);
    private static readonly TilePosition Origin = new(1, 1, 0), Moved = new(2, 1, 0);

    [Fact]
    public void Fresh_baseline_precedes_candidate_with_literal_absolute_ranks_and_separate_accuracy()
    {
        foreach (var (hp, maxHp, pa, evade, baselinePriority, priority, hit, rank) in new[] {
            (50, 50, 5, 0, 0, 26, 100, AiRankOrder.Above),
            (50, 50, 5, 75, 0, 26, 25, AiRankOrder.Above),
            (50000, 65535, 1, 0, 31, 31, 100, AiRankOrder.Equal),
            (50000, 65535, 1, 75, 31, 31, 25, AiRankOrder.Below) })
        {
            var state = State(hp: hp, maxHp: maxHp, pa: pa, evade: evade);
            var result = Evaluate(state); var outcome = Success(result);
            Assert.Equal(baselinePriority, outcome.Baseline.Score.Priority); Assert.Equal(100, outcome.Baseline.Score.HitPercent);
            Assert.Equal(priority, outcome.Candidate.Score!.Priority); Assert.Equal(hit, outcome.Candidate.Score.HitPercent);
            Assert.Equal(rank, outcome.Rank); Assert.Equal(55, outcome.Baseline.TurnEndCt);
            Assert.Equal((1ul, 21ul, B), (outcome.Baseline.ElapsedTicks, outcome.Baseline.Ticks, outcome.Baseline.MoveOpportunityUnitId));
            Assert.Equal(16838u, Assert.Single(outcome.Baseline.UpkeepPicks).RawRoll);
            Assert.Equal(5627u, Assert.Single(outcome.Candidate.Projection!.Outcome!.UpkeepPicks).RawRoll);
            Assert.Equal(new FftRandomState(2516284547, 13), result.RngState);
            // Independently fresh candidate starts after precisely the baseline's one draw, on original units.
            var candidate = ThrowStoneAiCandidateScoring.Evaluate(state, Candidate(), CandidateProfile(), Score,
                new(1103527590, 8));
            Assert.Equal(Serialize(candidate), Serialize(outcome.Candidate));
            Assert.Equal(hp, outcome.Baseline.Units.Single(u => u.UnitId == B).Hp);
            Assert.Equal(hp - 2 * pa, outcome.Candidate.Projection.Outcome.Units.Single(u => u.UnitId == B).Hp);
        }
    }

    [Fact]
    public void Move_only_refunds_and_move_first_range_use_original_state_without_sword_ownership()
    {
        foreach (var (alreadyMoved, destination, expectedCt) in new[] {
            (false, Origin, 55), (false, Moved, 35), (true, Origin, 35) })
        {
            var outcome = Success(Evaluate(State(moved: alreadyMoved), baseline: destination));
            var actor = outcome.Baseline.Units.Single(u => u.UnitId == A);
            Assert.Equal(expectedCt, outcome.Baseline.TurnEndCt); Assert.False(actor.ActionTaken);
            Assert.Equal(alreadyMoved || destination != Origin, actor.MovementTaken);
            Assert.Equal(destination, actor.Position); Assert.Equal((byte)255, actor.DeathCounter);
        }
        var state = State(target: new(6, 1, 0)); // Five tiles from original actor; four from Moved.
        var result = Evaluate(state, Candidate(Moved, actBeforeMove: false));
        Assert.Equal(AiRankOrder.Above, Success(result).Rank);
        var failure = Atomic(Evaluate(state, Candidate(Moved, actBeforeMove: true)), ThrowStoneAiBaselineComparisonStage.Candidate);
        Assert.Equal(ThrowStoneAiCandidateRejection.OutOfRange, failure.CandidateRejection);
        Assert.Equal(AttackCommandRejection.OutOfRange, failure.AbilityRejection);
    }

    [Fact]
    public void NotValuable_retains_baseline_and_four_formula_draws_but_no_candidate_clock_or_rank()
    {
        var result = Evaluate(State(evade: 100, ticks: ulong.MaxValue, targetCt: 100)); var outcome = Success(result);
        Assert.Equal(0ul, outcome.Baseline.ElapsedTicks); Assert.Equal(ulong.MaxValue, outcome.Baseline.Ticks);
        Assert.Equal(AiScoringDisposition.NotValuable, outcome.Candidate.Disposition);
        Assert.Null(outcome.Candidate.Score); Assert.Null(outcome.Candidate.Projection!.Outcome); Assert.Null(outcome.Rank);
        Assert.Equal(new FftRandomState(4182499122, 12), result.RngState);
        var shortBudget = new FftRandomState(1, ulong.MaxValue - 4);
        var failure = Atomic(Evaluate(State(evade: 100), rng: shortBudget), ThrowStoneAiBaselineComparisonStage.Candidate, shortBudget);
        Assert.Equal(ThrowStoneAiCandidateRejection.RandomDrawCountExhausted, failure.CandidateRejection);
    }

    [Fact]
    public void KO_and_exact_draw_budgets_carry_real_upkeep_and_rollback_every_partial_result_on_late_failure()
    {
        foreach (var (hp, evade, draws, expectedWord) in new[] {
            (50, 0, 6ul, 2516284547u), (10, 0, 7ul, 3655513600u), (50, 100, 5ul, 4182499122u) })
        {
            var rng = new FftRandomState(1, ulong.MaxValue - draws); var result = Evaluate(State(hp: hp, evade: evade), rng: rng);
            var outcome = Success(result); Assert.Equal(new FftRandomState(expectedWord, ulong.MaxValue), result.RngState);
            if (hp == 10)
            {
                Assert.Equal(AiRankOrder.Above, outcome.Rank); Assert.Equal(320, outcome.Candidate.Score!.Priority);
                var projected = outcome.Candidate.Projection!.Outcome!;
                Assert.Equal((A, 13ul), (projected.MoveOpportunityUnitId, projected.ElapsedTicks));
                Assert.Collection(projected.UpkeepPicks, p => { Assert.Equal(5627u, p.RawRoll); Assert.True(p.Dead); },
                    p => { Assert.Equal(23010u, p.RawRoll); Assert.False(p.Dead); });
            }
            var shortBudget = rng with { DrawCount = rng.DrawCount + 1 };
            var failure = Atomic(Evaluate(State(hp: hp, evade: evade), rng: shortBudget),
                ThrowStoneAiBaselineComparisonStage.Candidate, shortBudget);
            Assert.Equal(ThrowStoneAiCandidateRejection.RandomDrawCountExhausted, failure.CandidateRejection);
        }
        var exhausted = new FftRandomState(1, ulong.MaxValue);
        Assert.Equal(ThrowStoneAiBaselineComparisonRejection.RandomDrawCountExhausted,
            Atomic(Evaluate(State(), rng: exhausted), ThrowStoneAiBaselineComparisonStage.Baseline, exhausted).Reason);
        Assert.Equal(ThrowStoneAiBaselineComparisonRejection.TickCountExhausted,
            Atomic(Evaluate(State(ticks: ulong.MaxValue)), ThrowStoneAiBaselineComparisonStage.Baseline).Reason);
        var late = Atomic(Evaluate(State(hp: 10, ticks: ulong.MaxValue - 1)), ThrowStoneAiBaselineComparisonStage.Candidate);
        Assert.Equal(ThrowStoneAiCandidateRejection.TickCountExhausted, late.CandidateRejection);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Baseline_source_facts_score_certificates_and_ability_geometry_remain_explicit()
    {
        foreach (var profile in new[] { Profile with { OriginalScenarioZeroDestination = false },
            Profile with { FreshSourceScoreCell = false }, Profile with { NormalScoring = false }, Profile with { CanonicalCoordinates = false } })
            Assert.Equal(ThrowStoneAiBaselineComparisonRejection.UnsupportedBaselineProfile,
                Atomic(Evaluate(State(), profile: profile), ThrowStoneAiBaselineComparisonStage.Baseline).Reason);
        var unsupported = new AiScoreProfile([new(A, AiScoreFacts.None), new(B, AiScoreFacts.ClosedProfile)]);
        Assert.Equal(ThrowStoneAiBaselineComparisonRejection.UnsupportedScoreProfile,
            Atomic(Evaluate(State(), score: unsupported), ThrowStoneAiBaselineComparisonStage.Baseline).Reason);
        Assert.Equal(AttackCommandRejection.AbilityNotGranted,
            Atomic(Evaluate(State(), candidateProfile: CandidateProfile(granted: false)), ThrowStoneAiBaselineComparisonStage.Baseline).AbilityRejection);
        Assert.Equal(AttackCommandRejection.UnsupportedSetup,
            Atomic(Evaluate(State(badGround: true)), ThrowStoneAiBaselineComparisonStage.Baseline).AbilityRejection);
        foreach (var state in new[] { State(acted: true), State(active: false) })
            Assert.Equal(ThrowStoneAiBaselineComparisonRejection.UnavailableAction,
                Atomic(Evaluate(state), ThrowStoneAiBaselineComparisonStage.Baseline).Reason);
        Assert.Equal(ThrowStoneAiBaselineComparisonRejection.TerminalInput,
            Atomic(Evaluate(State(hp: 0)), ThrowStoneAiBaselineComparisonStage.Baseline).Reason);
        foreach (var destination in new[] { new TilePosition(4, 1, 0), new(2, 1, 1), new(7, 7, 0) })
            Assert.Equal(ThrowStoneAiBaselineComparisonRejection.InvalidBaselineDestination,
                Atomic(Evaluate(State(), baseline: destination), ThrowStoneAiBaselineComparisonStage.Baseline).Reason);
        Assert.Equal(ThrowStoneAiCandidateRejection.InvalidCandidate,
            Atomic(Evaluate(State(), Candidate(new(4, 1, 0))), ThrowStoneAiBaselineComparisonStage.Candidate).CandidateRejection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_orders_replay_without_live_state_or_RNG_mutation()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = State(hp: 20, second: second); var before = Snapshot(state); var candidate = Candidate(Moved, second: second);
            var result = Evaluate(state, candidate, Moved); var outcome = Success(result);
            Assert.Equal(AiRankOrder.Above, outcome.Rank); Assert.Equal(135, outcome.Candidate.Score!.Priority);
            Assert.Equal(second ? A : B, Assert.Single(outcome.Candidate.Score.Contributions.Where(c => c.Enemy)).UnitId);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, candidate, Moved)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(hp: 20, second: second, reverse: true), candidate, Moved)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static ThrowStoneAiBaselineComparisonResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiCandidate? candidate = null,
        TilePosition? baseline = null, ThrowStoneAiBaselineProfile? profile = null, FftRandomState? rng = null,
        AiScoreProfile? score = null, ThrowStoneAiCandidateProfile? candidateProfile = null) =>
        ThrowStoneAiBaselineComparison.Evaluate(state, baseline ?? Origin, candidate ?? Candidate(), candidateProfile ?? CandidateProfile(),
            score ?? Score, profile ?? Profile, rng ?? Decision);
    private static ThrowStoneAiBaselineComparisonOutcome Success(ThrowStoneAiBaselineComparisonResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<ThrowStoneAiBaselineComparisonOutcome>(result.Outcome); }
    private static ThrowStoneAiBaselineComparisonFailure Atomic(ThrowStoneAiBaselineComparisonResult result,
        ThrowStoneAiBaselineComparisonStage stage, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<ThrowStoneAiBaselineComparisonFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure; }
}
