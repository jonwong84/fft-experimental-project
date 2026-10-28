using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiMovementScenarioTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly FftRandomState Decision = new(1, 7), Live = new(57, 100);
    private static readonly SwordAiCandidateProfile Candidate = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly SwordAiScoreProfile Score = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
    private static readonly SwordAiMovementScenarioProfile Profile = new(true, true, true);

    [Theory] [InlineData(95, 2, 2)] [InlineData(0, 2, 1)]
    public void Derived_destinations_use_original_imminence_distance_and_last_tie_order(int targetCt, int x, int y)
    {
        var state = State(targetCt: targetCt); var result = Evaluate(state); var o = Success(result);
        Assert.Equal(new[] { new TilePosition(2, 0, 0), new(2, 1, 0), new(3, 1, 0), new(2, 2, 0) }, o.Scenario.MovementOrigins);
        Assert.Equal(new TilePosition(x, y, 0), o.Scenario.AttackDestination);
        Assert.Equal(new TilePosition(2, 1, 0), o.Scenario.BaselineDestination);
        Assert.True(o.BaselineUsedDistanceFallback); Assert.Equal(1, o.BaselineIndex);
        Assert.Equal(1, o.TargetDistance); Assert.Equal(2, o.TargetReach);
        Assert.Equal(new ulong[] { 8, 9, 10, 11 }, o.Movement.Select(m => m.RngState.DrawCount));
        Assert.Equal(new FftRandomState(3295386429u, 11), result.RngState);
        Assert.All(o.Ordering, c => Assert.Equal(targetCt == 95 ? 10 - c.TargetDistance : 0, c.Proximity));
        Assert.Equal(targetCt == 95 ? new ulong[] { 21, 21, 21, 21 } : new ulong[] { 33, 29, 33, 33 },
            o.Movement.Select(m => m.Outcome.Ticks));
    }

    [Fact]
    public void In_reach_boundary_uses_closer_D0_and_farther_D1_without_a_reach_random_roll()
    {
        var result = Evaluate(State(origin: new(3, 1, 0))); var o = Success(result);
        Assert.Equal(2, o.TargetDistance); Assert.Equal(2, o.TargetReach);
        Assert.False(o.BaselineUsedDistanceFallback);
        Assert.Equal(new TilePosition(2, 1, 0), o.Scenario.BaselineDestination);
        Assert.Equal(new TilePosition(3, 2, 0), o.Scenario.AttackDestination);
        Assert.Equal(Decision.DrawCount + (ulong)o.Movement.Length, result.RngState.DrawCount);
    }

    [Theory] [InlineData(0, false)] [InlineData(4, true)]
    public void Zero_Move_and_already_moved_keep_singleton_but_reach_uses_original_Move(int move, bool moved)
    {
        var result = Evaluate(State(move: move, moved: moved)); var o = Success(result);
        Assert.Single(o.Movement); Assert.Equal(new TilePosition(2, 1, 0), o.Scenario.AttackDestination);
        Assert.Equal(o.Scenario.AttackDestination, o.Scenario.BaselineDestination);
        Assert.Equal(move + 1, o.TargetReach); Assert.Equal(8ul, result.RngState.DrawCount);
        Assert.Equal(moved ? 35 : 55, o.Movement[0].Outcome.TurnEndCt);
    }

    [Theory] [InlineData(100, 50, 64)] [InlineData(50, 100, -64)]
    public void Negated_u16_priority_is_compared_as_a_signed_packed_value(int actorHp, int targetHp, int priority)
    {
        var o = Success(Evaluate(State(actorHp: actorHp, targetHp: targetHp, maxHp: 100)));
        Assert.All(o.Movement, m => Assert.Equal(priority, m.Outcome.Score.Priority));
        Assert.All(o.Ordering, c => Assert.Equal(unchecked((-priority << 16) + c.Proximity), c.PackedScore));
        Assert.All(o.Ordering, c => Assert.Equal(priority > 0, c.PackedScore < 0));
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [Trait("Coverage", "CurrentAdmission")]
    public void Unknown_source_context_rejects_before_RNG_work(int missing)
    {
        var p = missing switch
        {
            0 => Profile with { FocusedOpponent = false }, 1 => Profile with { OrdinarySwordReach = false },
            2 => Profile with { OrdinaryScenarioPolicy = false }, _ => Profile
        };
        var rng = new FftRandomState(1, ulong.MaxValue);
        var result = SwordAiMovementScenario.Evaluate(State(), Candidate, Score, new(A, B, missing != 3), p, rng);
        Atomic(result, rng, SwordAiMovementScenarioStage.Admission);
        Assert.Equal(SwordAiMovementScenarioRejection.UnsupportedProfile, result.Failure!.Reason);
    }

    [Theory] [InlineData("out-of-reach")] [InlineData("move-cap")] [InlineData("acted")]
    [InlineData("unknown-target")] [InlineData("unknown-actor")] [InlineData("score")] [InlineData("geometry")]
    [Trait("Coverage", "CurrentAdmission")]
    public void Unsupported_branch_and_state_reject_atomically_even_with_exhausted_RNG(string kind)
    {
        var state = State(origin: kind == "out-of-reach" ? new(4, 1, 0) : null,
            move: kind == "move-cap" ? 125 : 1, acted: kind == "acted", height: kind == "geometry" ? 11 : 10);
        var score = kind == "score" ? new SwordAiScoreProfile([new(A, SwordAiScoreFacts.None), new(B, SwordAiScoreFacts.None)]) : Score;
        var rng = new FftRandomState(1, ulong.MaxValue);
        var result = SwordAiMovementScenario.Evaluate(state, Candidate, score,
            new(kind == "unknown-actor" ? new("missing:actor") : A, kind == "unknown-target" ? new("missing:target") : B, true), Profile, rng);
        Atomic(result, rng, SwordAiMovementScenarioStage.Admission);
        if (kind == "out-of-reach") Assert.Equal(SwordAiMovementScenarioRejection.OutOfReach, result.Failure!.Reason);
    }

    [Fact]
    public void Later_cell_exhaustion_discards_completed_cells_but_exact_MAX_succeeds()
    {
        var rng = new FftRandomState(1, ulong.MaxValue - 2);
        var rejected = Evaluate(State(), rng: rng);
        Atomic(rejected, rng, SwordAiMovementScenarioStage.Movement);
        Assert.Equal(2, rejected.Failure!.Index);
        Assert.Equal(SwordAiMovementBaselineRejection.RandomDrawCountExhausted, rejected.Failure.MovementReason);
        var accepted = Evaluate(State(), rng: new(1, ulong.MaxValue - 4)); Success(accepted);
        Assert.Equal(ulong.MaxValue, accepted.RngState.DrawCount);
    }

    [Fact]
    public void Each_cell_starts_at_original_ticks_including_later_moved_tile_overflow()
    {
        var state = State(origin: new(0, 0, 0), target: new(1, 0, 0), targetCt: 0, ticks: ulong.MaxValue - 9);
        var failed = Evaluate(state); Atomic(failed, Decision, SwordAiMovementScenarioStage.Movement);
        Assert.Equal(1, failed.Failure!.Index);
        Assert.Equal(SwordAiMovementBaselineRejection.TickCountExhausted, failed.Failure.MovementReason);
        var exact = Success(Evaluate(State(move: 0, ticks: ulong.MaxValue - 1)));
        Assert.Equal(ulong.MaxValue, exact.Movement[0].Outcome.Ticks);
    }

    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Contract", "QueryIsolation")]
    public void Replay_actor_identity_collection_order_and_explicit_RNG_carry_preserve_live_state(bool second)
    {
        var state = State(second: second); string before = Snapshot(state);
        var result = Evaluate(state, second); Success(result);
        Assert.Equal(Serialize(result), Serialize(Evaluate(state, second)));
        Assert.Equal(Serialize(result), Serialize(Evaluate(State(second: second, reverse: true), second)));
        var carried = Evaluate(state, second, result.RngState); Success(carried);
        Assert.Equal(result.RngState.DrawCount + 4, carried.RngState.DrawCount);
        Assert.Equal(before, Snapshot(state)); Assert.Equal(Live, state.TurnBattle.Turns.RngState);
    }

    private static SwordAiMovementScenarioResult Evaluate(AttackBattleSnapshot state, bool second = false, FftRandomState? rng = null) =>
        SwordAiMovementScenario.Evaluate(state, Candidate, Score, new(second ? B : A, second ? A : B, true), Profile, rng ?? Decision);
    private static SwordAiMovementScenarioOutcome Success(SwordAiMovementScenarioResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Null(result.Failure); return Assert.IsType<SwordAiMovementScenarioOutcome>(result.Outcome); }
    private static void Atomic(SwordAiMovementScenarioResult result, FftRandomState rng, SwordAiMovementScenarioStage stage)
    { Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState); Assert.Equal(stage, result.Failure!.Stage); }
    private static string Serialize(object value) => JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot state) => Serialize(new { state.Combatants, state.TurnBattle.Turns,
        Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }) });
    private static AttackBattleSnapshot State(TilePosition? origin = null, TilePosition? target = null, int move = 1,
        bool moved = false, bool acted = false, int targetCt = 95, int actorHp = 50, int targetHp = 50,
        int maxHp = 50, ulong ticks = 20, bool second = false, bool reverse = false, int height = 10)
    {
        var actor = second ? B : A; var enemy = second ? A : B;
        var movement = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, origin ?? new(2, 1, 0), movement, moved), new(enemy, target ?? new(1, 1, 0), movement) };
        var turns = new TurnUnitState[] { new(actor, 5, 15, 2, true, moved, acted), new(enemy, 5, targetCt, 17) };
        AttackUnitState Combat(BattleUnitId id, int hp) => new(id, hp, maxHp, 0, 0, 5, 0, 0, 0, 0, 70, 0,
            BattleFacing.West, AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, actorHp), Combat(enemy, targetHp) };
        return new(new(new(new(5, 3, 1, Enumerable.Repeat(new BattleTile(Ground, height), 15).ToArray()), reverse ? bodies.Reverse() : bodies, Live),
            new(reverse ? turns.Reverse() : turns, Live, ticks, TurnCyclePhase.UnitTurn)), reverse ? combat.Reverse() : combat);
    }
}
