using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AccumulateAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AccumulateAiMapEntryTests
{
    private static readonly RandomState EntryRng = new(5, 7);

    [Fact]
    public void Map_entry_filters_once_then_evaluates_both_origins_from_original_PA_and_retains_ACT_first_tie()
    {
        var state = Battle(pa: 98); var before = Snapshot(state); var result = Evaluate(state); var o = Success(result);
        Assert.Equal(new RandomState(2815492134, 12), result.RngState);
        Assert.Equal(18655u, Assert.Single(o.Preparation.Common.Refresh.UpkeepPicks).RawRoll);
        Assert.Equal(8457u, Assert.Single(o.Baseline.UpkeepPicks).RawRoll);
        Assert.Equal(10616u, o.Usefulness.Targets.Single(t => t.UnitId == A).Raw);
        Assert.Equal(new[] { 31877u, 10193u }, o.Candidates.Select(c => Assert.Single(c.Projection!.Outcome!.UpkeepPicks).RawRoll));
        Assert.Equal(new[] { true, false }, o.Candidates.Select(c => c.Projection!.Outcome!.Candidate.ActBeforeMove));
        Assert.All(o.Candidates, c => {
            Assert.Equal((98, 99), (c.Projection!.Outcome!.PhysicalAttackBefore, c.Projection.Outcome.PhysicalAttackAfter));
            Assert.Equal((3, 100), ((int)c.Score!.Priority, (int)c.Score.HitPercent));
        });
        Assert.Equal(0, o.BestCandidateIndex); Assert.Equal(AiRankOrder.Above, o.BaselineComparison); Assert.True(o.BeatsMovementBaseline);
        Assert.Equal(0, o.Baseline.Score.Priority); Assert.Equal(before, Snapshot(state));
    }

    [Fact]
    public void Same_origin_skips_Move_first_and_PA99_still_beats_a_negative_absolute_baseline()
    {
        var state = Battle(pa: 99, actorHp: 10); var result = Evaluate(state, destination: Origin); var o = Success(result);
        Assert.Equal(new RandomState(2089129857, 11), result.RngState);
        var candidate = Assert.Single(o.Candidates);
        Assert.Equal((99, 99), (candidate.Projection!.Outcome!.PhysicalAttackBefore, candidate.Projection.Outcome.PhysicalAttackAfter));
        Assert.Equal((-135, -132), ((int)o.Baseline.Score.Priority, (int)candidate.Score!.Priority)); Assert.True(o.BeatsMovementBaseline);
    }

    [Fact]
    public void Odd_entry_coin_retains_only_prefix_draws_and_never_runs_candidate_clocks()
    {
        var result = Evaluate(Battle(), rng: Decision); var o = Success(result);
        Assert.Equal(new RandomState(662824084, 10), result.RngState);
        Assert.False(o.Usefulness.SelfUseful); Assert.Empty(o.Candidates); Assert.Null(o.BestCandidateIndex);
        Assert.Null(o.BaselineComparison); Assert.False(o.BeatsMovementBaseline);
    }

    [Fact]
    public void Failure_at_each_distinct_draw_stage_restores_the_whole_query_and_exact_budget_fits()
    {
        var stages = new[] { AccumulateAiMapEntryStage.Preparation, AccumulateAiMapEntryStage.Movement,
            AccumulateAiMapEntryStage.Usefulness, AccumulateAiMapEntryStage.Candidate, AccumulateAiMapEntryStage.Candidate };
        for (int remaining = 0; remaining < 5; remaining++)
        {
            var rng = new RandomState(5, ulong.MaxValue - (ulong)remaining);
            var result = Evaluate(Battle(), rng: rng);
            Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState);
            Assert.Equal(stages[remaining], result.Failure!.Stage);
            if (remaining >= 3) Assert.Equal(remaining - 3, result.Failure.CandidateIndex);
        }
        var fits = Evaluate(Battle(), rng: new(5, ulong.MaxValue - 5)); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var tickFailure = Evaluate(Battle(ticks: ulong.MaxValue)); Assert.False(tickFailure.Accepted); Assert.Equal(EntryRng, tickFailure.RngState);
        Assert.Equal(AccumulateAiMapEntryStage.Preparation, tickFailure.Failure!.Stage);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Uncertified_or_unreachable_scenarios_return_no_partial_preparation()
    {
        var state = Battle(); var before = Snapshot(state);
        var request = new AccumulateAiMapEntryRequest(Request(), Origin, Destination);
        var rejected = AccumulateAiMapEntry.Evaluate(state, request, Configuration(), EntryRng);
        Assert.False(rejected.Accepted); Assert.Equal(EntryRng, rejected.RngState); Assert.Null(rejected.Outcome);
        rejected = Evaluate(state, destination: new(7, 7, 0));
        Assert.False(rejected.Accepted); Assert.Equal(EntryRng, rejected.RngState); Assert.Null(rejected.Outcome);
        Assert.Equal(before, Snapshot(state));
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_teams_identities_and_collection_orders_replay_without_changing_live_state()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second); var before = Snapshot(state);
            var first = Evaluate(state, second: second, team: team); Success(first);
            Assert.Equal(Serialize(first), Serialize(Evaluate(state, second: second, team: team)));
            Assert.Equal(Serialize(first), Serialize(Evaluate(Battle(second: second, reverse: true), second: second, team: team)));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static AccumulateAiMapEntryResult Evaluate(AttackBattleSnapshot state, TilePosition? destination = null,
        RandomState? rng = null, bool second = false, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue) =>
        AccumulateAiMapEntry.Evaluate(state, new(Request(second), Origin, destination ?? Destination, true), Configuration(team), rng ?? EntryRng);
    private static AccumulateAiMapEntryOutcome Success(AccumulateAiMapEntryResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); return Assert.IsType<AccumulateAiMapEntryOutcome>(result.Outcome); }
}
