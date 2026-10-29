using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiSelectionTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly TilePosition Actor = new(2, 1, 0), Top = new(1, 0, 0), Bottom = new(1, 2, 0);
    private static readonly FftRandomState Decision = new(1, 7), LiveRng = new(57, 100);
    private static readonly SwordAiCandidateProfile Candidate = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly AiScoreProfile Score = new([new(A, AiScoreFacts.ClosedProfile), new(B, AiScoreFacts.ClosedProfile)]);

    private sealed record Frame(AttackBattleSnapshot State, SwordAiCompositionScenario Scenario, BoundedSwordWeapon? Weapon,
        SwordAiCandidateProfile Candidate, AiScoreProfile Score, SwordAiComparisonProfile Comparison,
        SwordAiMovementBaselineProfile Baseline, SwordAiScanProfile Scan, SwordAiCompositionProfile Composition,
        SwordAiSelectionProfile Profile, FftRandomState Rng);
    private static Frame Setup(AttackBattleSnapshot? state = null, bool second = false)
    {
        state ??= State(); var actor = second ? B : A;
        state.TurnBattle.Battle.TryGetUnit(actor, out var body); var p = body!.Position;
        return new(state, new([p], p, p), new(Weapon, 5), Candidate, Score, new(actor, second ? A : B, true),
            new(true, true, true, true), new(true, true, true, true, true), new(true, true, true),
            new(TerminalAttackEntry: true, NoDefendSupport: true, FreshOrdinarySelectionDefaults: true), Decision);
    }
    private static SwordAiSelectionResult Evaluate(Frame f) => SwordAiSelection.Evaluate(f.State, f.Scenario,
        f.Weapon, f.Candidate, f.Score, f.Comparison, f.Baseline, f.Scan, f.Composition, f.Profile, f.Rng);

    [Theory]
    [InlineData(89, SwordAiSelectionReason.FirstRoll, 3655513600u, 14ul, 1)]
    [InlineData(90, SwordAiSelectionReason.AllRollsFailedUseFirst, 2937909499u, 21ul, 8)]
    public void Partial_hit_lottery_uses_modulo_and_strict_equality_but_both_paths_select_the_same_Attack(
        int evade, SwordAiSelectionReason reason, uint word, ulong count, int rolls)
    {
        var f = Setup(State(evade: evade)); var before = Snapshot(f.State); var result = Evaluate(f); var o = Success(result);
        Assert.Equal(new FftRandomState(2516284547, 13), o.LotteryEntryRng);
        Assert.Equal(new FftRandomState(word, count), result.RngState);
        Assert.Equal(reason, o.Reason); Assert.Equal(0, o.SelectedRankedIndex);
        Assert.Equal(rolls, o.Lottery.Length); Assert.Equal(Enumerable.Range(0, rolls), o.Lottery.Select(r => r.Slot));
        Assert.Equal(new SwordAiLotteryRoll(0, 23010, 10, 100 - evade, evade == 89), o.Lottery[0]);
        Assert.All(o.Lottery.Skip(1), r => { Assert.Equal(0, r.HitPercent); Assert.False(r.Passed); });
        Assert.Equal(SwordAiSelectedPlanKind.Attack, o.Plan.Kind); Assert.True(o.Plan.ActBeforeMove);
        Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, o.Plan.WaitFacingHint);
        Assert.Equal(A, o.Plan.ActorId); Assert.Equal(Actor, o.Plan.Destination);
        var attack = Assert.IsType<SwordAiScannedCandidate>(o.Plan.Attack);
        Assert.Equal(o.Composition.RankedActions[0], attack);
        Assert.Equal((A, B, Actor, SwordAiCandidateOrder.ActThenMove),
            (attack.Candidate.ActorId, attack.Candidate.TargetId, attack.Candidate.Destination, attack.Candidate.Order));
        Assert.Equal(new TilePosition(1, 1, 0), attack.TargetPanel);
        Assert.Equal(64, attack.Evaluation.Score!.Priority); Assert.Equal(100 - evade, attack.Evaluation.Score.HitPercent);
        Assert.Equal(25, attack.Evaluation.Projection!.Outcome!.NormalDamage);
        Assert.Equal(o.LotteryEntryRng, attack.Evaluation.RngState); // No second projection or hit resolution after lottery.
        Assert.Equal(before, Snapshot(f.State)); Assert.Equal(50, f.State.Combatants.Single(u => u.UnitId == B).Hp);
    }

    [Theory] [InlineData("inactive", 3210001534u, 16ul)] [InlineData("no-origins", 3210001534u, 16ul)]
    [InlineData("equal-score", 2937909499u, 21ul)] [InlineData("not-valuable", 595337866u, 20ul)]
    public void Empty_ranking_always_visits_eight_slots_and_returns_MoveOnly_without_a_scored_Attack(string kind, uint word, ulong count)
    {
        var f = Setup(kind == "no-origins" ? State(origin: new(3, 2, 0)) : State(evade: kind == "not-valuable" ? 100 : 0));
        if (kind == "inactive") f = f with { Scan = f.Scan with { TargetActive = false } };
        if (kind == "equal-score") f = f with { Weapon = new(Weapon, 0) };
        var result = Evaluate(f); var o = Success(result);
        Assert.Equal(new FftRandomState(word, count), result.RngState);
        Assert.Equal(SwordAiSelectionReason.MovementFallback, o.Reason); Assert.Null(o.SelectedRankedIndex);
        Assert.Equal(8, o.Lottery.Length); Assert.All(o.Lottery, r => { Assert.Equal(0, r.HitPercent); Assert.False(r.Passed); });
        Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Plan.Kind); Assert.Null(o.Plan.Attack); Assert.False(o.Plan.ActBeforeMove);
        Assert.Equal(f.Scenario.BaselineDestination, o.Plan.Destination); Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, o.Plan.WaitFacingHint);
        Assert.Equal(o.LotteryEntryRng.DrawCount + 8, result.RngState.DrawCount);
        if (kind == "equal-score")
        {
            Assert.Equal(SwordAiAdmission.NotAboveBaseline, o.Composition.Admission);
            Assert.Equal(0, o.Composition.Scan.Outcome!.Candidates[0].Evaluation.Score!.Priority);
        }
        else Assert.Equal(SwordAiAdmission.NoScoredCandidate, o.Composition.Admission);
    }

    [Fact]
    public void Movement_fallback_keeps_D0_when_D1_differs_and_does_not_advance_clock_or_live_choices()
    {
        var f = Setup(State(moved: false)) with { Scenario = new([Actor, Bottom], Bottom, Actor) };
        f = f with { Scan = f.Scan with { TargetActive = false } }; var before = Snapshot(f.State);
        var result = Evaluate(f); var o = Success(result);
        Assert.Equal(Bottom, o.Plan.Destination); Assert.NotEqual(f.Scenario.AttackDestination, o.Plan.Destination);
        Assert.Equal(17ul, result.RngState.DrawCount); // Two movement draws plus eight lottery draws; fallback adds none.
        Assert.All(o.Composition.Movement, m => Assert.Equal(21ul, m.Outcome.Ticks));
        Assert.Equal(before, Snapshot(f.State));
    }

    [Theory] [InlineData(false, -116)] [InlineData(true, 0)]
    public void Improving_negative_and_zero_priority_Attacks_are_selected_as_real_actions(bool zero, int expected)
    {
        var o = Success(Evaluate(Setup(State(evade: 0, hp: 10, maxHp: 100, targetHp: zero ? 20 : 100, targetMaxHp: 100, pa: zero ? 2 : 5))));
        Assert.Equal(SwordAiSelectedPlanKind.Attack, o.Plan.Kind); Assert.Equal(expected, o.Plan.Attack!.Evaluation.Score!.Priority);
        Assert.True(o.Composition.Baseline.Outcome.Score.Priority < expected);
    }

    [Fact]
    public void A_selected_moved_origin_retains_MoveThenAct_and_its_fresh_target_payload()
    {
        var f = Setup(State(moved: false, evade: 0)) with { Scenario = new([Top], Top, Top) };
        var o = Success(Evaluate(f)); Assert.Equal(SwordAiSelectedPlanKind.Attack, o.Plan.Kind);
        Assert.False(o.Plan.ActBeforeMove); Assert.Equal(SwordAiCandidateOrder.MoveThenAct, o.Plan.Attack!.Candidate.Order);
        Assert.Equal(Top, o.Plan.Destination); Assert.Equal(B, o.Plan.Attack.Candidate.TargetId);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [Trait("Coverage", "CurrentAdmission")]
    public void Every_new_source_fact_is_required_before_even_a_certain_Attack_can_be_composed(int missing)
    {
        var f = Setup(State(evade: 0)) with { Rng = new(1, ulong.MaxValue) };
        f = f with { Profile = missing switch
        {
            0 => f.Profile with { TerminalAttackEntry = false }, 1 => f.Profile with { NoDefendSupport = false },
            _ => f.Profile with { FreshOrdinarySelectionDefaults = false }
        } };
        var failure = Rejected(f, SwordAiSelectionStage.Admission);
        Assert.Equal(SwordAiSelectionRejection.UnsupportedProfile, failure.Reason); Assert.Null(failure.CompositionFailure);
    }

    [Theory] [InlineData("admission")] [InlineData("movement")] [InlineData("attack")]
    public void Composition_failures_propagate_once_without_partial_diagnostics_or_lottery_work(string stage)
    {
        var f = Setup(); f = stage switch
        {
            "admission" => f with { Weapon = null }, "movement" => f with { Rng = new(1, ulong.MaxValue) },
            _ => f with { Rng = new(1, ulong.MaxValue - 5) }
        };
        var failure = Rejected(f, SwordAiSelectionStage.Composition);
        Assert.Equal(SwordAiSelectionRejection.CompositionRejected, failure.Reason); Assert.Null(failure.LotteryIndex);
        var nested = Assert.IsType<SwordAiCompositionFailure>(failure.CompositionFailure);
        Assert.Equal(stage switch { "admission" => SwordAiCompositionStage.Admission, "movement" => SwordAiCompositionStage.Movement,
            _ => SwordAiCompositionStage.Attack }, nested.Stage);
        if (stage == "movement") Assert.Equal(SwordAiMovementBaselineRejection.RandomDrawCountExhausted, nested.MovementReason);
        if (stage == "attack") Assert.Equal(SwordAiCandidateRejection.RandomDrawCountExhausted, nested.ProjectionReason);
    }

    [Theory] [InlineData(6, 0)] [InlineData(7, 1)] [InlineData(9, 3)] [InlineData(13, 7)]
    public void Late_lottery_exhaustion_restores_the_entire_query_RNG_and_identifies_the_failed_slot(int available, int slot)
    {
        var f = Setup(State(evade: 90)) with { Rng = new(1, ulong.MaxValue - (ulong)available) };
        var failure = Rejected(f, SwordAiSelectionStage.Lottery);
        Assert.Equal(SwordAiSelectionRejection.RandomDrawCountExhausted, failure.Reason);
        Assert.Equal(slot, failure.LotteryIndex); Assert.Null(failure.CompositionFailure);
    }

    [Theory] [InlineData(0, false, 7, 1)] [InlineData(89, false, 7, 1)]
    [InlineData(90, false, 14, 8)] [InlineData(0, true, 9, 8)]
    public void Exact_MAX_completion_reserves_only_the_composition_and_lottery_draws_actually_needed(int evade, bool inactive, int needed, int rolls)
    {
        var f = Setup(State(evade: evade)); f = f with { Rng = new(1, ulong.MaxValue - (ulong)needed), Scan = f.Scan with { TargetActive = !inactive } };
        var result = Evaluate(f); Assert.Equal(ulong.MaxValue, result.RngState.DrawCount); Assert.Equal(rolls, Success(result).Lottery.Length);
    }

    [Fact]
    public void Selection_needs_no_extra_tick_at_MAX_and_leaves_facing_deferred()
    {
        var f = Setup(State(targetCt: 254, ticks: ulong.MaxValue)); var before = Snapshot(f.State);
        var o = Success(Evaluate(f)); Assert.Equal(0ul, o.Composition.Movement[0].Outcome.ElapsedTicks);
        Assert.Equal(ulong.MaxValue, o.Plan.Attack!.Evaluation.Projection!.Outcome!.Ticks);
        Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, o.Plan.WaitFacingHint); Assert.Equal(before, Snapshot(f.State));
    }

    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Contract", "QueryIsolation")]
    public void Identity_collection_order_replay_and_carried_stream_preserve_live_state_and_profiles(bool second)
    {
        var f = Setup(State(second: second, moved: false, evade: 0), second) with { Scenario = new([Bottom, Actor], Bottom, Actor) };
        var before = Snapshot(f.State); var result = Evaluate(f); Success(result);
        var reversed = Setup(State(second: second, moved: false, evade: 0, reverse: true), second) with
        {
            Scenario = new(f.Scenario.MovementOrigins.Reverse(), Bottom, Actor),
            Candidate = new(new(Ground), new(Ground, A, B), Candidate.Units.Reverse()), Score = new(Score.Units.Reverse())
        };
        Assert.Equal(Serialize(result), Serialize(Evaluate(reversed))); Assert.Equal(Serialize(result), Serialize(Evaluate(f)));
        Assert.Equal(result.RngState.DrawCount + 13, Evaluate(f with { Rng = result.RngState }).RngState.DrawCount);
        Assert.Equal(before, Snapshot(f.State)); Assert.Equal(LiveRng, f.State.TurnBattle.Turns.RngState);
        Assert.Equal(second ? B : A, result.Outcome!.Plan.ActorId);
    }

    private static SwordAiSelectionOutcome Success(SwordAiSelectionResult result)
    { Assert.True(result.Accepted); Assert.Null(result.Failure); return Assert.IsType<SwordAiSelectionOutcome>(result.Outcome); }
    private static SwordAiSelectionFailure Rejected(Frame f, SwordAiSelectionStage stage)
    {
        var before = Snapshot(f.State); var result = Evaluate(f);
        Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(f.Rng, result.RngState); Assert.Equal(before, Snapshot(f.State));
        var failure = Assert.IsType<SwordAiSelectionFailure>(result.Failure); Assert.Equal(stage, failure.Stage); return failure;
    }
    private static string Serialize(object value) => JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot state) => Serialize(new { state.Combatants, state.TurnBattle.Turns,
        state.TurnBattle.Battle.Map, state.TurnBattle.Battle.RngState,
        Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved, u.MovementProfile.Move }) });
    private static AttackBattleSnapshot State(int evade = 89, bool moved = true, TilePosition? origin = null,
        int hp = 50, int maxHp = 50, int targetHp = 50, int targetMaxHp = 50, int pa = 5,
        int targetCt = 95, bool second = false, bool reverse = false, ulong ticks = 20)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var movement = new UnitMovementProfile(4, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, origin ?? Actor, movement, moved), new(target, new(1, 1, 0), movement) };
        var turns = new TurnUnitState[] { new(actor, 5, 15, 2, true, moved), new(target, 5, targetCt, 17) };
        AttackUnitState Combat(BattleUnitId id, int current, int maximum, BattleFacing facing, int evasion) =>
            new(id, current, maximum, 0, 0, pa, evasion, 0, 0, 0, 70, 0, facing,
                current <= maximum / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, hp, maxHp, BattleFacing.West, 0), Combat(target, targetHp, targetMaxHp, BattleFacing.East, evade) };
        return new(new(new(new(4, 3, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 12).ToArray()), reverse ? bodies.Reverse() : bodies, LiveRng),
            new(reverse ? turns.Reverse() : turns, LiveRng, ticks, TurnCyclePhase.UnitTurn)), reverse ? combat.Reverse() : combat);
    }
}
