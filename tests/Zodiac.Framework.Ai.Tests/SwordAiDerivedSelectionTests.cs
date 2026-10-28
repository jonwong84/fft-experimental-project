using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiDerivedSelectionTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly FftRandomState Decision = new(1, 7), Live = new(57, 100);
    private static readonly SwordAiCandidateProfile Candidate = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly SwordAiScoreProfile Score = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
    private static readonly SwordAiDerivedSelectionProfile Derived = new(true, true, true, true, true, true, true, FirstAttackEntry: true);
    private static readonly SwordAiSelectionProfile Selection = new(true, true, true);
    private static readonly SwordAiMovementScenarioProfile Scenario = new(true, true, true);

    private sealed record Frame(AttackBattleSnapshot State, BoundedSwordWeapon? Weapon, SwordAiComparisonProfile Comparison,
        SwordAiDerivedSelectionProfile Derived, SwordAiSelectionProfile Selection, SwordAiMovementScenarioProfile Scenario, FftRandomState Rng);
    private static Frame Setup(AttackBattleSnapshot? state = null, bool second = false) => new(state ?? State(), new(Weapon, 5),
        new(second ? B : A, second ? A : B, true), Derived, Selection, Scenario, Decision);
    private static SwordAiDerivedSelectionResult Evaluate(Frame f) => SwordAiDerivedSelection.Evaluate(f.State, f.Weapon,
        Candidate, Score, f.Comparison, f.Scenario, f.Derived, f.Selection, f.Rng);

    [Fact]
    public void Fresh_non_adjacent_scenario_reuses_movement_scores_and_matches_independently_supplied_source_inputs()
    {
        var f = Setup(State(origin: new(3, 1, 0), moved: false, evade: 0)); var result = Evaluate(f); var o = Success(result);
        TilePosition[] cells = [new(3, 0, 0), new(2, 1, 0), new(3, 1, 0), new(4, 1, 0), new(3, 2, 0)];
        Assert.Equal(cells, o.MovementScenario.Scenario.MovementOrigins);
        Assert.Equal(new TilePosition(2, 1, 0), o.MovementScenario.Scenario.BaselineDestination);
        Assert.Equal(new TilePosition(3, 2, 0), o.MovementScenario.Scenario.AttackDestination);
        var composition = o.Selection.Composition;
        Assert.Equal(cells, composition.AttackScenario.Origins);
        Assert.Equal(5, composition.Movement.Length);
        for (int i = 0; i < cells.Length; i++) Assert.Same(o.MovementScenario.Movement[i], composition.Movement[i]);
        Assert.Equal(12ul, composition.Movement[^1].RngState.DrawCount);
        Assert.Equal(17ul, o.Selection.LotteryEntryRng.DrawCount); Assert.Equal(18ul, result.RngState.DrawCount);
        Assert.Equal(SwordAiSelectedPlanKind.Attack, o.Selection.Plan.Kind); Assert.False(o.Selection.Plan.ActBeforeMove);
        Assert.Equal(new TilePosition(2, 1, 0), o.Selection.Plan.Destination);
        Assert.Equal(64, o.Selection.Plan.Attack!.Evaluation.Score!.Priority);
        Assert.Single(composition.Scan.Outcome!.Candidates); Assert.Single(o.Selection.Lottery);
        var supplied = SwordAiSelection.Evaluate(f.State, new(cells, new(2, 1, 0), new(3, 2, 0)), f.Weapon,
            Candidate, Score, f.Comparison, new(true, true, true, true), new(true, true, true, true, true),
            new(true, true, true), Selection, Decision);
        Assert.True(supplied.Accepted); Assert.Equal(Serialize(supplied.Outcome), Serialize(o.Selection));
        Assert.Equal(supplied.RngState, result.RngState);
    }

    [Theory] [InlineData(89, SwordAiSelectionReason.FirstRoll, 3655513600u, 14ul, 1)]
    [InlineData(90, SwordAiSelectionReason.AllRollsFailedUseFirst, 2937909499u, 21ul, 8)]
    public void Derived_singleton_keeps_exact_partial_hit_lottery_without_rescoring(int evade, SwordAiSelectionReason reason,
        uint word, ulong count, int rolls)
    {
        var f = Setup(State(evade: evade)); var result = Evaluate(f); var o = Success(result).Selection;
        Assert.Equal(new FftRandomState(2516284547, 13), o.LotteryEntryRng);
        Assert.Equal(new FftRandomState(word, count), result.RngState);
        Assert.Equal(reason, o.Reason); Assert.Equal(rolls, o.Lottery.Length);
        Assert.Equal(new SwordAiLotteryRoll(0, 23010, 10, 100 - evade, evade == 89), o.Lottery[0]);
        Assert.Equal(SwordAiSelectedPlanKind.Attack, o.Plan.Kind); Assert.True(o.Plan.ActBeforeMove);
        Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, o.Plan.WaitFacingHint);
        Assert.Equal(50, f.State.Combatants.Single(u => u.UnitId == B).Hp);
    }

    [Theory] [InlineData(false, 20ul, SwordAiAdmission.NoScoredCandidate)] [InlineData(true, 25ul, SwordAiAdmission.NotAboveBaseline)]
    public void Empty_ranking_uses_derived_D0_instead_of_D1(bool zeroPower, ulong count, SwordAiAdmission admission)
    {
        var f = Setup(State(origin: new(3, 1, 0), moved: false, evade: 0));
        f = f with { Derived = Derived with { TargetActive = zeroPower }, Weapon = new(Weapon, zeroPower ? 0 : 5) };
        var result = Evaluate(f); var o = Success(result);
        Assert.Equal(admission, o.Selection.Composition.Admission); Assert.Equal(count, result.RngState.DrawCount);
        Assert.Equal(SwordAiSelectionReason.MovementFallback, o.Selection.Reason);
        Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Selection.Plan.Kind); Assert.Null(o.Selection.Plan.Attack);
        Assert.Equal(new TilePosition(2, 1, 0), o.Selection.Plan.Destination);
        Assert.NotEqual(o.MovementScenario.Scenario.AttackDestination, o.Selection.Plan.Destination);
        Assert.Equal(8, o.Selection.Lottery.Length); Assert.All(o.Selection.Lottery, r => Assert.Equal(0, r.HitPercent));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Remaining_facts_and_weapon_are_preflighted_before_even_inactive_target_movement()
    {
        var f = Setup() with { Rng = new(1, ulong.MaxValue), Derived = Derived with { TargetActive = false } };
        SwordAiDerivedSelectionProfile[] invalid =
        [ f.Derived with { NormalScoring = false }, f.Derived with { CanonicalCoordinates = false },
          f.Derived with { OrdinaryAttackTilePolicy = false }, f.Derived with { TargetSelectionMatchesBaseline = false },
          f.Derived with { FreshAbilityBestReset = false }, f.Derived with { FreshEmptyRankedList = false },
          f.Derived with { FirstAttackEntry = false } ];
        foreach (var profile in invalid) Rejected(f with { Derived = profile }, SwordAiDerivedSelectionStage.Admission);
        foreach (var profile in new[] { Selection with { TerminalAttackEntry = false }, Selection with { NoDefendSupport = false },
            Selection with { FreshOrdinarySelectionDefaults = false } })
            Rejected(f with { Selection = profile }, SwordAiDerivedSelectionStage.Admission);
        var failure = Rejected(f with { Weapon = null }, SwordAiDerivedSelectionStage.Admission);
        Assert.Equal(SwordAiCandidateRejection.UnsupportedProfile, failure.AdmissionFailure!.ProjectionRejection);
    }

    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Coverage", "CurrentAdmission")]
    public void Unsupported_scenario_branch_rejects_without_a_reach_roll(bool missingFact)
    {
        var f = Setup(State(origin: new(4, 1, 0), moved: false)) with { Rng = new(1, ulong.MaxValue) };
        if (missingFact) f = f with { Scenario = Scenario with { OrdinaryScenarioPolicy = false } };
        var failure = Rejected(f, SwordAiDerivedSelectionStage.MovementScenario);
        Assert.Equal(missingFact ? SwordAiMovementScenarioRejection.UnsupportedProfile : SwordAiMovementScenarioRejection.OutOfReach,
            failure.ScenarioFailure!.Reason);
    }

    [Fact]
    public void Late_movement_and_Attack_exhaustion_restore_the_entire_query_stream()
    {
        var f = Setup(State(origin: new(3, 1, 0), moved: false, evade: 0));
        var movement = Rejected(f with { Rng = new(1, ulong.MaxValue - 3) }, SwordAiDerivedSelectionStage.MovementScenario);
        Assert.Equal(3, movement.ScenarioFailure!.Index);
        var attack = Rejected(f with { Rng = new(1, ulong.MaxValue - 7) }, SwordAiDerivedSelectionStage.Composition);
        Assert.Equal(SwordAiCompositionStage.Attack, attack.CompositionFailure!.Stage);
        Assert.Equal(0, attack.CompositionFailure.Index);
        Assert.Equal(SwordAiCandidateRejection.RandomDrawCountExhausted, attack.CompositionFailure.ProjectionReason);
    }

    [Theory] [InlineData(6, 0)] [InlineData(13, 7)]
    public void Late_lottery_exhaustion_discards_the_fresh_scenario_as_well_as_the_composition(int available, int slot)
    {
        var f = Setup(State(evade: 90)) with { Rng = new(1, ulong.MaxValue - (ulong)available) };
        var failure = Rejected(f, SwordAiDerivedSelectionStage.Lottery);
        Assert.Equal(slot, failure.SelectionFailure!.LotteryIndex);
        Assert.Equal(SwordAiSelectionRejection.RandomDrawCountExhausted, failure.SelectionFailure.Reason);
    }

    [Theory] [InlineData(89, 7)] [InlineData(90, 14)]
    public void Exact_actual_draw_budget_succeeds_at_MAX_for_one_or_eight_rolls(int evade, int cost)
    {
        var result = Evaluate(Setup(State(evade: evade)) with { Rng = new(1, ulong.MaxValue - (ulong)cost) });
        Success(result); Assert.Equal(ulong.MaxValue, result.RngState.DrawCount);
    }

    [Fact]
    public void Already_ready_target_needs_no_tick_reservation_for_any_stage()
    {
        var o = Success(Evaluate(Setup(State(targetCt: 100, ticks: ulong.MaxValue))));
        Assert.All(o.MovementScenario.Movement, m => Assert.Equal(ulong.MaxValue, m.Outcome.Ticks));
        Assert.Equal(ulong.MaxValue, o.Selection.Plan.Attack!.Evaluation.Projection!.Outcome!.Ticks);
    }

    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Contract", "QueryIsolation")]
    public void Derived_selection_replays_across_order_and_identity_without_changing_live_state(bool second)
    {
        var f = Setup(State(second: second, moved: false, evade: 0, origin: new(3, 1, 0)), second);
        string before = Snapshot(f.State); var result = Evaluate(f); var o = Success(result);
        Assert.Equal(Serialize(result), Serialize(Evaluate(f)));
        Assert.Equal(Serialize(result), Serialize(Evaluate(Setup(State(second: second, moved: false, evade: 0,
            origin: new(3, 1, 0), reverse: true), second))));
        var carried = Evaluate(f with { Rng = result.RngState }); Success(carried);
        Assert.True(carried.RngState.DrawCount > result.RngState.DrawCount);
        Assert.Equal(before, Snapshot(f.State)); Assert.Equal(Live, f.State.TurnBattle.Turns.RngState);
        Assert.Equal(second ? B : A, o.Selection.Plan.ActorId);
    }

    private static SwordAiDerivedSelectionOutcome Success(SwordAiDerivedSelectionResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Null(result.Failure); return Assert.IsType<SwordAiDerivedSelectionOutcome>(result.Outcome); }
    private static SwordAiDerivedSelectionFailure Rejected(Frame f, SwordAiDerivedSelectionStage stage)
    {
        string before = Snapshot(f.State); var result = Evaluate(f);
        Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(f.Rng, result.RngState);
        Assert.Equal(before, Snapshot(f.State)); Assert.Equal(stage, result.Failure!.Stage); return result.Failure;
    }
    private static string Serialize(object? value) => JsonSerializer.Serialize(value);
    private static string Snapshot(AttackBattleSnapshot state) => Serialize(new { state.Combatants, state.TurnBattle.Turns,
        state.TurnBattle.Battle.RngState, Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }) });
    private static AttackBattleSnapshot State(TilePosition? origin = null, bool moved = true, int evade = 89,
        int targetCt = 95, ulong ticks = 20, bool second = false, bool reverse = false)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var movement = new UnitMovementProfile(1, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, origin ?? new(2, 1, 0), movement, moved), new(target, new(1, 1, 0), movement) };
        var turns = new TurnUnitState[] { new(actor, 5, 15, 2, true, moved), new(target, 5, targetCt, 17) };
        AttackUnitState Combat(BattleUnitId id, int evasion) => new(id, 50, 50, 0, 0, 5, evasion, 0, 0, 0, 70, 0,
            BattleFacing.East, AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, 0), Combat(target, evade) };
        return new(new(new(new(5, 3, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 15).ToArray()), reverse ? bodies.Reverse() : bodies, Live),
            new(reverse ? turns.Reverse() : turns, Live, ticks, TurnCyclePhase.UnitTurn)), reverse ? combat.Reverse() : combat);
    }
}
