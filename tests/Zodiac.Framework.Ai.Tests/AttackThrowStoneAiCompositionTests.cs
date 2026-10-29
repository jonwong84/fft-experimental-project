using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiCompositionTests
{
    [Fact]
    public void Two_fresh_entries_retain_stronger_priority_and_final_destination_proximity_with_typed_payloads()
    {
        foreach (var (power, attackScore, stoneFirst) in new[] { (1, 13, true), (2, 26, true), (4, 52, false) })
        {
            var outcome = Success(Evaluate(power: power));
            var attack = Assert.IsType<AttackThrowStoneAiAttack>(outcome.RankedActions[stoneFirst ? 1 : 0]);
            var stone = Assert.IsType<AttackThrowStoneAiStone>(outcome.RankedActions[stoneFirst ? 0 : 1]);
            Assert.Equal(attackScore, attack.Score.Priority); Assert.Equal(26, stone.Score.Priority);
            Assert.Equal(9, attack.Proximity); Assert.Equal(8, stone.Proximity);
            Assert.Equal(Origin, attack.Destination); Assert.Equal(Far, stone.Destination);
            Assert.All(outcome.RankedActions.Skip(2), Assert.Null);
        }
        var tie = Success(Evaluate(scenario: new([Origin], [Origin], Origin, Origin)));
        Assert.IsType<AttackThrowStoneAiAttack>(tie.RankedActions[0]); Assert.IsType<AttackThrowStoneAiStone>(tie.RankedActions[1]);
        Assert.Equal((0, 1), (tie.AttackInsertionIndex, tie.ThrowStoneInsertionIndex));
    }

    [Fact]
    public void One_prefix_and_original_units_carry_literal_RNG_through_Attack_then_Throw_Stone()
    {
        var state = Battle(); var before = Snapshot(state); var result = Evaluate(state); var outcome = Success(result);
        Assert.Equal(new[] { Far, Origin }, outcome.Movement.Select(m => m.Outcome.Destination));
        Assert.Equal(new ulong[] { 8, 9 }, outcome.Movement.Select(m => m.RngState.DrawCount));
        Assert.Equal(new FftRandomState(2524885223, 9), outcome.Movement[^1].RngState);
        var attack = Assert.Single(outcome.Attack.Candidates);
        Assert.Equal(new FftRandomState(3655513600, 14), attack.Evaluation.RngState);
        Assert.Equal(new FftRandomState(836760821, 19), result.RngState); // 2 + 5 + 5; no second prefix or D0 rescore.
        Assert.Equal(40, attack.Evaluation.Projection!.Outcome!.Units.Single(u => u.UnitId == B).Hp);
        Assert.Equal(40, outcome.ThrowStone.Candidate!.Evaluation.Projection!.Outcome!.Units.Single(u => u.UnitId == B).Hp);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void NotValuable_keeps_draws_without_reinserting_the_previous_best_and_inactive_entries_keep_only_prefix()
    {
        var south = new TilePosition(2, 2, 0);
        var scenario = new ThrowStoneAiScanScenario([Origin, Far, south], [Far, south], Origin, south);
        var result = Evaluate(Battle(evade: 100), scenario: scenario); var outcome = Success(result);
        Assert.IsType<AttackThrowStoneAiAttack>(outcome.RankedActions[0]); Assert.All(outcome.RankedActions.Skip(1), Assert.Null);
        Assert.Equal(AiScoringDisposition.NotValuable, outcome.ThrowStone.Candidate!.Evaluation.Disposition);
        Assert.Null(outcome.ThrowStoneInsertionIndex); Assert.Equal(new FftRandomState(836760821, 19), result.RngState);
        result = Evaluate(Battle(evade: 100)); outcome = Success(result);
        Assert.All(outcome.RankedActions, Assert.Null); Assert.Equal(new FftRandomState(267834847, 17), result.RngState);
        result = Evaluate(profile: Profile(active: false)); outcome = Success(result);
        Assert.All(outcome.RankedActions, Assert.Null); Assert.Empty(outcome.Attack.Candidates); Assert.Null(outcome.ThrowStone.Candidate);
        Assert.Equal(new FftRandomState(2524885223, 9), result.RngState);
    }

    [Fact]
    public void Exact_normal_and_KO_budgets_fit_and_late_failures_return_no_partial_ranks_or_RNG()
    {
        foreach (var (hp, draws) in new[] { (50, 12ul), (10, 14ul) })
        {
            var rng = new FftRandomState(1, ulong.MaxValue - draws); var result = Evaluate(Battle(hp: hp), rng: rng);
            var outcome = Success(result); Assert.Equal(ulong.MaxValue, result.RngState.DrawCount);
            Assert.All(outcome.RankedActions.Take(2), action => Assert.NotNull(action));
            var shortBudget = rng with { DrawCount = rng.DrawCount + 1 };
            var failure = Atomic(Evaluate(Battle(hp: hp), rng: shortBudget), AttackThrowStoneAiStage.ThrowStone, shortBudget);
            Assert.Equal(ThrowStoneAiCandidateRejection.RandomDrawCountExhausted, failure.ThrowStoneFailure!.Detail!.CandidateRejection);
        }
        var prefixBudget = new FftRandomState(1, ulong.MaxValue - 1);
        Assert.Equal(1, Atomic(Evaluate(rng: prefixBudget), AttackThrowStoneAiStage.Movement, prefixBudget).Index);
        var attackBudget = new FftRandomState(1, ulong.MaxValue - 6);
        Assert.Equal(SwordAiCandidateRejection.RandomDrawCountExhausted,
            Atomic(Evaluate(rng: attackBudget), AttackThrowStoneAiStage.Attack, attackBudget).ProjectionReason);
        Assert.Equal(SwordAiMovementBaselineRejection.TickCountExhausted,
            Atomic(Evaluate(Battle(ticks: ulong.MaxValue)), AttackThrowStoneAiStage.Movement).MovementReason);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Family_profile_and_source_order_disagreements_reject_before_prefix_work()
    {
        var p = Profile();
        foreach (var profile in new[] { p with { SourceAttackThenThrowStone = false }, p with { FreshEmptyRankedList = false } })
            Assert.Equal(AttackThrowStoneAiRejection.UnsupportedProfile, Atomic(Evaluate(profile: profile), AttackThrowStoneAiStage.Admission).Reason);
        var differentCounters = new ThrowStoneAiCandidateProfile(p.ThrowStone.Ability, p.ThrowStone.Movement, [new(A, 255), new(B, 20)]);
        foreach (var profile in new[] { p with { ThrowStone = differentCounters }, p with { AttackScan = p.AttackScan with { TargetActive = false } } })
            Assert.Equal(AttackThrowStoneAiRejection.MismatchedProfiles, Atomic(Evaluate(profile: profile), AttackThrowStoneAiStage.Admission).Reason);
        var noGrant = p with { ThrowStone = CandidateProfile(granted: false) };
        Assert.Equal(AttackCommandRejection.AbilityNotGranted,
            Atomic(Evaluate(profile: noGrant), AttackThrowStoneAiStage.Admission).ThrowStoneFailure!.Detail!.AbilityRejection);
        var naked = State(target: new(2, 1, 0));
        Assert.Equal(SwordAiCandidateRejection.UnsupportedProfile, Atomic(Evaluate(naked), AttackThrowStoneAiStage.Admission).ProjectionReason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actors_replay_with_reversed_masks_and_state_order_without_input_mutation()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second); var before = Snapshot(state); var result = Evaluate(state, second: second); Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, second: second)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(Battle(second: second, reverse: true),
                scenario: new([Far, Origin], [Far, Origin], Origin, Origin), second: second)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    internal static AttackThrowStoneAiCompositionOutcome Success(AttackThrowStoneAiCompositionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiCompositionOutcome>(result.Outcome); }
    private static AttackThrowStoneAiFailure Atomic(AttackThrowStoneAiCompositionResult result, AttackThrowStoneAiStage stage, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<AttackThrowStoneAiFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure; }
}
