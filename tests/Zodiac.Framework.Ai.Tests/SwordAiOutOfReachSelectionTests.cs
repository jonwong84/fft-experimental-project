using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiOutOfReachSelectionTests
{
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly FftRandomState Decision = new(1, 7), Live = new(57, 100);
    private static readonly SwordAiCandidateProfile Candidate = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    private static readonly SwordAiScoreProfile Score = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
    private static readonly SwordAiMovementScenarioProfile Scenario = new(true, true, true);
    private static readonly SwordAiDerivedSelectionProfile Derived = new(true, true, true, true, true, true, true, FirstAttackEntry: true);
    private static readonly SwordAiSelectionProfile Selection = new(true, true, true);
    private sealed record Frame(AttackBattleSnapshot State, BoundedSwordWeapon? Weapon, SwordAiScenarioTeam Team,
        SwordAiComparisonProfile Comparison, SwordAiDerivedSelectionProfile Derived, SwordAiSelectionProfile Selection, FftRandomState Rng);
    private static Frame Setup(AttackBattleSnapshot? state = null, bool second = false) => new(state ?? State(), new(Weapon, 5),
        SwordAiScenarioTeam.NonBlue, new(second ? B : A, second ? A : B, true), Derived, Selection, Decision);
    private static SwordAiOutOfReachSelectionResult Evaluate(Frame f) => SwordAiDerivedSelection.EvaluateOutOfReach(f.State,
        f.Weapon, Candidate, Score, f.Comparison, Scenario, f.Derived, f.Selection, f.Team, f.Rng);

    [Theory]
    [InlineData(SwordAiScenarioTeam.Blue, 1u, 5, 180171308u, 18ul)]
    [InlineData(SwordAiScenarioTeam.NonBlue, 1u, 5, 836760821u, 19ul)]
    [InlineData(SwordAiScenarioTeam.Blue, 19u, 5, 1844253734u, 18ul)]
    [InlineData(SwordAiScenarioTeam.NonBlue, 19u, 3, 1165580391u, 19ul)]
    public void Source_vectors_reuse_only_S_scores_carry_adjustment_and_visit_eight_empty_slots(
        SwordAiScenarioTeam team, uint seed, int destination, uint word, ulong count)
    {
        var f = Setup() with { Team = team, Rng = new(seed, 7) }; var result = Evaluate(f); var o = Success(result);
        var m = o.MovementScenario.MovementScenario; var c = o.Selection.Composition;
        Assert.Equal(new[] { new TilePosition(3, 0, 0), new(4, 0, 0), new(5, 0, 0) }, m.Scenario.MovementOrigins);
        Assert.Equal(new[] { new TilePosition(3, 0, 0), new(4, 0, 0), new(5, 0, 0), new(6, 0, 0) }, c.AttackScenario.Origins);
        Assert.Equal(3, c.Movement.Length);
        for (int i = 0; i < 3; i++) Assert.Same(m.Movement[i], c.Movement[i]);
        Assert.DoesNotContain(c.Movement, cell => cell.Outcome.Destination == new TilePosition(6, 0, 0));
        Assert.Equal(SwordAiScanDisposition.NoCandidates, c.Scan.Disposition); Assert.Empty(c.Scan.Outcome!.Candidates);
        Assert.Equal(SwordAiAdmission.NoScoredCandidate, c.Admission); Assert.All(c.RankedActions, Assert.Null);
        Assert.Equal(8, o.Selection.Lottery.Length); Assert.Equal(Enumerable.Range(0, 8), o.Selection.Lottery.Select(r => r.Slot));
        Assert.All(o.Selection.Lottery, r => { Assert.Equal(0, r.HitPercent); Assert.False(r.Passed); });
        Assert.Equal(SwordAiSelectionReason.MovementFallback, o.Selection.Reason); Assert.Null(o.Selection.SelectedRankedIndex);
        Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Selection.Plan.Kind); Assert.Null(o.Selection.Plan.Attack);
        Assert.Equal(new TilePosition(destination, 0, 0), o.Selection.Plan.Destination);
        Assert.Equal(m.Scenario.BaselineDestination, o.Selection.Plan.Destination);
        Assert.Equal(SwordAiWaitFacingHint.ChooseAtWait, o.Selection.Plan.WaitFacingHint);
        Assert.Equal(new FftRandomState(word, count), result.RngState);
        Assert.Equal(team == SwordAiScenarioTeam.Blue ? 10ul : 11ul, o.Selection.LotteryEntryRng.DrawCount);
        if (team == SwordAiScenarioTeam.Blue) Assert.Equal(c.Movement[^1].RngState, o.Selection.LotteryEntryRng);
        else
        {
            Assert.NotEqual(c.Movement[^1].RngState, o.Selection.LotteryEntryRng);
            Assert.Equal(seed == 19 ? new SwordAiReachAdjustment(224, 0, 4, 2) : new(17515, 1, 4, 4), o.MovementScenario.ReachAdjustment);
        }
    }

    [Theory] [InlineData(SwordAiScenarioTeam.Blue, 16ul)] [InlineData(SwordAiScenarioTeam.NonBlue, 17ul)]
    public void Inactive_target_and_no_match_singleton_keep_the_real_prefix_and_lottery_schedule(SwordAiScenarioTeam team, ulong count)
    {
        var f = Setup(State(origin: new(3, 0, 0), move: 0)) with { Team = team, Derived = Derived with { TargetActive = false } };
        var result = Evaluate(f); var o = Success(result);
        Assert.Single(o.Selection.Composition.Movement); Assert.True(o.MovementScenario.AttackUsedDistanceFallback);
        Assert.Equal(count, result.RngState.DrawCount); Assert.Equal(8, o.Selection.Lottery.Length);
        Assert.Equal(new TilePosition(3, 0, 0), o.Selection.Plan.Destination);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Evaluate(f with { Derived = Derived })));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Omitted_first_entry_fact_rejects_both_derived_APIs_before_movement_even_when_inactive()
    {
        var profile = new SwordAiDerivedSelectionProfile(false, true, true, true, true, true, true);
        Assert.False(profile.FirstAttackEntry);
        var f = Setup() with { Derived = profile, Rng = new(1, ulong.MaxValue) };
        var failure = Rejected(f, SwordAiDerivedSelectionStage.Admission);
        Assert.Equal(SwordAiDerivedSelectionRejection.UnsupportedProfile, failure.Reason);
        var state = State(origin: new(4, 0, 0));
        var inside = SwordAiDerivedSelection.Evaluate(state, f.Weapon, Candidate, Score, f.Comparison, Scenario, profile, Selection, f.Rng);
        Assert.False(inside.Accepted); Assert.Null(inside.Outcome); Assert.Equal(f.Rng, inside.RngState);
        Assert.Equal(SwordAiDerivedSelectionStage.Admission, inside.Failure!.Stage);
    }

    [Theory] [InlineData("weapon")] [InlineData("team")] [InlineData("branch")] [InlineData("defend")]
    [Trait("Coverage", "CurrentAdmission")]
    public void Required_weapon_team_branch_and_support_context_reject_without_prefix_work(string invalid)
    {
        var f = Setup() with { Rng = new(1, ulong.MaxValue), Derived = Derived with { TargetActive = false } };
        f = invalid switch
        {
            "weapon" => f with { Weapon = null }, "team" => f with { Team = SwordAiScenarioTeam.Unknown },
            "branch" => f with { State = State(origin: new(5, 0, 0)) },
            _ => f with { Selection = Selection with { NoDefendSupport = false } }
        };
        var failure = Rejected(f, invalid is "team" or "branch" ? SwordAiDerivedSelectionStage.MovementScenario : SwordAiDerivedSelectionStage.Admission);
        if (invalid == "weapon") Assert.Equal(SwordAiCandidateRejection.UnsupportedProfile, failure.AdmissionFailure!.ProjectionRejection);
        if (invalid == "branch") Assert.Equal(SwordAiMovementScenarioRejection.WrongBranch, failure.ScenarioFailure!.Reason);
    }

    [Theory] [InlineData("movement", 2)] [InlineData("adjustment", 3)] [InlineData("lottery-first", 4)] [InlineData("lottery-last", 11)]
    public void Late_failures_restore_whole_query_RNG_without_partial_scenario_or_plan(string stage, int available)
    {
        var f = Setup() with { Rng = new(1, ulong.MaxValue - (ulong)available) };
        var failure = Rejected(f, stage.StartsWith("lottery", StringComparison.Ordinal)
            ? SwordAiDerivedSelectionStage.Lottery : SwordAiDerivedSelectionStage.MovementScenario);
        if (stage == "movement") { Assert.Equal(SwordAiMovementScenarioStage.Movement, failure.ScenarioFailure!.Stage); Assert.Equal(2, failure.ScenarioFailure.Index); }
        else if (stage == "adjustment") Assert.Equal(SwordAiMovementScenarioStage.ReachAdjustment, failure.ScenarioFailure!.Stage);
        else Assert.Equal(stage == "lottery-first" ? 0 : 7, failure.SelectionFailure!.LotteryIndex);
    }

    [Theory] [InlineData(SwordAiScenarioTeam.Blue, 11)] [InlineData(SwordAiScenarioTeam.NonBlue, 12)]
    public void Exact_actual_draw_budget_completes_at_MAX_without_speculative_formula_draws(SwordAiScenarioTeam team, int cost)
    {
        var result = Evaluate(Setup() with { Team = team, Rng = new(1, ulong.MaxValue - (ulong)cost) }); Success(result);
        Assert.Equal(ulong.MaxValue, result.RngState.DrawCount);
    }

    [Fact]
    public void Adjustment_and_lottery_need_no_ticks_after_original_per_cell_clock_completion()
    {
        var o = Success(Evaluate(Setup(State(ticks: ulong.MaxValue - 1))));
        Assert.All(o.Selection.Composition.Movement, cell => Assert.Equal(ulong.MaxValue, cell.Outcome.Ticks));
        Assert.Equal(SwordAiSelectedPlanKind.MoveOnly, o.Selection.Plan.Kind);
    }

    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Contract", "QueryIsolation")]
    public void Original_state_replay_order_identity_and_explicit_stream_carry_are_preserved(bool second)
    {
        var f = Setup(State(second: second), second); var before = Snapshot(f.State);
        var result = Evaluate(f); var o = Success(result);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Evaluate(f)));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Evaluate(Setup(State(second: second, reverse: true), second))));
        var carried = Evaluate(f with { Rng = result.RngState }); Success(carried);
        Assert.Equal(result.RngState.DrawCount + 12, carried.RngState.DrawCount);
        Assert.Equal(before, Snapshot(f.State)); Assert.Equal(Live, f.State.TurnBattle.Turns.RngState);
        Assert.Equal(second ? B : A, o.Selection.Plan.ActorId);
    }

    private static SwordAiOutOfReachSelectionOutcome Success(SwordAiOutOfReachSelectionResult result)
    { Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Null(result.Failure); return Assert.IsType<SwordAiOutOfReachSelectionOutcome>(result.Outcome); }
    private static SwordAiDerivedSelectionFailure Rejected(Frame f, SwordAiDerivedSelectionStage stage)
    {
        var before = Snapshot(f.State); var result = Evaluate(f);
        Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(f.Rng, result.RngState);
        Assert.Equal(before, Snapshot(f.State)); Assert.Equal(stage, result.Failure!.Stage); return result.Failure;
    }
    private static string Snapshot(AttackBattleSnapshot state) => JsonSerializer.Serialize(new { state.Combatants, state.TurnBattle.Turns,
        Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }) });
    private static AttackBattleSnapshot State(TilePosition? origin = null, int move = 3, ulong ticks = 20, bool second = false, bool reverse = false)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var movement = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, origin ?? new(6, 0, 0), movement), new(target, new(1, 0, 0), movement) };
        var turns = new TurnUnitState[] { new(actor, 5, 15, 2, true), new(target, 5, 95, 17) };
        var combat = new[] { actor, target }.Select(id => new AttackUnitState(id, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0,
            BattleFacing.East, AttackUnitStatus.None, 0, Weapon)).ToArray();
        return new(new(new(new(7, 1, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 7).ToArray()), reverse ? bodies.Reverse() : bodies, Live),
            new(reverse ? turns.Reverse() : turns, Live, ticks, TurnCyclePhase.UnitTurn)), reverse ? combat.Reverse() : combat);
    }
}
