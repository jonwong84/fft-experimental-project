using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiCompositionTests
{
    [Fact]
    public void Source_tape_retains_one_prefix_Attack_self_coin_two_clocks_Stone_and_ordered_ranks()
    {
        var state = Battle(); var before = Snapshot(state); var result = Compose(state); var o = Success(result);
        Assert.Equal(new RandomState(3297241969, 23), result.RngState); // Sixteen draws from word1/count7.
        Assert.Equal(16838u, Assert.Single(o.Preparation.Common.Refresh.UpkeepPicks).RawRoll);
        Assert.Equal(new[] { Far, Origin }, o.Movement.Select(m => m.Outcome.Destination));
        Assert.Equal(new[] { 5758u, 10113u }, o.Movement.Select(m => Assert.Single(m.Outcome.UpkeepPicks).RawRoll));
        var attack = Assert.Single(o.Attack.Candidates);
        Assert.Equal((10, 3), (attack.Evaluation.Projection!.Outcome!.NormalDamage, attack.Evaluation.Projection.Outcome.HitPercent));
        Assert.Equal(7419u, Assert.Single(attack.Evaluation.Projection.Outcome.UpkeepPicks).RawRoll);
        Assert.Equal(16212u, o.Accumulate.Usefulness.Targets.Single(t => t.UnitId == A).Raw);
        Assert.Equal(new[] { 4086u, 2749u }, o.Accumulate.Candidates.Select(c => Assert.Single(c.Projection!.Outcome!.UpkeepPicks).RawRoll));
        Assert.All(o.Accumulate.Candidates, c => Assert.Equal((5, 6), (c.Projection!.Outcome!.PhysicalAttackBefore, c.Projection.Outcome.PhysicalAttackAfter)));
        var stone = o.ThrowStone.Candidate!;
        Assert.Equal((10, 3), (stone.Evaluation.Projection!.Outcome!.NormalDamage, stone.Evaluation.Projection.Outcome.HitPercent));
        Assert.Equal(17543u, Assert.Single(stone.Evaluation.Projection.Outcome.UpkeepPicks).RawRoll);
        Assert.Equal(new[] { BasicSkillAiActionKind.Attack, BasicSkillAiActionKind.ThrowStone, BasicSkillAiActionKind.Accumulate },
            o.RankedActions.Where(a => a is not null).Select(a => a!.Kind));
        Assert.Equal(new short[] { 26, 26, 3 }, o.RankedActions.Take(3).Select(a => a!.Score.Priority));
        Assert.Equal((0, 1, 1), (o.AttackInsertionIndex, o.AccumulateInsertionIndex, o.ThrowStoneInsertionIndex));
        Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Empty_damage_mask_does_not_disable_self_entry_and_odd_coin_does_not_skip_Stone()
    {
        var odd = Success(Compose(Battle(), scenario: Scenario(emptyDamageMask: true)));
        Assert.Empty(odd.Attack.Candidates); Assert.Empty(odd.Accumulate.Candidates); Assert.False(odd.Accumulate.Usefulness.SelfUseful);
        Assert.Null(odd.ThrowStone.Candidate); Assert.All(odd.RankedActions, Assert.Null);
        var useful = Compose(Battle(), scenario: Scenario(emptyDamageMask: true), rng: new(2, 7)); var o = Success(useful);
        Assert.Equal(12914u, o.Accumulate.Usefulness.Targets.Single(t => t.UnitId == A).Raw);
        Assert.Equal(2, o.Accumulate.Candidates.Length); Assert.IsType<BasicSkillAiAccumulate>(o.RankedActions[0]);
        Assert.Equal(13ul, useful.RngState.DrawCount);
        // No Attack is in range, but an odd draw4 still leaves the later Throw Stone entry active.
        var nonAdjacent = Compose(Battle(adjacent: false), scenario: Scenario()); Success(nonAdjacent);
        Assert.False(nonAdjacent.Outcome!.Accumulate.Usefulness.SelfUseful); Assert.Null(nonAdjacent.Outcome.ThrowStone.Candidate);
        var later = Success(Compose(Battle(target: new(4, 1, 0))));
        Assert.False(later.Accumulate.Usefulness.SelfUseful); Assert.NotNull(later.ThrowStone.Candidate);
    }

    [Fact]
    public void Same_D1_runs_one_self_clock_and_losing_damage_or_PA_projection_never_changes_later_inputs()
    {
        var result = Compose(Battle(), scenario: Scenario(sameD1: true)); var o = Success(result);
        Assert.Single(o.Accumulate.Candidates); Assert.Equal(new RandomState(2111915288, 22), result.RngState);
        Assert.Equal(50, o.Accumulate.Candidates[0].Projection!.Outcome!.Units.Single(u => u.UnitId == B).Hp);
        Assert.Equal(10, o.ThrowStone.Candidate!.Evaluation.Projection!.Outcome!.NormalDamage);
    }

    [Fact]
    public void Distinct_failures_restore_whole_query_and_exact_or_spare_budget_keeps_same_tape()
    {
        (int Draws, BasicSkillAiStage Stage)[] boundaries = [(0, BasicSkillAiStage.Preparation), (1, BasicSkillAiStage.Movement),
            (3, BasicSkillAiStage.Attack), (8, BasicSkillAiStage.Accumulate), (9, BasicSkillAiStage.Accumulate),
            (10, BasicSkillAiStage.Accumulate), (11, BasicSkillAiStage.ThrowStone), (15, BasicSkillAiStage.ThrowStone)];
        foreach (var boundary in boundaries)
        {
            var rng = new RandomState(1, ulong.MaxValue - (ulong)boundary.Draws); var state = Battle(); var before = Snapshot(state);
            var result = Compose(state, rng: rng); Assert.False(result.Accepted); Assert.Null(result.Outcome);
            Assert.Equal(boundary.Stage, result.Failure!.Stage); Assert.Equal(rng, result.RngState); Assert.Equal(before, Snapshot(state));
        }
        var fits = Compose(Battle(), rng: new(1, ulong.MaxValue - 16)); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var spare = Compose(Battle(), rng: new(1, ulong.MaxValue - 17)); Success(spare); Assert.Equal(ulong.MaxValue - 1, spare.RngState.DrawCount);
        Assert.Equal(fits.RngState.Value, spare.RngState.Value);
        Assert.Equal(fits.Outcome!.RankedActions.Select(a => a?.Kind), spare.Outcome!.RankedActions.Select(a => a?.Kind));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void General_entry_scan_and_known_score_cell_provenance_cannot_be_manufactured()
    {
        var p = Profile(); var state = Battle();
        foreach (var bad in new[] { p with { SourceGeneralEntry = false }, p with { SourceAttackAccumulateThrowStoneOrder = false },
            p with { FreshEmptyRankedList = false }, p with { AttackScan = p.AttackScan with { TargetActive = false } },
            p with { ThrowStoneScan = p.ThrowStoneScan with { OriginalMovementScoreMask = false } } })
        { var result = Compose(state, bad); Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(Decision, result.RngState); }
        var unknownD1 = new ThrowStoneAiScanScenario([Origin], [], Origin, Far);
        var failure = Compose(state, scenario: unknownD1); Assert.False(failure.Accepted); Assert.Equal(Decision, failure.RngState);
        Assert.Equal(ThrowStoneAiScanRejection.InvalidScenario, failure.Failure!.ThrowStone!.Reason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_and_identities_replay_independently_of_collection_order()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second); var before = Snapshot(state); var p = Profile(team);
            var result = Compose(state, p, second: second); Success(result);
            Assert.Equal(Serialize(result), Serialize(Compose(state, p, second: second)));
            Assert.Equal(Serialize(result), Serialize(Compose(Battle(second: second, reverse: true), p, second: second)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    internal static BasicSkillAiCompositionOutcome Success(BasicSkillAiCompositionResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); return Assert.IsType<BasicSkillAiCompositionOutcome>(result.Outcome); }
}
