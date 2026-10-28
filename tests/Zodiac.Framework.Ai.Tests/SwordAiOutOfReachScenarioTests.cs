using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiOutOfReachScenarioTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly FftRandomState Decision = new(1, 7), Live = new(57, 100);
    private static readonly SwordAiCandidateProfile Candidate = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly SwordAiScoreProfile Score = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
    private static readonly SwordAiMovementScenarioProfile Profile = new(true, true, true);

    [Theory]
    [InlineData(SwordAiScenarioTeam.Blue, 1u, 5, 662824084u, 10ul)]
    [InlineData(SwordAiScenarioTeam.NonBlue, 1u, 5, 3295386429u, 11ul)]
    [InlineData(SwordAiScenarioTeam.Blue, 19u, 5, 811149646u, 10ul)]
    [InlineData(SwordAiScenarioTeam.NonBlue, 19u, 3, 2162199151u, 11ul)]
    public void Source_vectors_filter_before_scoring_and_adjust_reach_only_after_the_prefix(
        SwordAiScenarioTeam team, uint seed, int destination, uint word, ulong count)
    {
        var result = Evaluate(State(), team, new(seed, 7)); var o = Success(result); var m = o.MovementScenario;
        Assert.Equal(new[] { new TilePosition(3, 0, 0), new(4, 0, 0), new(5, 0, 0) }, m.Scenario.MovementOrigins);
        Assert.DoesNotContain(new TilePosition(6, 0, 0), m.Scenario.MovementOrigins);
        Assert.Equal(new byte[] { 2, 3, 4 }, m.Ordering.Select(c => c.TargetDistance));
        Assert.Equal(new byte[] { 8, 7, 6 }, m.Ordering.Select(c => c.Proximity));
        Assert.Equal(new TilePosition(destination, 0, 0), m.Scenario.AttackDestination);
        Assert.Equal(m.Scenario.AttackDestination, m.Scenario.BaselineDestination);
        Assert.Equal(destination - 3, m.BaselineIndex); Assert.False(m.BaselineUsedDistanceFallback);
        Assert.False(o.AttackUsedDistanceFallback); Assert.Equal(team, o.Team);
        Assert.Equal(new FftRandomState(word, count), result.RngState);
        Assert.Equal(new ulong[] { 8, 9, 10 }, m.Movement.Select(v => v.RngState.DrawCount));
        Assert.All(m.Movement, v => { Assert.Equal(21ul, v.Outcome.Ticks); Assert.Equal(0, v.Outcome.Score.Priority); });
        Assert.Equal(5, m.TargetDistance); Assert.Equal(4, m.TargetReach);
        if (team == SwordAiScenarioTeam.Blue) { Assert.Null(o.ReachAdjustment); Assert.Equal(4, o.EffectiveReach); }
        else Assert.Equal(seed == 19 ? new SwordAiReachAdjustment(224, 0, 4, 2) : new(17515, 1, 4, 4), o.ReachAdjustment);
    }

    [Theory] [InlineData(1u)] [InlineData(19u)]
    public void Ineligible_enemy_still_consumes_adjustment_even_when_the_destination_does_not_change(uint seed)
    {
        var result = Evaluate(State(targetCt: 0), SwordAiScenarioTeam.NonBlue, new(seed, 7)); var o = Success(result);
        Assert.All(o.MovementScenario.Ordering, c => Assert.Equal(0, c.Proximity));
        Assert.Equal(new TilePosition(3, 0, 0), o.MovementScenario.Scenario.AttackDestination);
        Assert.NotNull(o.ReachAdjustment); Assert.Equal(11ul, result.RngState.DrawCount);
    }

    [Theory] [InlineData(SwordAiScenarioTeam.Blue, 8ul)] [InlineData(SwordAiScenarioTeam.NonBlue, 9ul)]
    public void No_match_filter_copies_singleton_and_destination_fallback_does_not_rescore(SwordAiScenarioTeam team, ulong count)
    {
        var result = Evaluate(State(origin: new(3, 0, 0), move: 0), team); var o = Success(result);
        Assert.Single(o.MovementScenario.Movement); Assert.True(o.AttackUsedDistanceFallback);
        Assert.Equal(new TilePosition(3, 0, 0), o.MovementScenario.Scenario.BaselineDestination);
        Assert.Equal(count, result.RngState.DrawCount); Assert.False(o.MovementScenario.BaselineUsedDistanceFallback);
        var moved = Success(Evaluate(State(moved: true), team));
        Assert.Single(moved.MovementScenario.Movement); Assert.Equal(4, moved.MovementScenario.TargetReach);
    }

    [Fact]
    public void Reach_one_can_halve_to_zero_and_distance_first_complete_ties_keep_the_last_tile()
    {
        var o = Success(Evaluate(State(origin: new(3, 0, 0), move: 0), SwordAiScenarioTeam.NonBlue, new(6, 7)));
        Assert.Equal(new SwordAiReachAdjustment(25515, 0, 1, 0), o.ReachAdjustment);
        Assert.True(o.AttackUsedDistanceFallback); Assert.Equal(0, o.EffectiveReach);
        var tie = Success(Evaluate(State(width: 5, depth: 3, origin: new(4, 2, 0), target: new(0, 0, 0), move: 1), SwordAiScenarioTeam.Blue));
        Assert.Equal(new[] { new TilePosition(4, 1, 0), new(3, 2, 0) }, tie.MovementScenario.Scenario.MovementOrigins);
        Assert.True(tie.AttackUsedDistanceFallback);
        Assert.Equal(new TilePosition(3, 2, 0), tie.MovementScenario.Scenario.AttackDestination);
    }

    [Theory] [InlineData(SwordAiScenarioTeam.Unknown)] [InlineData((SwordAiScenarioTeam)99)]
    [Trait("Coverage", "CurrentAdmission")]
    public void Unknown_team_is_rejected_before_any_evaluation(SwordAiScenarioTeam team)
    {
        var rng = new FftRandomState(1, ulong.MaxValue); var result = Evaluate(State(), team, rng);
        Atomic(result, rng, SwordAiMovementScenarioStage.Admission);
        Assert.Equal(SwordAiMovementScenarioRejection.UnsupportedProfile, result.Failure!.Reason);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Branch_boundary_and_unknown_source_context_do_not_fall_through_to_another_search()
    {
        var rng = new FftRandomState(1, ulong.MaxValue);
        var boundary = Evaluate(State(origin: new(5, 0, 0)), SwordAiScenarioTeam.NonBlue, rng);
        Atomic(boundary, rng, SwordAiMovementScenarioStage.Admission);
        Assert.Equal(SwordAiMovementScenarioRejection.WrongBranch, boundary.Failure!.Reason);
        var missing = SwordAiMovementScenario.EvaluateOutOfReach(State(), Candidate, Score, new(A, B, true),
            Profile with { OrdinarySwordReach = false }, SwordAiScenarioTeam.Blue, rng);
        Atomic(missing, rng, SwordAiMovementScenarioStage.Admission);
        var legacy = SwordAiMovementScenario.Evaluate(State(), Candidate, Score, new(A, B, true), Profile, rng);
        Assert.False(legacy.Accepted); Assert.Equal(SwordAiMovementScenarioRejection.OutOfReach, legacy.Failure!.Reason);
        Assert.Equal(rng, legacy.RngState);
    }

    [Fact]
    public void Post_score_adjustment_failure_discards_completed_cells_while_Blue_needs_no_reserved_draw()
    {
        var rng = new FftRandomState(1, ulong.MaxValue - 3);
        var failed = Evaluate(State(), SwordAiScenarioTeam.NonBlue, rng);
        Atomic(failed, rng, SwordAiMovementScenarioStage.ReachAdjustment);
        Assert.Equal(SwordAiMovementScenarioRejection.RandomDrawCountExhausted, failed.Failure!.Reason);
        var blue = Evaluate(State(), SwordAiScenarioTeam.Blue, rng); Success(blue); Assert.Equal(ulong.MaxValue, blue.RngState.DrawCount);
        var exact = Evaluate(State(), SwordAiScenarioTeam.NonBlue, new(1, ulong.MaxValue - 4)); Success(exact);
        Assert.Equal(ulong.MaxValue, exact.RngState.DrawCount);
        var early = Evaluate(State(), SwordAiScenarioTeam.NonBlue, new(1, ulong.MaxValue - 2));
        Atomic(early, new(1, ulong.MaxValue - 2), SwordAiMovementScenarioStage.Movement); Assert.Equal(2, early.Failure!.Index);
    }

    [Fact]
    public void Each_cell_uses_original_ticks_and_adjustment_adds_no_tick_reservation()
    {
        var o = Success(Evaluate(State(ticks: ulong.MaxValue - 1), SwordAiScenarioTeam.NonBlue));
        Assert.All(o.MovementScenario.Movement, c => Assert.Equal(ulong.MaxValue, c.Outcome.Ticks));
        var failed = Evaluate(State(ticks: ulong.MaxValue), SwordAiScenarioTeam.Blue);
        Atomic(failed, Decision, SwordAiMovementScenarioStage.Movement);
        Assert.Equal(SwordAiMovementBaselineRejection.TickCountExhausted, failed.Failure!.MovementReason);
    }

    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Contract", "QueryIsolation")]
    public void Replay_order_identity_and_RNG_carry_leave_the_live_snapshot_unchanged(bool second)
    {
        var state = State(second: second); var before = Snapshot(state);
        var result = Evaluate(state, SwordAiScenarioTeam.NonBlue, second: second); Success(result);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Evaluate(state, SwordAiScenarioTeam.NonBlue, second: second)));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Evaluate(State(second: second, reverse: true), SwordAiScenarioTeam.NonBlue, second: second)));
        var carried = Evaluate(state, SwordAiScenarioTeam.NonBlue, result.RngState, second); Success(carried);
        Assert.Equal(result.RngState.DrawCount + 4, carried.RngState.DrawCount);
        Assert.Equal(before, Snapshot(state)); Assert.Equal(Live, state.TurnBattle.Turns.RngState);
    }

    private static SwordAiOutOfReachScenarioResult Evaluate(AttackBattleSnapshot state, SwordAiScenarioTeam team,
        FftRandomState? rng = null, bool second = false) => SwordAiMovementScenario.EvaluateOutOfReach(state,
            Candidate, Score, new(second ? B : A, second ? A : B, true), Profile, team, rng ?? Decision);
    private static SwordAiOutOfReachScenarioOutcome Success(SwordAiOutOfReachScenarioResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Null(result.Failure); return Assert.IsType<SwordAiOutOfReachScenarioOutcome>(result.Outcome); }
    private static void Atomic(SwordAiOutOfReachScenarioResult result, FftRandomState rng, SwordAiMovementScenarioStage stage)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState); Assert.Equal(stage, result.Failure!.Stage); }
    private static string Snapshot(AttackBattleSnapshot state) => JsonSerializer.Serialize(new { state.Combatants, state.TurnBattle.Turns,
        Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }) });
    private static AttackBattleSnapshot State(int width = 7, int depth = 1, TilePosition? origin = null, TilePosition? target = null,
        int move = 3, int targetCt = 95, bool moved = false, ulong ticks = 20, bool second = false, bool reverse = false)
    {
        var actor = second ? B : A; var enemy = second ? A : B;
        var movement = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, origin ?? new(6, 0, 0), movement, moved), new(enemy, target ?? new(1, 0, 0), movement) };
        var turns = new TurnUnitState[] { new(actor, 5, 15, 2, true, moved), new(enemy, 5, targetCt, 17) };
        var combat = new[] { actor, enemy }.Select(id => new AttackUnitState(id, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0,
            BattleFacing.East, AttackUnitStatus.None, 0, Weapon)).ToArray();
        return new(new(new(new(width, depth, 1, Enumerable.Repeat(new BattleTile(Ground, 10), width * depth).ToArray()),
            reverse ? bodies.Reverse() : bodies, Live), new(reverse ? turns.Reverse() : turns, Live, ticks, TurnCyclePhase.UnitTurn)),
            reverse ? combat.Reverse() : combat);
    }
}
