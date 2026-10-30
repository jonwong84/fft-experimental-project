using System.Collections.Immutable;
using Xunit;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiOutOfReachScenarioTests
{
    private static readonly AttackThrowStoneAiScenarioProfile ScenarioProfile = new(true, true, true);
    private static readonly TilePosition Right = new(2, 1, 0);

    [Fact]
    public void Distances_three_four_and_five_preserve_unwritten_actor_priority_fresh_full_proximity_and_overlapping_masks()
    {
        foreach (int distance in new[] { 3, 4, 5 })
        {
            var result = Derive(Battle(move: 1, target: new(distance + 1, 1, 0))); var value = Success(result);
            Assert.Equal(new[] { Right }, value.ScoreMask);
            Assert.Equal(new[] { Origin, Right }, value.Scenario1);
            Assert.Equal(new[] { new TilePosition(1, 0, 0), Far, Origin, new TilePosition(1, 2, 0) }, value.Scenario2);
            Assert.Equal(new[] { Origin }, value.Scenario1.Intersect(value.Scenario2));
            Assert.Equal(5, value.Cells.Length); // All five original reachable cells get proximity, only Right gets a priority write.
            Assert.All(value.Cells, cell => {
                Assert.Equal(10 - Math.Abs(distance + 1 - cell.Tile.X) - Math.Abs(1 - cell.Tile.Y), cell.Proximity);
                Assert.Equal(cell.Tile == Right, cell.PriorityWritten);
                Assert.Equal(cell.Tile == Right ? (short?)0 : null, cell.WrittenPriority);
            });
            var moved = Assert.Single(value.Movement);
            Assert.Equal(Right, moved.Outcome.Destination); Assert.Equal(new FftRandomState(2524885223, 9), moved.RngState);
            Assert.Equal((Right, Right, 0, distance, 2, 2), (value.BaselineDestination, value.ActFirstDestination,
                value.BaselineIndex, value.TargetDistance, value.TargetReach, value.EffectiveReach));
            Assert.Equal(distance > 3, value.DestinationUsedDistanceFallback); Assert.False(value.FilterUsedReachableFallback);
            Assert.Equal(new SwordAiReachAdjustment(10113, 5, 2, 2), value.ReachAdjustment);
            Assert.Equal(new FftRandomState(662824084, 10), result.RngState); // One refresh, one scored tile, then adjustment; no actor rescore.
        }
    }

    [Fact]
    public void Multiple_filtered_tiles_use_original_delay_priority_then_distance_and_later_coordinate_ties()
    {
        TilePosition[] scored = [new(3, 0, 0), Right, new(3, 1, 0), new(4, 1, 0), new(3, 2, 0)];
        var result = Derive(Battle(target: new(6, 1, 0))); var value = Success(result);
        Assert.Equal(scored, value.ScoreMask); Assert.Equal(scored, value.Movement.Select(m => m.Outcome.Destination));
        Assert.All(value.Movement, m => Assert.Equal(0, m.TilePriority));
        Assert.Equal(new TilePosition(3, 2, 0), value.BaselineDestination); Assert.Equal(value.BaselineDestination, value.ActFirstDestination);
        Assert.Equal(new SwordAiReachAdjustment(23010, 1, 4, 4), value.ReachAdjustment);
        Assert.Equal(new FftRandomState(3655513600, 14), result.RngState);
        Assert.Equal(new FftRandomState(2516284547, 13), value.Movement[^1].RngState);
        var notImminent = Success(Derive(Battle(target: new(6, 1, 0), targetCt: 0)));
        Assert.All(notImminent.Cells, cell => Assert.Equal(0, cell.Proximity));
        Assert.Equal(new TilePosition(4, 1, 0), notImminent.BaselineDestination);
        Assert.Equal((17, 20), (notImminent.Preparation.Common.ActorDelay, notImminent.Preparation.Common.TargetDelay));
    }

    [Fact]
    public void Zero_Move_no_match_fallback_scores_actor_fresh_and_halving_occurs_after_movement()
    {
        var zero = Derive(Battle(move: 0, target: new(4, 1, 0))); var value = Success(zero);
        Assert.True(value.FilterUsedReachableFallback); Assert.True(value.DestinationUsedDistanceFallback);
        Assert.Equal(new[] { Origin }, value.ScoreMask); Assert.Equal(new[] { Origin }, value.Scenario1); Assert.Empty(value.Scenario2);
        Assert.Equal(new AiMovementCellProvenance(Origin, 7, 0), Assert.Single(value.Cells));
        Assert.Equal(Origin, value.BaselineDestination); Assert.Equal(Origin, value.ActFirstDestination);
        Assert.Equal(55, Assert.Single(value.Movement).Outcome.TurnEndCt);
        Assert.Equal(new FftRandomState(662824084, 10), zero.RngState);
        var halved = Derive(Battle(move: 1, target: new(4, 1, 0)), rng: new(20, 7)); var half = Success(halved);
        Assert.Equal(new SwordAiReachAdjustment(12502, 0, 2, 1), half.ReachAdjustment);
        Assert.True(half.DestinationUsedDistanceFallback); Assert.Equal(Right, half.BaselineDestination);
        Assert.Equal(new FftRandomState(143664818, 9), Assert.Single(half.Movement).RngState);
        Assert.Equal(new FftRandomState(2966873603, 10), halved.RngState);
        var halfZero = Success(Derive(Battle(move: 0, target: new(4, 1, 0)), rng: new(20, 7)));
        Assert.Equal(0, halfZero.EffectiveReach); Assert.Equal(Origin, halfZero.BaselineDestination);
    }

    [Fact]
    public void Exact_draw_budget_and_failures_in_refresh_movement_adjustment_or_late_ticks_are_atomic()
    {
        var state = Battle(move: 1, target: new(4, 1, 0)); var before = Snapshot(state);
        var fits = Derive(state, rng: new(1, ulong.MaxValue - 3)); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var adjustmentRng = new FftRandomState(1, ulong.MaxValue - 2);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.RandomDrawCountExhausted,
            Atomic(Derive(state, rng: adjustmentRng), AttackThrowStoneAiScenarioStage.ReachAdjustment, adjustmentRng).Reason);
        var movementRng = new FftRandomState(1, ulong.MaxValue - 1);
        var movement = Atomic(Derive(state, rng: movementRng), AttackThrowStoneAiScenarioStage.Movement, movementRng);
        Assert.Equal((0, SwordAiMovementBaselineRejection.RandomDrawCountExhausted), (movement.MovementIndex, movement.MovementReason));
        var exhausted = new FftRandomState(1, ulong.MaxValue);
        Atomic(Derive(state, rng: exhausted), AttackThrowStoneAiScenarioStage.Preparation, exhausted);
        var tick = Atomic(Derive(Battle(move: 1, target: new(4, 1, 0), targetCt: 0, ticks: ulong.MaxValue - 9)),
            AttackThrowStoneAiScenarioStage.Movement);
        Assert.Equal(SwordAiMovementBaselineRejection.TickCountExhausted, tick.MovementReason);
        var lastTick = Success(Derive(Battle(move: 1, target: new(4, 1, 0), ticks: ulong.MaxValue - 1)));
        Assert.All(lastTick.Movement, m => Assert.Equal(ulong.MaxValue, m.Outcome.Ticks));
        Success(Derive(Battle(move: 1, target: new(4, 1, 0), targetCt: 100, ticks: ulong.MaxValue)));
        Assert.Equal(before, Snapshot(state));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Branch_policy_and_existing_fresh_cell_scan_requirements_are_not_broadened()
    {
        var state = Battle(move: 1, target: new(4, 1, 0));
        foreach (var profile in new[] { ScenarioProfile with { NormalScoring = false }, ScenarioProfile with { CanonicalCoordinates = false },
            ScenarioProfile with { NoOtherScenarioOverrides = false } })
            Atomic(Derive(state, profile: profile), AttackThrowStoneAiScenarioStage.Admission);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.InReach, Atomic(Derive(Battle()), AttackThrowStoneAiScenarioStage.Branch).Reason);
        foreach (var team in new[] { SwordAiScenarioTeam.Blue, SwordAiScenarioTeam.Unknown })
            Assert.Equal(AttackThrowStoneAiPreparationRejection.UnsupportedTeam,
                Atomic(Derive(state, team: team), AttackThrowStoneAiScenarioStage.Preparation).PreparationFailure!.Reason);
        Atomic(Derive(Battle(move: 125)), AttackThrowStoneAiScenarioStage.Preparation);
        Atomic(Derive(Battle(moved: true)), AttackThrowStoneAiScenarioStage.Preparation);
        var value = Success(Derive(state));
        var legacy = Evaluate(state: state, scenario: new(value.ScoreMask, value.Scenario1, value.BaselineDestination, value.ActFirstDestination));
        Assert.False(legacy.Accepted); Assert.Null(legacy.Outcome); Assert.Equal(Decision, legacy.RngState);
        Assert.Equal(ThrowStoneAiScanRejection.InvalidScenario, legacy.Failure!.ThrowStoneFailure!.Reason);
        Assert.Equal(AttackThrowStoneAiScenarioRejection.OutOfReach,
            AttackThrowStoneAiMovementScenarioTests.Derive(state).Failure!.Reason);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_policy_order_replay_without_live_changes()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = Battle(move: 1, target: new(4, 1, 0), second: second); var before = Snapshot(state); var request = FreshRequest(second);
            var result = Derive(state, request: request); Success(result);
            Assert.Equal(Serialize(result), Serialize(Derive(state, request: request)));
            Assert.Equal(Serialize(result), Serialize(Derive(Battle(move: 1, target: new(4, 1, 0), second: second, reverse: true),
                request: request with { Policies = request.Policies.Reverse().ToImmutableArray() })));
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static AttackThrowStoneAiOutOfReachScenarioResult Derive(AttackBattleSnapshot state, SwordAiRefreshRequest? request = null,
        AttackThrowStoneAiScenarioProfile? profile = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null)
    {
        var p = Profile(); return AttackThrowStoneAiOutOfReachScenario.Evaluate(state, request ?? FreshRequest(), Stone, new(Sword, 2),
            p.Attack, p.ThrowStone, p.Score, new(true, true, true), new(true, true, true), profile ?? ScenarioProfile, team, rng ?? Decision);
    }
    private static AttackThrowStoneAiOutOfReachScenarioOutcome Success(AttackThrowStoneAiOutOfReachScenarioResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<AttackThrowStoneAiOutOfReachScenarioOutcome>(result.Outcome); }
    private static AttackThrowStoneAiScenarioFailure Atomic(AttackThrowStoneAiOutOfReachScenarioResult result,
        AttackThrowStoneAiScenarioStage stage, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng ?? Decision, result.RngState);
        var failure = Assert.IsType<AttackThrowStoneAiScenarioFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure; }
}
