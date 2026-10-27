using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

/// <summary>Closed managed pair regressions and explicitly isolated source rank kernels; no original execution.</summary>
public sealed class SwordAiCandidateComparisonTests
{
    private static readonly BattleUnitId A = new("sample:unit/ramza"), B = new("sample:unit/delita");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly SwordAiCandidateProfile CandidateProfile = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 255)]);
    private static readonly SwordAiScoreProfile ScoreProfile = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
    private static readonly FftRandomState Decision = new(1, 7);

    public static IEnumerable<object[]> Proposals()
    {
        using var document = Contract();
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray().Where(r => r.GetProperty("family").GetString() == "proposed-pair"))
        foreach (bool reverse in new[] { false, true }) yield return [row.GetProperty("id").GetString()!, reverse];
    }

    [Theory] [MemberData(nameof(Proposals))]
    public void Executes_22_normalized_pair_proposals_with_real_geometry_formula_and_both_collection_orders(string id, bool reverse)
    {
        using var document = Contract();
        var row = document.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var d = row.GetProperty("inputs"); var e = row.GetProperty("expected"); var descriptions = d.GetProperty("candidates");
        var scoring = descriptions[0].GetProperty("scoring"); var c = scoring.GetProperty("candidate");
        bool second = d.GetProperty("actorId").GetString() == "Delita";
        var state = State(second: second, reverse: reverse || id == "pair-reverse-collection",
            actorHp: scoring.GetProperty("actorHp").GetInt32(), actorMax: scoring.GetProperty("actorMaxHp").GetInt32(),
            hp: c.GetProperty("targetHp").GetInt32(), maxHp: c.GetProperty("targetMaxHp").GetInt32(),
            evade: 100 - descriptions.EnumerateArray().Min(r => r.GetProperty("scoring").GetProperty("candidate").GetProperty("hitPercent").GetInt32()),
            ct: c.GetProperty("actorCt").GetInt32(), targetCt: c.GetProperty("targetCt").GetInt32(),
            speed: c.GetProperty("actorSpeed").GetInt32(), targetSpeed: c.GetProperty("targetSpeed").GetInt32(),
            slot: c.GetProperty("actorSlot").GetInt32(), ticks: c.GetProperty("elapsedTicks").GetUInt64(),
            origin: Position(c.GetProperty("origin")), enemy: Position(d.GetProperty("targetPosition")));
        var before = Snapshot(state);
        SwordAiCandidate Materialize(int i)
        {
            var data = descriptions[i].GetProperty("scoring").GetProperty("candidate");
            // A supplied false membership claim is materialized as the occupied enemy destination, never trusted as input.
            var destination = descriptions[i].GetProperty("scenarioZeroMember").GetBoolean()
                ? Position(data.GetProperty("destination")) : Position(d.GetProperty("targetPosition"));
            return Candidate(second, destination, Enum.Parse<SwordAiCandidateOrder>(data.GetProperty("order").GetString()!));
        }
        var profile = new SwordAiComparisonProfile(second ? B : A, second ? A : B, d.GetProperty("freshDecisionBaseline").GetBoolean());
        if (!d.GetProperty("sameActorAndTarget").GetBoolean()) profile = profile with { TargetId = new("sample:unit/other") };
        var facts = scoring.GetProperty("profile").GetProperty("units").EnumerateArray().Any(u => u.GetProperty("monster").GetBoolean())
            ? SwordAiScoreFacts.ClosedProfile & ~SwordAiScoreFacts.NonMonster : SwordAiScoreFacts.ClosedProfile;
        var scoreProfile = new SwordAiScoreProfile([new(A, facts), new(B, facts)]);
        var rng = new FftRandomState(d.GetProperty("rngWord").GetUInt32(), d.GetProperty("rngCount").GetUInt64());
        var result = Evaluate(state, Materialize(0), Materialize(1), c.GetProperty("damage").GetInt32() / 5,
            rng: rng, comparisonProfile: profile, scoreProfile: scoreProfile);
        Assert.Equal(e.GetProperty("disposition").GetString(), result.Disposition.ToString());
        Assert.Equal(new FftRandomState(e.GetProperty("rngWord").GetUInt32(), e.GetProperty("rngCount").GetUInt64()), result.RngState);
        Assert.Equal(before, Snapshot(state));
        if (result.Disposition == SwordAiComparisonDisposition.Rejected)
        {
            Assert.Equal(e.GetProperty("reason").GetString(), result.Rejection.ToString()); Assert.Null(result.Outcome);
            if (result.Rejection == SwordAiComparisonRejection.EvaluationRejected)
            {
                Assert.Equal(e.GetProperty("failedIndex").GetInt32(), result.FailedCandidateIndex);
                Assert.Equal(e.GetProperty("evaluationRejection").GetProperty("reason").GetString(), result.ScoringRejection.ToString());
                var projection = e.GetProperty("evaluationRejection").GetProperty("projectionReason");
                Assert.Equal(projection.ValueKind == JsonValueKind.Null ? "None" : projection.GetString(), result.ProjectionRejection.ToString());
            }
            return;
        }
        var outcome = Success(result);
        Assert.Equal(e.GetProperty("retained").ValueKind == JsonValueKind.Null ? null : (int?)e.GetProperty("retained").GetInt32(), outcome.RetainedCandidateIndex);
        Assert.Equal(e.GetProperty("baselineActorDelay").GetInt32(), outcome.Cache.ActorDelay);
        Assert.Equal(e.GetProperty("baselineTargetDelay").GetInt32(), outcome.Cache.TargetDelay);
        Assert.Equal(e.GetProperty("eligibleEnemy").GetBoolean(), outcome.Cache.EligibleEnemyId is not null);
        Assert.Equal(e.GetProperty("proximity").EnumerateArray().Select(v => v.GetInt32()), outcome.Cache.Destinations.Select(v => (int)v.Value));
        for (int i = 0; i < 2; i++)
        {
            var expected = e.GetProperty("evaluations")[i]; var actual = outcome.Candidates[i].Evaluation;
            Assert.Equal(expected.GetProperty("disposition").GetString(), actual.Disposition.ToString());
            Assert.Equal(new FftRandomState(expected.GetProperty("rngWord").GetUInt32(), expected.GetProperty("rngCount").GetUInt64()), actual.RngState);
            if (expected.GetProperty("score").ValueKind == JsonValueKind.Null) Assert.Null(actual.Score);
            else
            {
                Assert.Equal(expected.GetProperty("score").GetProperty("priority").GetInt32(), actual.Score!.Priority);
                Assert.Equal(expected.GetProperty("score").GetProperty("hitPercent").GetInt32(), actual.Score.HitPercent);
            }
        }
    }

    public static IEnumerable<object[]> RankKernels()
    {
        using var document = Contract();
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray().Where(r => r.GetProperty("family").GetString() == "source-rank"))
            if (row.GetProperty("inputs").GetProperty("first").GetProperty("decided").GetInt32() == 0 &&
                row.GetProperty("inputs").GetProperty("second").GetProperty("decided").GetInt32() == 0)
                foreach (bool swap in new[] { false, true }) yield return [row.GetProperty("id").GetString()!, swap];
    }

    [Theory] [MemberData(nameof(RankKernels))]
    public void Isolated_private_rank_kernel_matches_source_signed_priority_hit_and_strict_proximity(string id, bool swap)
    {
        using var document = Contract();
        var row = document.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var first = row.GetProperty("inputs").GetProperty(swap ? "second" : "first");
        var second = row.GetProperty("inputs").GetProperty(swap ? "first" : "second");
        // Diagnostic source kernel only: priorities outside -160..320 cannot enter the public closed pair.
        var method = typeof(SwordAiCandidateComparison).GetMethod("Compare", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = ((bool ChallengerIsHigher, SwordAiComparisonReason Reason))method.Invoke(null,
            [(short)second.GetProperty("priority").GetInt32(), (byte)second.GetProperty("hit").GetInt32(), (byte)second.GetProperty("proximity").GetInt32(),
             (short)first.GetProperty("priority").GetInt32(), (byte)first.GetProperty("hit").GetInt32(), (byte)first.GetProperty("proximity").GetInt32()])!;
        bool expected = first.GetProperty("priority").GetInt32() > second.GetProperty("priority").GetInt32() ||
            first.GetProperty("priority").GetInt32() == second.GetProperty("priority").GetInt32() &&
            (first.GetProperty("hit").GetInt32() > second.GetProperty("hit").GetInt32() ||
             first.GetProperty("hit").GetInt32() == second.GetProperty("hit").GetInt32() && first.GetProperty("proximity").GetInt32() < second.GetProperty("proximity").GetInt32());
        Assert.Equal(expected, result.ChallengerIsHigher);
        if (!swap) Assert.Equal(row.GetProperty("expected").GetProperty("firstIsHigher").GetBoolean(), result.ChallengerIsHigher);
        var reason = first.GetProperty("priority").GetInt32() != second.GetProperty("priority").GetInt32() ? SwordAiComparisonReason.Priority :
            first.GetProperty("hit").GetInt32() != second.GetProperty("hit").GetInt32() ? SwordAiComparisonReason.HitPercent :
            first.GetProperty("proximity").GetInt32() != second.GetProperty("proximity").GetInt32() ? SwordAiComparisonReason.Proximity : SwordAiComparisonReason.Equal;
        Assert.Equal(reason, result.Reason);
    }

    [Theory] [InlineData(50, 0, 0)] [InlineData(10, 0, -135)] [InlineData(10, 5, -71)]
    public void Zero_and_negative_absolute_priority_are_real_scores_and_exact_ties_keep_incumbent(int actorHp, int power, int priority)
    {
        var result = Evaluate(State(actorHp: actorHp), power: power); var outcome = Success(result);
        Assert.Equal(0, outcome.RetainedCandidateIndex); Assert.Equal(SwordAiComparisonReason.Equal, outcome.Reason);
        Assert.All(outcome.Candidates, c => Assert.Equal(priority, c.Evaluation.Score!.Priority));
    }

    [Theory]
    [InlineData(0, 1, 0, 1, 100, 100, true)] [InlineData(0, 255, 0, 255, 1, 1, true)]
    [InlineData(15, 5, 15, 5, 17, 17, true)] [InlineData(15, 5, 0, 1, 17, 100, false)]
    [InlineData(99, 1, 98, 1, 1, 2, false)] [InlineData(99, 255, 99, 255, 1, 1, true)]
    [InlineData(0, 9, 0, 10, 12, 10, true)] [InlineData(0, 10, 0, 9, 10, 12, false)]
    [InlineData(15, 5, 100, 1, 17, 0, true)] [InlineData(15, 5, 254, 255, 17, 0, true)]
    public void Baseline_ordinary_delay_uses_ceiling_byte_storage_and_inclusive_eligibility(int ct, int speed, int targetCt, int targetSpeed,
        int actorDelay, int targetDelay, bool eligible)
    {
        var result = Evaluate(State(ct: ct, speed: speed, targetCt: targetCt, targetSpeed: targetSpeed));
        var cache = Success(result).Cache;
        Assert.Equal(actorDelay, cache.ActorDelay); Assert.Equal(targetDelay, cache.TargetDelay);
        Assert.Equal(eligible ? B : (BattleUnitId?)null, cache.EligibleEnemyId);
        Assert.All(cache.Destinations, c => Assert.Equal(eligible ? 9 : 0, c.Value));
    }

    [Theory] [InlineData(10, 11, 1, 0, 1)] [InlineData(11, 12, 0, 0, 0)] [InlineData(12, 10, 0, 1, 0)]
    public void Proximity_clips_at_ten_and_uses_final_destinations_even_for_ActThenMove(int firstX, int secondX, int firstValue, int secondValue, int retained)
    {
        var state = State(width: 16, move: 20);
        var result = Evaluate(state, Candidate(destination: new(firstX, 0, 0), order: SwordAiCandidateOrder.ActThenMove),
            Candidate(destination: new(secondX, 0, 0), order: SwordAiCandidateOrder.ActThenMove));
        var outcome = Success(result);
        Assert.Equal(new[] { firstValue, secondValue }, outcome.Cache.Destinations.Select(c => (int)c.Value));
        Assert.Equal(retained, outcome.RetainedCandidateIndex);
        Assert.Equal(firstValue == secondValue ? SwordAiComparisonReason.Equal : SwordAiComparisonReason.Proximity, outcome.Reason);
        Assert.All(outcome.Candidates, c => Assert.Equal(64, c.Evaluation.Score!.Priority));
    }

    [Theory] [InlineData(1)] [InlineData(50)] [InlineData(99)]
    public void Accuracy_precedes_proximity_and_is_never_multiplied_into_priority(int evade)
    {
        var state = State(evade: evade);
        var result = Evaluate(state, Candidate(destination: new(2, 0, 0)), Candidate());
        var outcome = Success(result);
        Assert.Equal(new[] { 100 - evade, 100 }, outcome.Candidates.Select(c => (int)c.Evaluation.Score!.HitPercent));
        Assert.All(outcome.Candidates, c => Assert.Equal(64, c.Evaluation.Score!.Priority));
        Assert.Equal(1, outcome.RetainedCandidateIndex); Assert.Equal(SwordAiComparisonReason.HitPercent, outcome.Reason);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Baseline_eligible_foe_survives_both_lethal_projections_and_clock_changes(bool reverse)
    {
        var state = State(reverse: reverse); var before = Snapshot(state);
        var outcome = Success(Evaluate(state, power: 10));
        Assert.Equal(B, outcome.Cache.EligibleEnemyId); Assert.All(outcome.Cache.Destinations, c => Assert.Equal(9, c.Value));
        Assert.All(outcome.Candidates, c => { Assert.Equal(320, c.Evaluation.Score!.Priority); Assert.True(c.Evaluation.Projection!.Outcome!.TargetKnockedOut); });
        Assert.Equal(before, Snapshot(state)); Assert.Equal(50, state.Combatants.Single(u => u.UnitId == B).Hp);
        Assert.Equal(new FftRandomState(57, 100), state.TurnBattle.Turns.RngState);
    }

    public static IEnumerable<object[]> Budgets()
    {
        foreach (int kind in new[] { 0, 1, 2 }) for (int remaining = 0; remaining <= 12; remaining++) yield return [kind, remaining];
    }

    [Theory] [MemberData(nameof(Budgets))]
    public void Every_pair_budget_is_atomic_and_exact_MAX_success_needs_no_comparison_draw(int kind, int remaining)
    {
        bool notValuable = kind == 2; int each = notValuable ? 4 : kind == 1 ? 6 : 5;
        var state = State(evade: notValuable ? 100 : 0); var rng = new FftRandomState(1, ulong.MaxValue - (ulong)remaining);
        var candidate = notValuable ? Candidate(destination: new(2, 0, 0)) : Candidate();
        var result = Evaluate(state, candidate, candidate, kind == 1 ? 10 : 5, rng: rng);
        if (remaining < each * 2)
        {
            Failed(result, SwordAiComparisonRejection.EvaluationRejected, rng);
            Assert.Equal(remaining < each ? 0 : 1, result.FailedCandidateIndex);
            Assert.Equal(SwordAiScoringRejection.ProjectionRejected, result.ScoringRejection);
            Assert.Equal(SwordAiCandidateRejection.RandomDrawCountExhausted, result.ProjectionRejection);
        }
        else
        {
            Success(result); Assert.Equal(rng.DrawCount + (ulong)(each * 2), result.RngState.DrawCount);
            Assert.Equal(notValuable ? SwordAiComparisonDisposition.NoScoredCandidate : SwordAiComparisonDisposition.Compared, result.Disposition);
        }
    }

    [Theory] [InlineData(0, 50, 5)] [InlineData(1, 50, 5)] [InlineData(2, 50, 5)]
    [InlineData(0, 50, 0)] [InlineData(1, 50, 0)] [InlineData(0, 10, 5)] [InlineData(1, 10, 5)]
    public void NotValuable_is_absent_and_all_supported_draws_are_carried(int absent, int actorHp, int power)
    {
        var state = State(evade: 100, actorHp: actorHp); var noAccuracy = Candidate(destination: new(2, 0, 0));
        var first = absent == 0 || absent == 2 ? noAccuracy : Candidate();
        var second = absent == 1 || absent == 2 ? noAccuracy : Candidate();
        var result = Evaluate(state, first, second, power); var outcome = Success(result);
        Assert.Equal(absent == 2 ? null : (int?)(absent == 0 ? 1 : 0), outcome.RetainedCandidateIndex);
        Assert.Equal(absent == 2 ? 15ul : 16ul, result.RngState.DrawCount);
        Assert.Equal(absent == 2 ? SwordAiComparisonReason.NeitherScored : absent == 0 ? SwordAiComparisonReason.ChallengerOnly : SwordAiComparisonReason.IncumbentOnly, outcome.Reason);
        foreach (var c in outcome.Candidates)
            if (c.Evaluation.Disposition == SwordAiScoringDisposition.NotValuable) Assert.Null(c.Evaluation.Score);
        if (absent != 2) Assert.Equal(actorHp == 10 ? -71 : power == 0 ? 0 : 64,
            outcome.Candidates[outcome.RetainedCandidateIndex!.Value].Evaluation.Score!.Priority);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Late_tick_failure_rolls_back_pair_but_two_already_ready_queries_succeed_at_MAX_ticks(bool ready)
    {
        var state = State(targetCt: ready ? 254 : 0, targetSpeed: ready ? 5 : 1, ticks: ready ? ulong.MaxValue : ulong.MaxValue - 13);
        var result = Evaluate(state, Candidate(), Candidate(destination: new(0, 1, 0), order: SwordAiCandidateOrder.ActThenMove));
        if (ready)
        { Assert.All(Success(result).Candidates, c => Assert.Equal(0ul, c.Evaluation.Projection!.Outcome!.ElapsedTicks)); Assert.Equal(17ul, result.RngState.DrawCount); }
        else
        { Failed(result, SwordAiComparisonRejection.EvaluationRejected); Assert.Equal(1, result.FailedCandidateIndex); Assert.Equal(SwordAiCandidateRejection.TickCountExhausted, result.ProjectionRejection); }
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Missing_default_false_or_mismatched_comparison_certificate_rejects_before_invalid_weapon_and_rng(int kind)
    {
        var profile = kind switch
        {
            0 => new SwordAiComparisonProfile(A, B), 1 => new(A, B, false), 2 => new(default, B, true),
            3 => new(A, default, true), 4 => new(A, A, true), _ => new(B, A, true)
        };
        var rng = new FftRandomState(1, ulong.MaxValue);
        var result = SwordAiCandidateComparison.Evaluate(State(), Candidate(), Candidate(), null, CandidateProfile, ScoreProfile, profile, rng);
        Failed(result, SwordAiComparisonRejection.UnsupportedComparisonProfile, rng); Assert.Null(result.FailedCandidateIndex);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Cross_actor_target_and_unknown_pairs_are_rejected_without_scores(int kind)
    {
        var challenger = kind switch { 0 => Candidate(true), 1 => Candidate() with { TargetId = A }, _ => Candidate() with { TargetId = new("sample:unit/other") } };
        Failed(Evaluate(State(), challenger: challenger), SwordAiComparisonRejection.InvalidCandidatePair);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Invalid_or_unwritten_challenger_destination_is_preflighted_before_a_failing_first_score(int kind)
    {
        var state = State(moved: kind == 4);
        var tile = kind switch { 0 => new TilePosition(1, 0, 0), 1 => new(-1, 0, 0), 2 => new(0, 0, 1), 3 => new(5, 0, 0), _ => new(0, 1, 0) };
        var rng = new FftRandomState(1, ulong.MaxValue);
        var result = SwordAiCandidateComparison.Evaluate(state, Candidate(), Candidate(destination: tile), null, CandidateProfile, ScoreProfile, new(A, B, true), rng);
        Failed(result, SwordAiComparisonRejection.InvalidCacheDestination, rng); Assert.Equal(1, result.FailedCandidateIndex);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Unsupported_geometry_and_initial_terminal_state_publish_no_cache(int kind)
    {
        var state = State(width: kind == 0 ? 17 : 3, height: kind == 1 ? 11 : 10, cost: kind == 2 ? 2 : 1, hp: kind == 3 ? 0 : 50);
        Failed(Evaluate(state), SwordAiComparisonRejection.UnsupportedMovementProfile);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(16)] [InlineData(-1)]
    public void Unsupported_score_facts_preserve_original_typed_reason_and_no_partial_payload(int facts)
    {
        var profile = new SwordAiScoreProfile([new(A, (SwordAiScoreFacts)facts), new(B, SwordAiScoreFacts.ClosedProfile)]);
        var result = Evaluate(State(), scoreProfile: profile);
        Failed(result, SwordAiComparisonRejection.EvaluationRejected); Assert.Equal(0, result.FailedCandidateIndex);
        Assert.Equal(SwordAiScoringRejection.UnsupportedScoreProfile, result.ScoringRejection); Assert.Equal(SwordAiCandidateRejection.None, result.ProjectionRejection);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Candidate_order_and_action_origin_rejections_retain_the_failed_index_and_projection_reason(bool invalidOrder)
    {
        var challenger = invalidOrder ? Candidate() with { Order = (SwordAiCandidateOrder)99 } : Candidate(destination: new(0, 1, 0));
        var result = Evaluate(State(), challenger: challenger);
        Failed(result, SwordAiComparisonRejection.EvaluationRejected); Assert.Equal(1, result.FailedCandidateIndex);
        Assert.Equal(SwordAiScoringRejection.ProjectionRejected, result.ScoringRejection);
        Assert.Equal(invalidOrder ? SwordAiCandidateRejection.InvalidCandidate : SwordAiCandidateRejection.OutOfRange, result.ProjectionRejection);
    }

    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Fresh_scores_match_direct_queries_with_carried_rng_for_both_actor_IDs_and_original_slot_orders(bool second, bool enemyFirst)
    {
        var state = State(second: second, slot: enemyFirst ? 1 : 0, reverse: true); var before = Snapshot(state);
        var candidate = Candidate(second); var profile = new SwordAiComparisonProfile(candidate.ActorId, candidate.TargetId, true);
        var first = SwordAiCandidateScoring.Evaluate(state, candidate, new(Weapon, 5), CandidateProfile, ScoreProfile, Decision);
        var next = SwordAiCandidateScoring.Evaluate(state, candidate, new(Weapon, 5), CandidateProfile, ScoreProfile, first.RngState);
        var result = Evaluate(state, candidate, candidate, comparisonProfile: profile); var outcome = Success(result);
        Assert.Equal(Serialize(first), Serialize(outcome.Candidates[0].Evaluation));
        Assert.Equal(Serialize(next), Serialize(outcome.Candidates[1].Evaluation)); Assert.Equal(next.RngState, result.RngState);
        Assert.All(outcome.Candidates, c => Assert.Equal(new[] { 0, 1 }, c.Evaluation.Score!.Contributions.Select(u => u.TieKey)));
        Assert.Equal(before, Snapshot(state));
        Assert.Equal(Serialize(result), Serialize(Evaluate(state, candidate, candidate, comparisonProfile: profile)));
        var branch = Evaluate(state, candidate, candidate, rng: result.RngState, comparisonProfile: profile);
        Assert.Equal(result.RngState.DrawCount + 10, branch.RngState.DrawCount); Assert.NotEqual(result.RngState, branch.RngState);
    }

    [Fact]
    public void Public_entry_accepts_original_inputs_only_nulls_are_programmer_errors_and_no_command_authority_is_created()
    {
        var method = Assert.Single(typeof(SwordAiCandidateComparison).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.Equal("Evaluate", method.Name);
        Assert.Equal(new[] { typeof(AttackBattleSnapshot), typeof(SwordAiCandidate), typeof(SwordAiCandidate), typeof(BoundedSwordWeapon),
            typeof(SwordAiCandidateProfile), typeof(SwordAiScoreProfile), typeof(SwordAiComparisonProfile), typeof(FftRandomState) }, method.GetParameters().Select(p => p.ParameterType));
        var args = new object?[] { State(), Candidate(), Candidate(), new BoundedSwordWeapon(Weapon, 5), CandidateProfile, ScoreProfile, new SwordAiComparisonProfile(A, B, true), Decision };
        foreach (int index in new[] { 0, 1, 2, 4, 5, 6 })
        {
            var copy = args.ToArray(); copy[index] = null;
            Assert.IsType<ArgumentNullException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, copy)).InnerException);
        }
        var noWeapon = args.ToArray(); noWeapon[3] = null;
        var rejected = Assert.IsType<SwordAiComparisonResult>(method.Invoke(null, noWeapon));
        Failed(rejected, SwordAiComparisonRejection.EvaluationRejected); Assert.Equal(SwordAiCandidateRejection.UnsupportedProfile, rejected.ProjectionRejection);
        var state = State(acted: true); var result = Evaluate(state);
        Failed(result, SwordAiComparisonRejection.EvaluationRejected); Assert.Equal(SwordAiCandidateRejection.UnavailableAction, result.ProjectionRejection);
        var certificate = new SwordAiComparisonProfile(A, B); var certified = certificate with { FreshDecisionBaseline = true };
        Assert.False(certificate.FreshDecisionBaseline); Assert.True(certified.FreshDecisionBaseline);
    }

    private static JsonDocument Contract() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-comparison-contract-v1.json")));
    private static TilePosition Position(JsonElement a) => new(a[0].GetInt32(), a[1].GetInt32(), 0);
    private static SwordAiCandidate Candidate(bool second = false, TilePosition? destination = null, SwordAiCandidateOrder order = SwordAiCandidateOrder.MoveThenAct) =>
        new(second ? B : A, second ? A : B, destination ?? new(0, 0, 0), order);
    private static SwordAiComparisonResult Evaluate(AttackBattleSnapshot state, SwordAiCandidate? incumbent = null, SwordAiCandidate? challenger = null,
        int power = 5, FftRandomState? rng = null, SwordAiComparisonProfile? comparisonProfile = null, SwordAiScoreProfile? scoreProfile = null) =>
        SwordAiCandidateComparison.Evaluate(state, incumbent ?? Candidate(), challenger ?? Candidate(), new(Weapon, power), CandidateProfile,
            scoreProfile ?? ScoreProfile, comparisonProfile ?? new((incumbent ?? Candidate()).ActorId, (incumbent ?? Candidate()).TargetId, true), rng ?? Decision);
    private static SwordAiComparisonOutcome Success(SwordAiComparisonResult result)
    {
        Assert.NotEqual(SwordAiComparisonDisposition.Rejected, result.Disposition); Assert.Equal(SwordAiComparisonRejection.None, result.Rejection);
        Assert.Null(result.FailedCandidateIndex); Assert.Equal(SwordAiScoringRejection.None, result.ScoringRejection); Assert.Equal(SwordAiCandidateRejection.None, result.ProjectionRejection);
        var outcome = Assert.IsType<SwordAiComparisonOutcome>(result.Outcome); Assert.Equal(2, outcome.Candidates.Length);
        Assert.All(outcome.Candidates, c => Assert.Equal(0, c.DecidedMarker)); Assert.Equal(outcome.Candidates[1].Evaluation.RngState, result.RngState);
        return outcome;
    }
    private static void Failed(SwordAiComparisonResult result, SwordAiComparisonRejection reason, FftRandomState? rng = null)
    { Assert.Equal(SwordAiComparisonDisposition.Rejected, result.Disposition); Assert.Equal(reason, result.Rejection); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState); }
    private static string Serialize(object? value) => JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot s) => Serialize(new { s.Combatants, s.TurnBattle.Turns, s.TurnBattle.Battle.Map, s.TurnBattle.Battle.RngState,
        Units = s.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved, u.MovementProfile.Move, u.MovementProfile.Jump,
            Costs = u.MovementProfile.TerrainCosts.Select(c => new { Id = c.Key.Value, c.Value }) }) });
    private static AttackBattleSnapshot State(bool second = false, bool reverse = false, int actorHp = 50, int actorMax = 50, int hp = 50, int maxHp = 50,
        int evade = 0, int ct = 15, int targetCt = 95, int speed = 5, int targetSpeed = 5, int slot = 0, ulong ticks = 20,
        TilePosition? origin = null, TilePosition? enemy = null, bool moved = false, bool acted = false,
        int width = 3, int depth = 2, int move = 4, int height = 10, int cost = 1)
    {
        var actor = second ? B : A; var target = second ? A : B; var rng = new FftRandomState(57, 100);
        var movement = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(cost))]);
        var bodies = new[] { new BattleUnitState(actor, origin ?? new(0, 0, 0), movement, moved), new BattleUnitState(target, enemy ?? new(1, 0, 0), movement, true) };
        var turns = new[] { new TurnUnitState(actor, speed, ct, slot, true, moved, acted), new TurnUnitState(target, targetSpeed, targetCt, 1 - slot, false, true, true) };
        AttackUnitState Combat(BattleUnitId id, int current, int maximum, int evasion) => new(id, current, maximum, 0, 0, 5, evasion, 0, 0, 0, 70, 0,
            id == actor ? BattleFacing.West : BattleFacing.East, current == 0 ? AttackUnitStatus.Dead : current <= maximum / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 255, Weapon);
        var combat = new[] { Combat(actor, actorHp, actorMax, 0), Combat(target, hp, maxHp, evade) };
        return new(new(new(new(width, depth, 1, Enumerable.Repeat(new BattleTile(Ground, height), width * depth).ToArray()), reverse ? bodies.Reverse() : bodies, rng),
            new(reverse ? turns.Reverse() : turns, rng, ticks, TurnCyclePhase.UnitTurn)), reverse ? combat.Reverse() : combat);
    }
}
