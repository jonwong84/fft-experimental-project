using Xunit;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ThrowStoneAiCandidateScanTests
{
    private static readonly TilePosition Origin = new(1, 1, 0), Near = new(2, 1, 0), Far = new(1, 0, 0), Down = new(1, 2, 0);
    private static readonly AiScoreProfile Score = new([new(A, AiScoreFacts.ClosedProfile), new(B, AiScoreFacts.ClosedProfile)]);
    private static readonly ThrowStoneAiBaselineProfile Baseline = new(true, true, true, true);
    private static readonly ThrowStoneAiScanProfile Profile = new(true, true, true, true, true, true, true, true);
    private static ThrowStoneAiScanScenario Scenario() => new([Near, Origin, Far], [Near, Origin, Far], Near, Near);

    [Fact]
    public void Prefix_uses_map_order_then_lowest_origin_key_and_carries_RNG_without_a_second_D0_score()
    {
        var state = State(); var result = Evaluate(state); var outcome = Success(result);
        Assert.Equal(new[] { Far, Origin, Near }, outcome.Movement.Select(m => m.Outcome.Destination));
        Assert.Equal(new byte[] { 6, 7, 8 }, outcome.Movement.Select(m => m.Proximity));
        Assert.Equal(new[] { 6, 7, 8 }, outcome.Movement.Select(m => m.OriginKey));
        Assert.Equal(new uint[] { 16838, 5758, 10113 }, outcome.Movement.Select(m => Assert.Single(m.Outcome.UpkeepPicks).RawRoll));
        Assert.Equal(2, outcome.BaselineIndex); Assert.Equal(0, outcome.Baseline.Score.Priority);
        var selected = Assert.IsType<ThrowStoneAiScannedCandidate>(outcome.Candidate);
        Assert.Equal(Far, selected.Origin); Assert.Equal(Far, selected.Candidate.Destination); Assert.False(selected.Candidate.ActBeforeMove);
        Assert.Equal(AiRankOrder.Above, outcome.Rank); Assert.Equal(26, selected.Evaluation.Score!.Priority);
        Assert.Equal(7419u, Assert.Single(selected.Evaluation.Projection!.Outcome!.UpkeepPicks).RawRoll);
        Assert.Equal(new FftRandomState(2633739833, 15), result.RngState); // Three prefix + four evasion + one upkeep.
        Assert.All(outcome.Movement, tile => Assert.Equal(50, tile.Outcome.Units.Single(u => u.UnitId == B).Hp));
        var independent = ThrowStoneAiCandidateScoring.Evaluate(state, selected.Candidate, CandidateProfile(), Score, new(662824084, 10));
        Assert.Equal(Serialize(independent), Serialize(selected.Evaluation));

        // Origin priority belongs to the actor tile, even though ACT-first substitutes the higher-proximity D1.
        var actFirst = Success(Evaluate(state, new([Origin, Near], [Origin, Near], Origin, Near))).Candidate!;
        Assert.Equal(Origin, actFirst.Origin); Assert.Equal(Near, actFirst.Candidate.Destination); Assert.True(actFirst.Candidate.ActBeforeMove);
    }

    [Fact]
    public void Equal_origin_keys_prefer_later_map_coordinates_and_CT_zero_does_not_filter_a_ready_enemy()
    {
        var scenario = new ThrowStoneAiScanScenario([Down, Near, Origin], [Down, Near, Origin], Origin, Near);
        var outcome = Success(Evaluate(State(targetCt: 0), scenario));
        Assert.All(outcome.Movement, tile => Assert.Equal(0, tile.OriginKey));
        Assert.Equal(Down, outcome.Candidate!.Origin); Assert.False(outcome.Candidate.Candidate.ActBeforeMove);
        var ready = Success(Evaluate(State(targetCt: 100)));
        Assert.Equal(Far, ready.Candidate!.Origin); Assert.Equal(0ul, ready.Candidate.Evaluation.Projection!.Outcome!.ElapsedTicks);
        Assert.Equal(6, ready.Movement[0].Proximity);
    }

    [Fact]
    public void Range_four_enters_by_move_while_range_five_inactive_and_empty_masks_keep_only_prefix_cost()
    {
        var state = State(target: new(6, 1, 0));
        var scenario = new ThrowStoneAiScanScenario([Origin, Near], [Origin, Near], Origin, Near);
        var outcome = Success(Evaluate(state, scenario));
        Assert.Equal(Near, outcome.Candidate!.Origin); Assert.False(outcome.Candidate.Candidate.ActBeforeMove);
        foreach (var (scan, profile) in new[] {
            (new ThrowStoneAiScanScenario([Origin, Near], [Origin], Origin, Origin), Profile),
            (scenario, Profile with { TargetActive = false }),
            (new ThrowStoneAiScanScenario([Origin, Near], [], Origin, Origin), Profile) })
        {
            var result = Evaluate(state, scan, profile: profile); outcome = Success(result);
            Assert.Equal(ThrowStoneAiScanDisposition.NoCandidates, result.Disposition);
            Assert.Null(outcome.Candidate); Assert.Null(outcome.Rank);
            Assert.Equal(new FftRandomState(2524885223, 9), result.RngState); Assert.Equal(2, outcome.Movement.Length);
        }
    }

    [Fact]
    public void Zero_accuracy_consumes_the_sole_aim_instead_of_retrying_a_clear_nonfront_origin()
    {
        var zeroFront = new TilePosition(0, 1, 0); var behind = new TilePosition(3, 1, 0);
        var state = State(target: new(2, 1, 0), move: 4, evade: 100);
        var scenario = new ThrowStoneAiScanScenario([zeroFront, Origin, behind], [zeroFront, Origin, behind], Origin, behind);
        var result = Evaluate(state, scenario); var outcome = Success(result);
        Assert.Equal(ThrowStoneAiScanDisposition.NotValuable, result.Disposition);
        Assert.Equal(zeroFront, outcome.Candidate!.Origin); Assert.Null(outcome.Candidate.Evaluation.Score); Assert.Null(outcome.Rank);
        Assert.Null(outcome.Candidate.Evaluation.Projection!.Outcome);
        Assert.Equal(new FftRandomState(3655513600, 14), result.RngState); // Three prefix + four evasion; no candidate clock.
        var otherAngle = ThrowStoneAiCandidateScoring.Evaluate(state, Candidate(behind, actBeforeMove: false), CandidateProfile(), Score, Decision);
        Assert.Equal(AiScoringDisposition.Scored, otherAngle.Disposition); Assert.Equal(100, otherAngle.Score!.HitPercent);
    }

    [Fact]
    public void Exact_KO_and_NotValuable_budgets_and_late_prefix_or_candidate_failures_are_atomic()
    {
        foreach (var (hp, evade, draws, word) in new[] { (50, 0, 8ul, 2633739833u), (10, 0, 9ul, 3210001534u), (50, 100, 7ul, 3655513600u) })
        {
            var rng = new FftRandomState(1, ulong.MaxValue - draws);
            var result = Evaluate(State(hp: hp, evade: evade), rng: rng); var outcome = Success(result);
            Assert.Equal(new FftRandomState(word, ulong.MaxValue), result.RngState);
            if (hp == 10)
            {
                Assert.Equal(320, outcome.Candidate!.Evaluation.Score!.Priority);
                Assert.Equal(2, outcome.Candidate.Evaluation.Projection!.Outcome!.UpkeepPicks.Length);
                Assert.Equal(50, outcome.Baseline.Units.Single(u => u.UnitId == A).Hp);
            }
            var shortBudget = rng with { DrawCount = rng.DrawCount + 1 };
            var failure = Atomic(Evaluate(State(hp: hp, evade: evade), rng: shortBudget), ThrowStoneAiScanStage.Candidate, shortBudget);
            Assert.Equal(ThrowStoneAiCandidateRejection.RandomDrawCountExhausted, failure.Detail!.CandidateRejection);
        }
        var prefixBudget = new FftRandomState(1, ulong.MaxValue - 1);
        var prefix = Atomic(Evaluate(State(), rng: prefixBudget), ThrowStoneAiScanStage.Movement, prefixBudget);
        Assert.Equal(1, prefix.MovementIndex); Assert.Equal(ThrowStoneAiBaselineComparisonRejection.RandomDrawCountExhausted, prefix.Detail!.Reason);
        var twoTiles = new ThrowStoneAiScanScenario([Origin, Near], [Origin, Near], Origin, Near);
        var ticks = Atomic(Evaluate(State(targetCt: 0, ticks: ulong.MaxValue - 9), twoTiles), ThrowStoneAiScanStage.Movement);
        Assert.Equal(1, ticks.MovementIndex); Assert.Equal(ThrowStoneAiBaselineComparisonRejection.TickCountExhausted, ticks.Detail!.Reason);
        var late = Atomic(Evaluate(State(hp: 10, targetCt: 100, ticks: ulong.MaxValue)), ThrowStoneAiScanStage.Candidate);
        Assert.Equal(ThrowStoneAiCandidateRejection.TickCountExhausted, late.Detail!.CandidateRejection);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Source_certificates_and_complete_reachable_score_cells_are_required_before_any_prefix()
    {
        foreach (var profile in new[] { Profile with { OriginalMovementScoreMask = false }, Profile with { OriginalScenarioMask = false },
            Profile with { PreparedThrowStoneEntry = false }, Profile with { FreshDecisionBaseline = false },
            Profile with { FreshAbilityBestReset = false }, Profile with { NoInterveningRandomWork = false }, Profile with { ClearDirectTrajectories = false } })
            Assert.Equal(ThrowStoneAiScanRejection.UnsupportedScanProfile, Atomic(Evaluate(State(), profile: profile), ThrowStoneAiScanStage.Admission).Reason);
        foreach (var scenario in new[] {
            new ThrowStoneAiScanScenario([Origin, Origin], [Origin], Origin, Origin),
            new ThrowStoneAiScanScenario([Origin], [Near], Origin, Near),
            new ThrowStoneAiScanScenario([Origin, Near], [Origin], Origin, Near),
            new ThrowStoneAiScanScenario([Origin, new(4, 1, 0)], [Origin], Origin, Origin) })
            Assert.Equal(ThrowStoneAiScanRejection.InvalidScenario, Atomic(Evaluate(State(), scenario), ThrowStoneAiScanStage.Admission).Reason);
        Assert.Equal(ThrowStoneAiBaselineComparisonRejection.UnsupportedBaselineProfile,
            Atomic(Evaluate(State(), baseline: new()), ThrowStoneAiScanStage.Admission).Detail!.Reason);
        Assert.Equal(AttackCommandRejection.AbilityNotGranted,
            Atomic(Evaluate(State(), candidateProfile: CandidateProfile(granted: false)), ThrowStoneAiScanStage.Admission).Detail!.AbilityRejection);
        Assert.Equal(AttackCommandRejection.UnsupportedSetup,
            Atomic(Evaluate(State(badGround: true)), ThrowStoneAiScanStage.Admission).Detail!.AbilityRejection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actors_replay_independently_of_state_and_supplied_mask_order_without_live_changes()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = State(hp: 20, second: second); var before = Snapshot(state);
            var result = Evaluate(state, second: second); var outcome = Success(result);
            Assert.Equal(135, outcome.Candidate!.Evaluation.Score!.Priority); Assert.Equal(AiRankOrder.Above, outcome.Rank);
            Assert.Equal(second ? A : B, Assert.Single(outcome.Candidate.Evaluation.Score.Contributions.Where(c => c.Enemy)).UnitId);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, second: second)));
            var scenario = Scenario(); var reversed = new ThrowStoneAiScanScenario(scenario.ScoredOrigins.Reverse(), scenario.ScenarioOrigins.Reverse(), Near, Near);
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(hp: 20, second: second, reverse: true), reversed, second: second)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static ThrowStoneAiScanResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiScanScenario? scenario = null,
        ThrowStoneAiScanProfile? profile = null, FftRandomState? rng = null, bool second = false,
        ThrowStoneAiBaselineProfile? baseline = null, ThrowStoneAiCandidateProfile? candidateProfile = null) =>
        ThrowStoneAiCandidateScan.Evaluate(state, new(second ? B : A, Stone, second ? A : B), scenario ?? Scenario(),
            candidateProfile ?? CandidateProfile(), Score, baseline ?? Baseline, profile ?? Profile, rng ?? Decision);
    private static ThrowStoneAiScanOutcome Success(ThrowStoneAiScanResult result)
    { Assert.NotEqual(ThrowStoneAiScanDisposition.Rejected, result.Disposition); Assert.Null(result.Failure); return Assert.IsType<ThrowStoneAiScanOutcome>(result.Outcome); }
    private static ThrowStoneAiScanFailure Atomic(ThrowStoneAiScanResult result, ThrowStoneAiScanStage stage, FftRandomState? rng = null)
    { Assert.Equal(ThrowStoneAiScanDisposition.Rejected, result.Disposition); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<ThrowStoneAiScanFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure; }
}
