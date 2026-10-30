using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiMovementScenarioTests
{
    private static readonly AttackThrowStoneAiScenarioProfile ScenarioProfile = new(true, true, true);

    [Fact]
    public void Fresh_pair_derives_literal_ranged_D0_D1_and_retains_one_refresh_plus_each_scored_tile_draw()
    {
        foreach (var (target, move, d0, d1, draws, word) in new[] {
            (new TilePosition(2,1,0), 3, new TilePosition(1,3,0), new TilePosition(1,4,0), 15ul, 2111915288u),
            (new TilePosition(4,1,0), 3, new TilePosition(2,2,0), new TilePosition(1,4,0), 17ul, 3791773014u),
            (new TilePosition(2,1,0), 0, Origin, Origin, 2ul, 2524885223u),
            (new TilePosition(6,1,0), 4, new TilePosition(3,2,0), new TilePosition(1,5,0), 25ul, 744964398u) })
        {
            var result = Derive(Battle(target: target, move: move)); var outcome = Success(result);
            Assert.Equal(d0, outcome.Scenario.BaselineDestination); Assert.Equal(d1, outcome.Scenario.ActFirstDestination);
            Assert.Equal(move + 1, outcome.TargetReach); Assert.Equal(Math.Max(3, outcome.TargetDistance - 1), outcome.BaselineDistanceLimit);
            Assert.Equal(d0, outcome.Movement[outcome.BaselineIndex].Outcome.Destination); Assert.False(outcome.BaselineUsedDistanceFallback);
            Assert.Equal(new FftRandomState(word, 7 + draws), result.RngState);
            Assert.Equal((int)draws - 1, outcome.Movement.Length); // Source costs in these literal geometries, not an authoring-size limit.
            Assert.Equal(outcome.Scenario.ScoredOrigins, outcome.Scenario.ScenarioOrigins);
            Assert.All(outcome.Movement, m => { Assert.Equal(0, m.Outcome.Score.Priority); Assert.False(m.Outcome.Units.Single(u => u.UnitId == A).ActionTaken); });
        }
    }

    [Fact]
    public void Equal_priority_and_proximity_use_distance_then_later_coordinates_from_original_turn_delays()
    {
        var result = Derive(Battle(targetCt: 0)); var outcome = Success(result);
        Assert.All(outcome.Movement, m => Assert.Equal(0, m.OriginKey));
        Assert.Equal(new TilePosition(2, 2, 0), outcome.Scenario.BaselineDestination);
        Assert.Equal(new TilePosition(2, 2, 0), outcome.Scenario.ActFirstDestination);
        Assert.Equal((17, 20), (outcome.Preparation.Common.ActorDelay, outcome.Preparation.Common.TargetDelay));
        Assert.Equal(new FftRandomState(2111915288, 22), result.RngState);
    }

    [Fact]
    public void Plain_sword_keeps_its_original_closer_baseline_and_ranged_pair_does_not_borrow_that_policy()
    {
        var state = Battle(); var p = Profile(); var pair = Success(Derive(state));
        var sword = SwordAiMovementScenario.Evaluate(state, p.Attack, p.Score, new(A, B, true), new(true, true, true),
            new FftBiosRandom().Next(Decision).State);
        Assert.True(sword.Accepted); Assert.True(sword.Outcome!.BaselineUsedDistanceFallback);
        Assert.Equal(new TilePosition(2, 2, 0), sword.Outcome.Scenario.BaselineDestination);
        Assert.Equal(new TilePosition(1, 3, 0), pair.Scenario.BaselineDestination);
        Assert.Equal(sword.Outcome.Scenario.AttackDestination, pair.Scenario.ActFirstDestination);
    }

    [Fact]
    public void Exact_and_late_draw_or_tick_failure_publish_no_partial_preparation_or_mask()
    {
        var rng = new FftRandomState(1, ulong.MaxValue - 15); var fits = Derive(Battle(), rng: rng); Success(fits);
        Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var shortBudget = rng with { DrawCount = rng.DrawCount + 1 };
        var failure = Atomic(Derive(Battle(), rng: shortBudget), AttackThrowStoneAiScenarioStage.Movement, shortBudget);
        Assert.Equal(13, failure.MovementIndex); Assert.Equal(SwordAiMovementBaselineRejection.RandomDrawCountExhausted, failure.MovementReason);
        var ticks = Atomic(Derive(Battle(targetCt: 0, ticks: ulong.MaxValue - 9)), AttackThrowStoneAiScenarioStage.Movement);
        Assert.Equal(SwordAiMovementBaselineRejection.TickCountExhausted, ticks.MovementReason);
        // Every tile starts from original ticks, rather than accumulating the preceding private clock's ticks.
        var lastTick = Success(Derive(Battle(ticks: ulong.MaxValue - 1)));
        Assert.All(lastTick.Movement, m => Assert.Equal(ulong.MaxValue, m.Outcome.Ticks));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Remaining_scenario_facts_and_explicit_in_reach_branch_are_required()
    {
        foreach (var profile in new[] { ScenarioProfile with { NormalScoring = false }, ScenarioProfile with { CanonicalCoordinates = false },
            ScenarioProfile with { NoOtherScenarioOverrides = false } })
            Assert.Equal(AttackThrowStoneAiScenarioRejection.UnsupportedProfile, Atomic(Derive(Battle(), profile: profile), AttackThrowStoneAiScenarioStage.Admission).Reason);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.OutOfReach,
            Atomic(Derive(Battle(target: new(6, 1, 0))), AttackThrowStoneAiScenarioStage.Branch).Reason);
        var team = Atomic(Derive(Battle(), team: SwordAiScenarioTeam.Blue), AttackThrowStoneAiScenarioStage.Preparation);
        Assert.Equal(AttackThrowStoneAiPreparationRejection.UnsupportedTeam, team.PreparationFailure!.Reason);
        var moved = Atomic(Derive(Battle(moved: true)), AttackThrowStoneAiScenarioStage.Preparation);
        Assert.Equal(SwordAiRefreshRejection.UnavailableAction, moved.PreparationFailure!.RefreshRejection);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_original_collection_policy_orders_replay_without_live_changes()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(second: second); var before = Snapshot(state); var request = FreshRequest(second);
            var result = Derive(state, request: request); Success(result);
            Assert.Equal(Serialize(result), Serialize(Derive(state, request: request)));
            Assert.Equal(Serialize(result), Serialize(Derive(Battle(second: second, reverse: true),
                request: request with { Policies = request.Policies.Reverse().ToImmutableArray() })));
            Assert.Equal(before, Snapshot(state));
        }
    }

    internal static AttackThrowStoneAiMovementScenarioResult Derive(AttackBattleSnapshot state, SwordAiRefreshRequest? request = null,
        AttackThrowStoneAiScenarioProfile? profile = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null)
    {
        var p = Profile(); return AttackThrowStoneAiMovementScenario.Evaluate(state, request ?? FreshRequest(), Stone, new(Sword, 2),
            p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), profile ?? ScenarioProfile, team, rng ?? Decision);
    }
    internal static AttackThrowStoneAiMovementScenarioOutcome Success(AttackThrowStoneAiMovementScenarioResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiMovementScenarioOutcome>(result.Outcome); }
    private static AttackThrowStoneAiScenarioFailure Atomic(AttackThrowStoneAiMovementScenarioResult result,
        AttackThrowStoneAiScenarioStage stage, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<AttackThrowStoneAiScenarioFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure; }
}
