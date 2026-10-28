using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPreparationTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Sole_ally_policy_disables_retreat_and_single_enemy_hint_in_both_cached_paths(bool cachedSupport)
    {
        var state = State(hp: 10); var request = Request(cachedSupport: cachedSupport);
        var result = Evaluate(state, request); var o = Success(result);
        Assert.Equal(new FftRandomState(1103527590, 8), result.RngState);
        Assert.Equal(cachedSupport, o.Refresh.RefreshedUnits.Single(u => u.UnitId == A).TeamSupportUnavailable);
        Assert.Equal(new SwordAiRefreshPolicy(A, true, true), o.Policies.Single(p => p.UnitId == A));
        Assert.Equal(new SwordAiRefreshPolicy(B, false, false), o.Policies.Single(p => p.UnitId == B));
        Assert.False(o.RetreatSupportAvailable); Assert.False(o.SingleEnemyRemaining);
        Assert.True(o.TargetActive); Assert.True(o.ManualAutobattle); Assert.Equal(0, o.RandomUseThreshold);
        Assert.Equal((4, 3), (o.EnemyAbilityReach, o.AllyAbilityReach));
        Assert.Equal((17, 1, (byte)9), (o.ActorDelay, o.TargetDelay, o.FoeProximity));
        Assert.Equal((new TilePosition(1, 1, 0), new TilePosition(2, 1, 0)), (o.Origin, o.FocusPosition));
        Assert.Equal(40, o.Refresh.Units.Single(u => u.UnitId == A).Ct);
        Assert.Equal(0, o.Refresh.Units.Single(u => u.UnitId == B).Ct); // Diagnostic CT must not drive these delays.
    }

    [Theory] [InlineData(99, 255, 0, 1, 0)] [InlineData(99, 255, 100, 1, 9)] [InlineData(99, 1, 99, 1, 9)]
    public void Delay_rounding_ready_peers_and_imminent_equality_use_live_CT(int ct, int speed, int targetCt, int targetSpeed, byte proximity)
    {
        var o = Success(Evaluate(State(ct: ct, speed: speed, targetCt: targetCt, targetSpeed: targetSpeed)));
        Assert.Equal(1, o.ActorDelay); Assert.Equal(proximity, o.FoeProximity);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Fresh_preparation_replays_for_either_actor_and_order_without_adding_draws_or_mutating_live_state()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = State(second: second); var before = Snapshot(state); var request = Request(second);
            var result = Evaluate(state, request); var o = Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, request)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(second: second, reverse: true), Request(second, reverse: true))));
            var refresh = SwordAiRefresh.Evaluate(state, request, Candidate, Score, Refresh, Rng);
            Assert.Equal(refresh.RngState, result.RngState); Assert.Equal(Serialize(refresh.Outcome), Serialize(o.Refresh));
            Assert.Equal(request.TargetId, o.Refresh.RefreshedUnits.Single(u => u.Enemy).UnitId);
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Final_available_refresh_draw_succeeds_and_failures_return_original_RNG_without_partial_preparation()
    {
        var last = new FftRandomState(1, ulong.MaxValue - 1);
        Assert.Equal(ulong.MaxValue, Evaluate(State(), rng: last).RngState.DrawCount);
        var exhausted = new FftRandomState(1, ulong.MaxValue);
        var failed = Evaluate(State(), rng: exhausted);
        Assert.False(failed.Accepted); Assert.Null(failed.Outcome); Assert.Equal(exhausted, failed.RngState);
        Assert.Equal(SwordAiRefreshRejection.RandomDrawCountExhausted, failed.RefreshRejection);
        Assert.Equal(SwordAiPreparationRejection.RefreshRejected, failed.Rejection);
        failed = Evaluate(State(ticks: ulong.MaxValue));
        Assert.Null(failed.Outcome); Assert.Equal(Rng, failed.RngState);
        Assert.Equal(SwordAiRefreshRejection.TickCountExhausted, failed.RefreshRejection);
        Assert.True(Evaluate(State(ticks: ulong.MaxValue, targetCt: 100)).Accepted);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Unknown_source_facts_Blue_policy_and_raw_Move_mismatch_are_explicit_rejections()
    {
        foreach (var p in new[] { new SwordAiPreparationProfile(), Preparation with { OrdinaryAttackOnlyLists = false },
            Preparation with { NoSpecialBehavior = false }, Preparation with { CleanActorDelayCache = false } })
            Atomic(Evaluate(State(), profile: p), SwordAiPreparationRejection.UnsupportedProfile);
        foreach (var team in new[] { SwordAiScenarioTeam.Blue, SwordAiScenarioTeam.Unknown, (SwordAiScenarioTeam)99 })
            Atomic(Evaluate(State(), team: team), SwordAiPreparationRejection.UnsupportedTeam);
        Atomic(Evaluate(State(move: 125)), SwordAiPreparationRejection.UnsupportedProfile);
        var bad = Evaluate(State(moved: false));
        Atomic(bad, SwordAiPreparationRejection.RefreshRejected);
        Assert.Equal(SwordAiRefreshRejection.UnavailableAction, bad.RefreshRejection);
    }
    private static SwordAiPreparationResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest? request = null,
        SwordAiPreparationProfile? profile = null, SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null) =>
        SwordAiPreparation.Evaluate(state, request ?? Request(), Candidate, Score, Refresh, profile ?? Preparation, team, rng ?? Rng);
    private static SwordAiPreparationOutcome Success(SwordAiPreparationResult result)
    {
        Assert.True(result.Accepted); Assert.Equal(SwordAiPreparationRejection.None, result.Rejection);
        Assert.Equal(SwordAiRefreshRejection.None, result.RefreshRejection);
        return Assert.IsType<SwordAiPreparationOutcome>(result.Outcome);
    }
    private static void Atomic(SwordAiPreparationResult result, SwordAiPreparationRejection reason)
    {
        Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Null(result.Outcome); Assert.Equal(Rng, result.RngState);
    }
}
