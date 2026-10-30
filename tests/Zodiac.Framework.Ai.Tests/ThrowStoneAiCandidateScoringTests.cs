using Xunit;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ThrowStoneAiCandidateScoringTests
{
    private static readonly AiScoreProfile Score = new([new(A, AiScoreFacts.ClosedProfile), new(B, AiScoreFacts.ClosedProfile)]);

    [Fact]
    public void Fresh_normal_Critical_and_KO_scores_use_the_shared_absolute_terms_with_separate_accuracy()
    {
        foreach (var (hp, evade, expected, status, count) in new[] {
            (50, 0, 26, 0, 12ul), (50, 75, 26, 0, 12ul), (20, 0, 135, -32, 12ul), (10, 0, 320, -192, 13ul) })
        {
            var state = State(hp: hp, evade: evade); var before = Snapshot(state); var result = Evaluate(state); var score = Success(result);
            Assert.Equal(expected, score.Priority); Assert.Equal(100 - evade, score.HitPercent); Assert.Equal(count, result.RngState.DrawCount);
            Assert.Equal(status, score.Contributions.Single(c => c.UnitId == B).StatusTerm);
            Assert.All(score.Contributions, c => Assert.Equal(357, c.EquipmentTerm));
            var projection = ThrowStoneAiCandidateProjectionTests.Evaluate(state);
            Assert.Equal(Serialize(projection), Serialize(result.Projection)); Assert.Equal(projection.RngState, result.RngState);
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Zero_accuracy_has_no_score_or_clock_and_unsupported_score_facts_reject_before_projection()
    {
        var result = Evaluate(State(evade: 100, ticks: ulong.MaxValue));
        Assert.Equal(AiScoringDisposition.NotValuable, result.Disposition); Assert.Null(result.Score);
        Assert.Equal(ThrowStoneAiCandidateDisposition.NotValuable, result.Projection!.Disposition);
        Assert.Equal(new FftRandomState(3295386429, 11), result.RngState);
        var unsupported = new AiScoreProfile([new(A, AiScoreFacts.None), new(B, AiScoreFacts.ClosedProfile)]);
        result = Evaluate(State(), profile: unsupported);
        Assert.Equal(AiScoringDisposition.Rejected, result.Disposition); Assert.Equal(AiScoringRejection.UnsupportedScoreProfile, result.Rejection);
        Assert.Null(result.Projection); Assert.Null(result.Score); Assert.Equal(Decision, result.RngState);
    }

    [Fact]
    public void Exact_budget_and_late_projection_failure_publish_no_score_or_progressed_RNG()
    {
        var last = new FftRandomState(1, ulong.MaxValue - 5); var fits = Evaluate(State(), rng: last); Success(fits);
        Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var shortBudget = last with { DrawCount = ulong.MaxValue - 4 };
        var result = Evaluate(State(), rng: shortBudget);
        Assert.Equal(AiScoringDisposition.Rejected, result.Disposition); Assert.Equal(AiScoringRejection.ProjectionRejected, result.Rejection);
        Assert.Equal(ThrowStoneAiCandidateRejection.RandomDrawCountExhausted, result.Projection!.Rejection);
        Assert.Null(result.Score); Assert.Null(result.Projection.Outcome); Assert.Equal(shortBudget, result.RngState);
        result = Evaluate(State(badGround: true)); Assert.Equal(AiScoringRejection.ProjectionRejected, result.Rejection);
        Assert.Null(result.Score); Assert.Equal(Decision, result.RngState);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_orders_replay_with_current_target_sign_and_no_mutation()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = State(hp: 20, second: second); var before = Snapshot(state); var candidate = Candidate(second: second);
            var result = Evaluate(state, candidate); var score = Success(result); var target = second ? A : B;
            Assert.Equal(135, score.Priority); Assert.Equal(target, Assert.Single(score.Contributions.Where(c => c.Enemy)).UnitId);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, candidate)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(hp: 20, second: second, reverse: true), candidate)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static ThrowStoneAiScoringResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiCandidate? candidate = null,
        AiScoreProfile? profile = null, FftRandomState? rng = null) =>
        ThrowStoneAiCandidateScoring.Evaluate(state, candidate ?? Candidate(), CandidateProfile(), profile ?? Score, rng ?? Decision);
    private static AiCandidateScore Success(ThrowStoneAiScoringResult result)
    { Assert.Equal(AiScoringDisposition.Scored, result.Disposition); Assert.Equal(AiScoringRejection.None, result.Rejection);
        return Assert.IsType<AiCandidateScore>(result.Score); }
}
