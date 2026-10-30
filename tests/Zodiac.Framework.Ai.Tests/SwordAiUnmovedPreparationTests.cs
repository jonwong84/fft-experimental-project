using System.Text.Json;
using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-171/172.
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiUnmovedPreparationTests
{
    [Fact]
    public void Source_clock_vectors_cover_both_refunds_cap_slot_tie_and_exact_draw_tick_limits()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "ai-unmoved-preparation-contract-v1.json")));
        foreach (var row in fixture.RootElement.GetProperty("clockVectors").EnumerateArray())
        {
            var input = row.GetProperty("input"); var expected = row.GetProperty("expected");
            var units = input.GetProperty("units").EnumerateArray().ToArray();
            var actor = units.Single(u => u.GetProperty("id").GetString() == "a");
            var target = units.Single(u => u.GetProperty("id").GetString() == "b");
            var state = State(ct: actor.GetProperty("ct").GetInt32(), targetCt: target.GetProperty("ct").GetInt32(),
                speed: actor.GetProperty("speed").GetInt32(), targetSpeed: target.GetProperty("speed").GetInt32(),
                ticks: input.GetProperty("ticks").GetUInt64(), moved: false, reverse: units[0].GetProperty("id").GetString() == "b");
            var rng = new FftRandomState(input.GetProperty("word").GetUInt32(), input.GetProperty("count").GetUInt64());
            var before = Snapshot(state);
            var refresh = SwordAiRefresh.EvaluateUnmoved(state, Request(), Candidate, Score, Refresh, rng);
            var preparation = Evaluate(state, rng: rng);
            Assert.Equal(expected.GetProperty("accepted").GetBoolean(), refresh.Accepted);
            Assert.Equal(refresh.Accepted, preparation.Accepted);
            var final = new FftRandomState(expected.GetProperty("word").GetUInt32(), expected.GetProperty("count").GetUInt64());
            Assert.Equal(final, refresh.RngState); Assert.Equal(final, preparation.RngState);
            if (refresh.Accepted)
            {
                var o = Assert.IsType<SwordAiRefreshOutcome>(refresh.Outcome);
                Assert.Equal(expected.GetProperty("endCt").GetInt32(), o.TurnEndCt);
                Assert.Equal(expected.GetProperty("picked").GetString() == "a" ? A : B, o.MoveOpportunityUnitId);
                Assert.Equal(expected.GetProperty("elapsed").GetUInt64(), o.ElapsedTicks);
                Assert.Equal(expected.GetProperty("ticks").GetUInt64(), o.Ticks);
                Assert.Equal(expected.GetProperty("raw").GetUInt32(), Assert.Single(o.UpkeepPicks).RawRoll);
                Assert.Equal(expected.GetProperty("ct").GetProperty("a").GetInt32(), o.Units.Single(u => u.UnitId == A).Ct);
                Assert.Equal(expected.GetProperty("ct").GetProperty("b").GetInt32(), o.Units.Single(u => u.UnitId == B).Ct);
                Assert.Equal(Serialize(o), Serialize(Success(preparation).Refresh));
            }
            else
            {
                Assert.Null(refresh.Outcome); Assert.Null(preparation.Outcome);
                Assert.Equal(rng.DrawCount == ulong.MaxValue ? SwordAiRefreshRejection.RandomDrawCountExhausted
                    : SwordAiRefreshRejection.TickCountExhausted, refresh.Rejection);
                Assert.Equal(SwordAiPreparationRejection.RefreshRejected, preparation.Rejection);
                Assert.Equal(refresh.Rejection, preparation.RefreshRejection);
            }
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Preparation_returns_full_restored_reachable_origins_and_legitimate_zero_Move_singleton()
    {
        foreach (int move in new[] { 1, 0 })
        {
            var state = ReachState(move); var before = Snapshot(state);
            var result = Evaluate(state); var o = Success(result);
            TilePosition[] expected = move == 0 ? [new(3, 1, 0)]
                : [new(3, 0, 0), new(2, 1, 0), new(3, 1, 0), new(4, 1, 0), new(3, 2, 0)];
            Assert.Equal(expected, o.ReachableOrigins);
            Assert.Equal((move + 1, move), (o.EnemyAbilityReach, o.AllyAbilityReach));
            Assert.Equal((new TilePosition(3, 1, 0), new TilePosition(1, 1, 0)), (o.Origin, o.FocusPosition));
            Assert.Equal(new FftRandomState(1103527590, 8), result.RngState);
            Assert.Equal(55, o.Refresh.TurnEndCt); Assert.Equal(60, o.Refresh.Units.Single(u => u.UnitId == A).Ct);
            Assert.Equal((17, 1, (byte)8), (o.ActorDelay, o.TargetDelay, o.FoeProximity));
            Assert.Equal(before, Snapshot(state));
            Assert.False(state.TurnBattle.Turns.Units.Single(u => u.Id == A).MovementTaken);
        }
    }

    [Fact]
    public void Actor_can_be_hypothetically_ready_while_policy_uses_original_CT_flags_and_positions()
    {
        var state = State(ct: 15, targetCt: 40, speed: 10, targetSpeed: 10, moved: false, distance: 2, hp: 10);
        var o = Success(Evaluate(state));
        Assert.Equal(A, o.Refresh.MoveOpportunityUnitId); Assert.Equal(5ul, o.Refresh.ElapsedTicks);
        Assert.Equal(5, o.Refresh.Units.Single(u => u.UnitId == A).Ct);
        Assert.Equal(90, o.Refresh.Units.Single(u => u.UnitId == B).Ct);
        Assert.Equal((9, 6, (byte)8), (o.ActorDelay, o.TargetDelay, o.FoeProximity));
        Assert.Equal(new SwordAiRefreshPolicy(A, true, true), o.Policies.Single(p => p.UnitId == A));
        Assert.Equal(new SwordAiRefreshPolicy(B, false, false), o.Policies.Single(p => p.UnitId == B));
        Assert.False(o.Refresh.RefreshedUnits.Single(u => u.UnitId == A).TeamSupportUnavailable);
        Assert.True(o.Refresh.RefreshedUnits.Single(u => u.UnitId == A).HpBelowHalf);
        Assert.True(o.TargetActive); Assert.True(o.ManualAutobattle);
        Assert.False(o.RetreatSupportAvailable); Assert.False(o.SingleEnemyRemaining); Assert.Equal(0, o.RandomUseThreshold);
        Assert.Equal(new TilePosition(1, 1, 0), o.Origin); Assert.Equal(new TilePosition(3, 1, 0), o.FocusPosition);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Choice_entries_remain_distinct_and_unknown_context_never_returns_partial_preparation()
    {
        var state = State(moved: false);
        var oldRefresh = SwordAiRefresh.Evaluate(state, Request(), Candidate, Score, Refresh, Rng);
        Assert.False(oldRefresh.Accepted); Assert.Equal(SwordAiRefreshRejection.UnavailableAction, oldRefresh.Rejection);
        Assert.Null(oldRefresh.Outcome); Assert.Equal(Rng, oldRefresh.RngState);
        Atomic(SwordAiPreparation.Evaluate(state, Request(), Candidate, Score, Refresh, Preparation, SwordAiScenarioTeam.NonBlue, Rng),
            SwordAiPreparationRejection.RefreshRejected, SwordAiRefreshRejection.UnavailableAction);
        foreach (var unavailable in new[] { State(), State(moved: false, acted: true) })
        {
            var refresh = SwordAiRefresh.EvaluateUnmoved(unavailable, Request(), Candidate, Score, Refresh, Rng);
            Assert.False(refresh.Accepted); Assert.Equal(SwordAiRefreshRejection.UnavailableAction, refresh.Rejection);
            Assert.Null(refresh.Outcome); Assert.Equal(Rng, refresh.RngState);
            Atomic(Evaluate(unavailable), SwordAiPreparationRejection.RefreshRejected, SwordAiRefreshRejection.UnavailableAction);
        }
        Atomic(Evaluate(state, profile: Preparation with { CleanActorDelayCache = false }), SwordAiPreparationRejection.UnsupportedProfile);
        Atomic(Evaluate(state, refresh: Refresh with { FreshRefreshEntry = false }), SwordAiPreparationRejection.RefreshRejected,
            SwordAiRefreshRejection.UnsupportedRefreshProfile);
        foreach (var team in new[] { SwordAiScenarioTeam.Blue, SwordAiScenarioTeam.Unknown })
            Atomic(Evaluate(state, team: team), SwordAiPreparationRejection.UnsupportedTeam);
        Atomic(Evaluate(State(moved: false, move: 125)), SwordAiPreparationRejection.UnsupportedProfile);
        Atomic(Evaluate(state, request: Request() with { Policies = [] }), SwordAiPreparationRejection.RefreshRejected,
            SwordAiRefreshRejection.InvalidPolicies);
        Atomic(Evaluate(state, request: Request(second: true)), SwordAiPreparationRejection.RefreshRejected,
            SwordAiRefreshRejection.UnavailableAction);
        // Keep the old entry's outcome semantics as well as its admission unchanged.
        var legacy = SwordAiPreparation.Evaluate(State(), Request(), Candidate, Score, Refresh, Preparation, SwordAiScenarioTeam.NonBlue, Rng);
        Assert.Equal(35, Success(legacy).Refresh.TurnEndCt); Assert.Empty(legacy.Outcome!.ReachableOrigins);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_and_input_order_replay_and_carry_only_the_explicit_hypothetical_stream()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = State(moved: false, second: second, distance: 2); var before = Snapshot(state);
            var request = Request(second); var result = Evaluate(state, request); var o = Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, request)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(moved: false, second: second, reverse: true, distance: 2), Request(second, reverse: true))));
            Assert.Equal(request.TargetId, o.Refresh.RefreshedUnits.Single(u => u.Enemy).UnitId);
            Assert.Equal(new FftRandomState(2524885223, 9), Evaluate(state, request, rng: result.RngState).RngState);
            Assert.Equal(before, Snapshot(state));
            Assert.Equal(LiveRng, state.TurnBattle.Turns.RngState); Assert.Equal(LiveRng, state.TurnBattle.Battle.RngState);
        }
    }

    private static SwordAiPreparationResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest? request = null,
        SwordAiPreparationProfile? profile = null, SwordAiRefreshProfile? refresh = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null) =>
        SwordAiPreparation.EvaluateUnmoved(state, request ?? Request(), Candidate, Score, refresh ?? Refresh,
            profile ?? Preparation, team, rng ?? Rng);
    private static SwordAiPreparationOutcome Success(SwordAiPreparationResult result)
    { Assert.True(result.Accepted, Serialize(result)); Assert.Equal(SwordAiPreparationRejection.None, result.Rejection);
        Assert.Equal(SwordAiRefreshRejection.None, result.RefreshRejection); return Assert.IsType<SwordAiPreparationOutcome>(result.Outcome); }
    private static void Atomic(SwordAiPreparationResult result, SwordAiPreparationRejection reason,
        SwordAiRefreshRejection refresh = SwordAiRefreshRejection.None)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Equal(refresh, result.RefreshRejection);
        Assert.Null(result.Outcome); Assert.Equal(Rng, result.RngState); }
    private static AttackBattleSnapshot ReachState(int move)
    {
        var state = State(moved: false, move: move, distance: 2);
        var bodies = state.TurnBattle.Battle.Units.Select(u => new BattleUnitState(u.Id,
            u.Id == A ? new(3, 1, 0) : new(1, 1, 0), u.MovementProfile, u.HasMoved));
        var map = new BattleMapDefinition(5, 3, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 15).ToArray());
        return new(new(new(map, bodies, LiveRng), state.TurnBattle.Turns), state.Combatants);
    }
}
