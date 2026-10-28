using Xunit;
using static Zodiac.Framework.Ai.Tests.SwordAiPreparationTestData;

namespace Zodiac.Framework.Ai.Tests;

// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-171/173.
[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiUnmovedPreparedSelectionTests
{
    private static readonly SwordAiPreparedSelectionProfile Profile = new(true, true, true, true, true, true);

    [Fact]
    public void In_reach_stream_retains_refresh_then_one_scoring_scan_and_lottery_sequence()
    {
        foreach (var (evade, word, count, rolls) in new[] { (0, 836760821u, 19ul, 1), (89, 3794901700u, 26ul, 8) })
        {
            var state = Ready(evade: evade, move: false).Battle; var before = Snapshot(state);
            var result = Evaluate(state); var o = Success(result);
            Assert.Null(o.OutOfReach); Assert.Equal(new FftRandomState(word, count), result.RngState);
            Assert.Equal(55, o.Preparation.Refresh.TurnEndCt);
            Assert.Equal(16838u, Assert.Single(o.Preparation.Refresh.UpkeepPicks).RawRoll);
            Assert.Equal(5, o.Preparation.ReachableOrigins.Length);
            Assert.Equal(new[] { 5758u, 10113u, 17515u, 31051u, 5627u },
                o.Decision.MovementScenario.Movement.Select(c => Assert.Single(c.Outcome.UpkeepPicks).RawRoll));
            Assert.Equal(new FftRandomState(180171308, 18), o.Decision.Selection.LotteryEntryRng);
            Assert.Equal(12767u, o.Decision.Selection.Lottery[0].Raw); Assert.Equal(rolls, o.Decision.Selection.Lottery.Length);
            Assert.Equal(evade == 0 ? SwordAiSelectionReason.FirstRoll : SwordAiSelectionReason.AllRollsFailedUseFirst,
                o.Decision.Selection.Reason);
            Assert.Equal(SwordAiSelectedPlanKind.Attack, o.Decision.Selection.Plan.Kind);
            Assert.False(o.Decision.Selection.Plan.ActBeforeMove); Assert.Equal(new TilePosition(2, 1, 0), o.Decision.Selection.Plan.Destination);
            Assert.Single(o.Decision.Selection.Composition.Scan.Outcome!.Candidates);
            Assert.Equal(5, o.Decision.Selection.Composition.Movement.Length);
            for (int i = 0; i < 5; i++) Assert.Same(o.Decision.MovementScenario.Movement[i], o.Decision.Selection.Composition.Movement[i]);
            var preparation = SwordAiPreparation.EvaluateUnmoved(state, Request(), Candidate, Score, Refresh, Preparation,
                SwordAiScenarioTeam.NonBlue, Rng);
            Assert.Equal(Serialize(preparation.Outcome), Serialize(o.Preparation));
            var explicitTail = SwordAiDerivedSelection.Evaluate(state, new(Weapon, 5), Candidate, Score, new(A, B, true),
                new(true, true, true), new(true, true, true, true, true, true, true, true), new(true, true, true), preparation.RngState);
            Assert.Equal(Serialize(explicitTail.Outcome), Serialize(o.Decision)); Assert.Equal(explicitTail.RngState, result.RngState);
            Assert.Equal(before, Snapshot(state));
        }
    }

    [Fact]
    public void Empty_ranking_retains_real_scan_work_and_eight_rolls_then_deferred_Move_only_fallback()
    {
        var state = Ready(evade: 0, move: false).Battle; var result = Evaluate(state, power: 0); var o = Success(result);
        Assert.Equal(new FftRandomState(3794901700, 26), result.RngState);
        Assert.Equal(5, o.Decision.MovementScenario.Movement.Length);
        Assert.All(o.Decision.Selection.Composition.RankedActions, Assert.Null);
        Assert.Equal(8, o.Decision.Selection.Lottery.Length);
        Assert.Equal(SwordAiSelectionReason.MovementFallback, o.Decision.Selection.Reason);
        Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Decision.Selection.Plan.Kind);
        Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, o.Decision.Selection.Plan.WaitFacingHint);
        Assert.Equal(new TilePosition(2, 1, 0), o.Decision.Selection.Plan.Destination);
        Assert.False(state.TurnBattle.Turns.Units.Single(u => u.Id == A).MovementTaken);
        Assert.Equal(15, state.TurnBattle.Turns.Units.Single(u => u.Id == A).Ct);
    }

    [Fact]
    public void Explicit_out_of_reach_branch_keeps_filtered_S_distinct_and_the_post_score_adjustment_draw()
    {
        foreach (var (seed, raw, reach, word) in new[] { (1u, 10113u, 2, 180171308u), (20u, 12502u, 1, 3368358523u) })
        {
            var state = Ready(move: false, origin: new(4, 1, 0)).Battle;
            var result = Evaluate(state, outOfReach: true, rng: new(seed, 7)); var o = Success(result);
            var detail = Assert.IsType<SwordAiOutOfReachSelectionOutcome>(o.OutOfReach);
            Assert.Equal(new FftRandomState(word, 18), result.RngState);
            Assert.Equal(4, o.Preparation.ReachableOrigins.Length); // Full spread, not filtered/scored S.
            var cell = Assert.Single(o.Decision.MovementScenario.Movement);
            Assert.Equal(new TilePosition(3, 1, 0), cell.Outcome.Destination);
            Assert.Equal(raw, detail.MovementScenario.ReachAdjustment!.Raw);
            Assert.Equal(reach, detail.MovementScenario.EffectiveReach);
            Assert.Equal(reach == 1, detail.MovementScenario.AttackUsedDistanceFallback);
            Assert.Equal(SwordAiScenarioTeam.NonBlue, detail.MovementScenario.Team);
            Assert.Equal(10ul, detail.Selection.LotteryEntryRng.DrawCount);
            Assert.Empty(detail.Selection.Composition.Scan.Outcome!.Candidates);
            Assert.Equal(8, detail.Selection.Lottery.Length);
            Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, detail.Selection.Plan.Kind);
            Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, detail.Selection.Plan.WaitFacingHint);
            Assert.Equal(cell.Outcome.Destination, detail.Selection.Plan.Destination);
            Assert.Same(cell, detail.Selection.Composition.Movement[0]);
            Assert.Same(detail.Selection, o.Decision.Selection);
        }
    }

    [Fact]
    public void Exact_budgets_and_late_tick_failure_roll_back_the_entire_owned_prefix()
    {
        foreach (bool far in new[] { false, true })
        {
            var state = Ready(evade: 0, move: false, origin: far ? new(4, 1, 0) : null).Battle;
            int cost = far ? 11 : 12;
            Assert.Equal(ulong.MaxValue, Evaluate(state, outOfReach: far, rng: new(1, ulong.MaxValue - (ulong)cost)).RngState.DrawCount);
            foreach (int available in far ? new[] { 0, 1, 2, 3, 10 } : new[] { 0, 1, 6, 11 })
            {
                var rng = new FftRandomState(1, ulong.MaxValue - (ulong)available);
                var failure = Atomic(Evaluate(state, outOfReach: far, rng: rng),
                    available == 0 ? SwordAiPreparedSelectionStage.Preparation : SwordAiPreparedSelectionStage.Selection, rng);
                if (available == 0) Assert.Equal(SwordAiRefreshRejection.RandomDrawCountExhausted, failure.RefreshRejection);
                else Assert.Equal(available <= (far ? 2 : 1) ? SwordAiDerivedSelectionStage.MovementScenario
                    : !far && available == 6 ? SwordAiDerivedSelectionStage.Composition : SwordAiDerivedSelectionStage.Lottery, failure.SelectionStage);
            }
        }
        var low = Ready(move: false).Battle;
        Assert.Equal(ulong.MaxValue, Evaluate(low, rng: new(1, ulong.MaxValue - 19)).RngState.DrawCount);
        Atomic(Evaluate(low, rng: new(1, ulong.MaxValue - 18)), SwordAiPreparedSelectionStage.Selection, new(1, ulong.MaxValue - 18));
        var late = State(moved: false, move: 1, distance: 2, targetCt: 0, speed: 10, targetSpeed: 1, ticks: ulong.MaxValue - 7);
        Assert.Equal(SwordAiDerivedSelectionStage.Composition, Atomic(Evaluate(late), SwordAiPreparedSelectionStage.Selection).SelectionStage);
        var early = State(moved: false, move: 1, distance: 2, ticks: ulong.MaxValue);
        Assert.Equal(SwordAiRefreshRejection.TickCountExhausted, Atomic(Evaluate(early), SwordAiPreparedSelectionStage.Preparation).RefreshRejection);
        Success(Evaluate(State(moved: false, move: 1, distance: 2, ticks: ulong.MaxValue, targetCt: 100)));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Explicit_branch_choice_entry_and_remaining_certificates_are_required_before_publishing_results()
    {
        var near = Ready(move: false).Battle; var far = Ready(move: false, origin: new(4, 1, 0)).Battle;
        Atomic(Evaluate(near, outOfReach: true), SwordAiPreparedSelectionStage.Selection);
        Atomic(Evaluate(far), SwordAiPreparedSelectionStage.Selection);
        Atomic(Evaluate(near, profile: Profile with { NoDefendSupport = false }), SwordAiPreparedSelectionStage.Admission);
        Atomic(Evaluate(near, preparation: Preparation with { OrdinaryAttackOnlyLists = false }), SwordAiPreparedSelectionStage.Preparation);
        foreach (var team in new[] { SwordAiScenarioTeam.Blue, SwordAiScenarioTeam.Unknown })
            Atomic(Evaluate(far, outOfReach: true, team: team), SwordAiPreparedSelectionStage.Preparation);
        Atomic(Evaluate(near, weapon: new(new("sample:item/wrong"), 5)), SwordAiPreparedSelectionStage.Selection);
        Atomic(Evaluate(Ready().Battle), SwordAiPreparedSelectionStage.Preparation);
        Atomic(SwordAiPreparedSelection.Evaluate(near, Request(), new(Weapon, 5), Candidate, Score, Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, Profile, Rng), SwordAiPreparedSelectionStage.Preparation);
        Atomic(SwordAiPreparedSelection.EvaluateOutOfReach(far, Request(), new(Weapon, 5), Candidate, Score, Refresh, Preparation,
            SwordAiScenarioTeam.NonBlue, Profile, Rng), SwordAiPreparedSelectionStage.Preparation);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_branch_actor_identities_and_collection_order_replay_without_committing_hypothetical_state()
    {
        foreach (bool second in new[] { false, true })
        foreach (bool far in new[] { false, true })
        {
            var state = State(moved: false, move: 1, distance: far ? 3 : 2, second: second); var before = Snapshot(state);
            var request = Request(second); var result = Evaluate(state, request: request, outOfReach: far); var o = Success(result);
            Assert.Equal(Serialize(result), Serialize(Evaluate(state, request: request, outOfReach: far)));
            Assert.Equal(Serialize(result), Serialize(Evaluate(State(moved: false, move: 1, distance: far ? 3 : 2, second: second, reverse: true),
                request: Request(second, reverse: true), outOfReach: far)));
            Assert.Equal(request.ActorId, o.Decision.Selection.Plan.ActorId);
            Assert.Equal(before, Snapshot(state));
            Assert.Equal(LiveRng, state.TurnBattle.Turns.RngState); Assert.Equal(LiveRng, state.TurnBattle.Battle.RngState);
        }
    }

    private static SwordAiPreparedSelectionResult Evaluate(AttackBattleSnapshot state, int power = 5,
        SwordAiRefreshRequest? request = null, SwordAiPreparedSelectionProfile? profile = null, SwordAiPreparationProfile? preparation = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, FftRandomState? rng = null, bool outOfReach = false, BoundedSwordWeapon? weapon = null) =>
        outOfReach ? SwordAiPreparedSelection.EvaluateOutOfReachUnmoved(state, request ?? Request(), weapon ?? new(Weapon, power), Candidate, Score,
            Refresh, preparation ?? Preparation, team, profile ?? Profile, rng ?? Rng)
        : SwordAiPreparedSelection.EvaluateUnmoved(state, request ?? Request(), weapon ?? new(Weapon, power), Candidate, Score,
            Refresh, preparation ?? Preparation, team, profile ?? Profile, rng ?? Rng);
    private static SwordAiPreparedSelectionOutcome Success(SwordAiPreparedSelectionResult result)
    { Assert.True(result.Accepted, Serialize(result.Failure)); Assert.Null(result.Failure); return Assert.IsType<SwordAiPreparedSelectionOutcome>(result.Outcome); }
    private static SwordAiPreparedSelectionFailure Atomic(SwordAiPreparedSelectionResult result, SwordAiPreparedSelectionStage stage, FftRandomState? rng = null)
    { Assert.False(result.Accepted); Assert.Equal(stage, result.Failure!.Stage); Assert.Null(result.Outcome); Assert.Equal(rng ?? Rng, result.RngState); return result.Failure; }
}
