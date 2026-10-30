using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-176/179.
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPostActPreparationTests
{
    [Fact]
    public void Owned_post_ACT_refresh_keeps_reach_but_disables_Attack_and_derives_policy_from_live_CT()
    {
        foreach (var (evade, word, count) in new[] { (0, 3535774167u, 25ul), (89, 1343201072u, 30ul) })
        {
            var state = AfterAct(evade: evade).Battle; var before = Snapshot(state); var result = Evaluate(state); var o = Success(result);
            var refresh = SwordAiRefresh.EvaluateAfterAct(state, Request(), Candidate, Score, Refresh, state.TurnBattle.Turns.RngState);
            Assert.True(refresh.Accepted); Assert.Equal(Serialize(refresh.Outcome), Serialize(o.Refresh));
            Assert.Equal(refresh.RngState, result.RngState); Assert.Equal(new FftRandomState(word, count), result.RngState);
            Assert.False(o.ActorAttackUsable); Assert.Equal((2, 1), (o.EnemyAbilityReach, o.AllyAbilityReach));
            Assert.Equal((17, 1, (byte)9), (o.ActorDelay, o.TargetDelay, o.FoeProximity));
            Assert.Equal(40, o.Refresh.Units.Single(u => u.UnitId == A).Ct); Assert.Equal(0, o.Refresh.Units.Single(u => u.UnitId == B).Ct);
            Assert.Equal(new TilePosition(2, 1, 0), o.Origin); Assert.Equal(new TilePosition(1, 1, 0), o.FocusPosition);
            Assert.Equal(new[] { new TilePosition(2, 0, 0), new(2, 1, 0), new(3, 1, 0), new(2, 2, 0) }, o.ReachableOrigins);
            Assert.Equal(new SwordAiRefreshPolicy(A, true, true), o.Policies.Single(u => u.UnitId == A));
            Assert.False(o.Refresh.RefreshedUnits.Single(u => u.UnitId == A).TeamSupportUnavailable);
            Assert.False(o.RetreatSupportAvailable); Assert.False(o.SingleEnemyRemaining); Assert.True(o.TargetActive); Assert.True(o.ManualAutobattle);
            Assert.Equal(0, o.RandomUseThreshold); Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Real_knockback_and_Critical_HP_change_focus_geometry_and_flags_without_reusing_pre_ACT_data()
    {
        var pushed = AfterAct(power: 1, entryWord: 3).Battle; var before = Snapshot(pushed); var result = Evaluate(pushed); var o = Success(result);
        Assert.Equal(new TilePosition(0, 1, 0), o.FocusPosition); // Actual critical pushes target from x1 to x0.
        Assert.Equal(5, o.ReachableOrigins.Length); Assert.Contains(new TilePosition(1, 1, 0), o.ReachableOrigins);
        Assert.Equal((byte)8, o.FoeProximity); Assert.False(o.ActorAttackUsable); Assert.Equal(2, o.EnemyAbilityReach);
        Assert.Equal(new FftBiosRandom().Next(pushed.TurnBattle.Turns.RngState).State, result.RngState);
        Assert.Equal(before, Snapshot(pushed));
        var critical = AfterAct(power: 8).Battle; var hp = Success(Evaluate(critical));
        Assert.Equal(10, hp.Refresh.Units.Single(u => u.UnitId == B).Hp);
        Assert.Equal(AttackUnitStatus.Critical, hp.Refresh.Units.Single(u => u.UnitId == B).Status);
        Assert.True(hp.Refresh.RefreshedUnits.Single(u => u.UnitId == B).HpBelowHalf);
        Assert.Equal(new TilePosition(1, 1, 0), hp.FocusPosition); Assert.Equal(4, hp.ReachableOrigins.Length);
    }

    [Fact]
    public void No_draws_follow_refresh_including_MAX_and_zero_Move_while_failures_hide_the_whole_prefix()
    {
        var state = AfterAct().Battle; var last = new FftRandomState(state.TurnBattle.Turns.RngState.Value, ulong.MaxValue - 1);
        var fits = Evaluate(state, rng: last); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var exhausted = last with { DrawCount = ulong.MaxValue };
        Atomic(Evaluate(state, rng: exhausted), SwordAiPreparationRejection.RefreshRejected, SwordAiRefreshRejection.RandomDrawCountExhausted, exhausted);
        var ticks = State(moved: false, acted: true, ticks: ulong.MaxValue);
        Atomic(Evaluate(ticks), SwordAiPreparationRejection.RefreshRejected, SwordAiRefreshRejection.TickCountExhausted, LiveRng);
        var zero = Success(Evaluate(State(moved: false, acted: true, move: 0, ticks: ulong.MaxValue, targetCt: 100)));
        Assert.Equal(zero.Origin, Assert.Single(zero.ReachableOrigins)); Assert.Equal((1, 0), (zero.EnemyAbilityReach, zero.AllyAbilityReach));
        Assert.False(zero.ActorAttackUsable); Assert.Equal(0ul, zero.Refresh.ElapsedTicks);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Source_and_choice_entries_remain_explicit_and_do_not_infer_support_from_ACT_used()
    {
        var state = AfterAct().Battle; var rng = state.TurnBattle.Turns.RngState;
        Atomic(SwordAiPreparation.Evaluate(state, Request(), Candidate, Score, Refresh, Preparation, SwordAiScenarioTeam.NonBlue, rng),
            SwordAiPreparationRejection.RefreshRejected, SwordAiRefreshRejection.UnavailableAction, rng);
        Atomic(SwordAiPreparation.EvaluateUnmoved(state, Request(), Candidate, Score, Refresh, Preparation, SwordAiScenarioTeam.NonBlue, rng),
            SwordAiPreparationRejection.RefreshRejected, SwordAiRefreshRejection.UnavailableAction, rng);
        foreach (var team in new[] { SwordAiScenarioTeam.Blue, SwordAiScenarioTeam.Unknown })
            Atomic(Evaluate(state, team: team), SwordAiPreparationRejection.UnsupportedTeam, rng: rng);
        Atomic(Evaluate(state, profile: Preparation with { CleanActorDelayCache = false }), SwordAiPreparationRejection.UnsupportedProfile, rng: rng);
        Atomic(Evaluate(state, refresh: Refresh with { FreshRefreshEntry = false }), SwordAiPreparationRejection.RefreshRejected,
            SwordAiRefreshRejection.UnsupportedRefreshProfile, rng);
        Atomic(Evaluate(State(moved: false)), SwordAiPreparationRejection.RefreshRejected, SwordAiRefreshRejection.UnavailableAction, LiveRng);
        Atomic(Evaluate(State(moved: false, acted: true, move: 125)), SwordAiPreparationRejection.UnsupportedProfile, rng: LiveRng);
        var ko = AfterAct(power: 10).Battle;
        Atomic(Evaluate(ko), SwordAiPreparationRejection.RefreshRejected, SwordAiRefreshRejection.TerminalInput, ko.TurnBattle.Turns.RngState);
        var unmoved = Ready(move: false).Battle;
        Assert.True(SwordAiPreparation.EvaluateUnmoved(unmoved, Request(), Candidate, Score, Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, unmoved.TurnBattle.Turns.RngState).Outcome!.ActorAttackUsable);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Either_actor_order_and_carried_hypothetical_RNG_replay_without_committing_to_the_battle()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = AfterAct(second: second).Battle; var before = Snapshot(state); var result = Evaluate(state, Request(second)); Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, Request(second))));
            Assert.Equal(Serialize(result), Serialize(Evaluate(AfterAct(second: second, reverse: true).Battle, Request(second, reverse: true))));
            var carried = Evaluate(state, Request(second), rng: result.RngState); Success(carried);
            Assert.Equal(new FftBiosRandom().Next(result.RngState).State, carried.RngState); Assert.Equal(before, Snapshot(state));
        }
    }

    private static SwordAiPreparationResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest? request = null,
        SwordAiPreparationProfile? profile = null, SwordAiRefreshProfile? refresh = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null) =>
        SwordAiPreparation.EvaluateAfterAct(state, request ?? Request(), Candidate, Score, refresh ?? Refresh,
            profile ?? Preparation, team, rng ?? state.TurnBattle.Turns.RngState);
    private static SwordAiPreparationOutcome Success(SwordAiPreparationResult result)
    { Assert.True(result.Accepted, Serialize(result)); Assert.Equal(SwordAiPreparationRejection.None, result.Rejection);
        return Assert.IsType<SwordAiPreparationOutcome>(result.Outcome); }
    private static void Atomic(SwordAiPreparationResult result, SwordAiPreparationRejection reason,
        SwordAiRefreshRejection refresh = SwordAiRefreshRejection.None, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Equal(refresh, result.RefreshRejection);
        Assert.Null(result.Outcome); Assert.Equal(rng ?? Rng, result.RngState); }
}
