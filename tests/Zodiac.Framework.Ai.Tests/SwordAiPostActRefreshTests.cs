using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-176/178.
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiPostActRefreshTests
{
    [Fact]
    public void Actual_hit_and_miss_refresh_from_live_post_ACT_HP_and_RNG_without_an_action_refund()
    {
        foreach (var (evade, word, count, raw, hp) in new[] { (0, 3535774167u, 25ul, 21183u, 25), (89, 1343201072u, 30ul, 20495u, 50) })
        {
            var state = AfterAct(evade: evade); var before = Snapshot(state.Battle); var result = Evaluate(state.Battle); var o = Success(result);
            Assert.Equal(new FftRandomState(word, count), result.RngState);
            Assert.Equal(raw, Assert.Single(o.UpkeepPicks).RawRoll); Assert.Equal(35, o.TurnEndCt);
            Assert.Equal(1ul, o.ElapsedTicks); Assert.Equal(state.Battle.TurnBattle.Turns.Ticks + 1, o.Ticks);
            Assert.Equal(B, o.MoveOpportunityUnitId); Assert.Equal(40, o.Units.Single(u => u.UnitId == A).Ct);
            Assert.Equal(0, o.Units.Single(u => u.UnitId == B).Ct);
            Assert.True(o.Units.Single(u => u.UnitId == A).ActionTaken); Assert.False(o.Units.Single(u => u.UnitId == A).MovementTaken);
            Assert.Equal(hp, o.Units.Single(u => u.UnitId == B).Hp);
            Assert.Equal(hp < 37, o.RefreshedUnits.Single(u => u.UnitId == B).HpBelowThreeQuarters);
            Assert.False(o.RefreshedUnits.Single(u => u.UnitId == B).HpBelowHalf); // HP25 == maxHP/2, strict source test.
            Assert.Equal(before, Snapshot(state.Battle)); Assert.Null(state.PendingFacing);
            Assert.Equal(15, state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
        }
    }

    [Fact]
    public void Exact_draw_tick_and_cap_boundaries_use_existing_closed_clock_rules()
    {
        var state = State(moved: false, acted: true); var last = new FftRandomState(1, ulong.MaxValue - 1);
        var fits = Evaluate(state, rng: last); Success(fits); Assert.Equal(ulong.MaxValue, fits.RngState.DrawCount);
        var exhausted = new FftRandomState(1, ulong.MaxValue);
        Atomic(Evaluate(state, rng: exhausted), SwordAiRefreshRejection.RandomDrawCountExhausted, exhausted);
        Atomic(Evaluate(State(moved: false, acted: true, ticks: ulong.MaxValue)), SwordAiRefreshRejection.TickCountExhausted, LiveRng);
        var ready = Success(Evaluate(State(moved: false, acted: true, ticks: ulong.MaxValue, targetCt: 100)));
        Assert.Equal(0ul, ready.ElapsedTicks); Assert.Equal(ulong.MaxValue, ready.Ticks); Assert.Equal(35, ready.TurnEndCt);
        var cap = Success(Evaluate(State(moved: false, acted: true, ct: 95, targetCt: 60, speed: 20, targetSpeed: 20, reverse: true)));
        Assert.Equal(60, cap.TurnEndCt); Assert.Equal(A, cap.MoveOpportunityUnitId); Assert.Equal(2ul, cap.ElapsedTicks);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Source_entry_certificate_and_opposite_choice_modes_remain_explicit_and_terminal_data_rejects()
    {
        var state = AfterAct().Battle; var rng = state.TurnBattle.Turns.RngState;
        Atomic(Evaluate(state, profile: Refresh with { FreshRefreshEntry = false }), SwordAiRefreshRejection.UnsupportedRefreshProfile, rng);
        Atomic(SwordAiRefresh.Evaluate(state, Request(), Candidate, Score, Refresh, rng), SwordAiRefreshRejection.UnavailableAction, rng);
        Atomic(SwordAiRefresh.EvaluateUnmoved(state, Request(), Candidate, Score, Refresh, rng), SwordAiRefreshRejection.UnavailableAction, rng);
        Atomic(Evaluate(State()), SwordAiRefreshRejection.UnavailableAction, LiveRng);
        Atomic(Evaluate(State(moved: false)), SwordAiRefreshRejection.UnavailableAction, LiveRng);
        Atomic(Evaluate(state, request: Request() with { Policies = [] }), SwordAiRefreshRejection.InvalidPolicies, rng);
        Atomic(Evaluate(state, request: Request(second: true)), SwordAiRefreshRejection.UnavailableAction, rng);
        var ko = AfterAct(power: 10).Battle;
        Atomic(Evaluate(ko), SwordAiRefreshRejection.TerminalInput, ko.TurnBattle.Turns.RngState);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_collection_order_and_carried_hypothetical_RNG_preserve_the_actual_battle()
    {
        foreach (bool second in new[] { false, true })
        {
            var state = AfterAct(second: second).Battle; var before = Snapshot(state);
            var result = Evaluate(state, Request(second)); var o = Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, Request(second))));
            Assert.Equal(Serialize(result), Serialize(Evaluate(AfterAct(second: second, reverse: true).Battle, Request(second, reverse: true))));
            var carried = Evaluate(state, Request(second), rng: result.RngState); Success(carried);
            Assert.Equal(new FftBiosRandom().Next(result.RngState).State, carried.RngState);
            Assert.Equal(second ? A : B, o.RefreshedUnits.Single(u => u.Enemy).UnitId);
            Assert.Equal(before, Snapshot(state));
        }
    }

    private static SwordAiRefreshResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest? request = null,
        SwordAiRefreshProfile? profile = null, FftRandomState? rng = null) => SwordAiRefresh.EvaluateAfterAct(state, request ?? Request(),
            Candidate, Score, profile ?? Refresh, rng ?? state.TurnBattle.Turns.RngState);
    private static SwordAiRefreshOutcome Success(SwordAiRefreshResult result)
    { Assert.True(result.Accepted); Assert.Equal(SwordAiRefreshRejection.None, result.Rejection); return Assert.IsType<SwordAiRefreshOutcome>(result.Outcome); }
    private static void Atomic(SwordAiRefreshResult result, SwordAiRefreshRejection reason, FftRandomState rng)
    { Assert.False(result.Accepted); Assert.Equal(reason, result.Rejection); Assert.Null(result.Outcome); Assert.Equal(rng, result.RngState); }
}
