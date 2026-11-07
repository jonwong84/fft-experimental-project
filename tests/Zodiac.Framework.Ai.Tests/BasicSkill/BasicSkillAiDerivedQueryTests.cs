using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiDerivedQueryTests
{
    internal static BasicSkillAiDerivedProfile DerivedProfile(SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue) =>
        new(SelfData.Configuration(team), new(true, true, true), true, true, true);
    internal static readonly BasicSkillAiSelectionProfile Selection = new(true, true, true);
    private static BasicSkillAiScenarioResult Scenarios(FacingTurnState state, BasicSkillAiDerivedProfile? profile = null, RandomState? rng = null)
    {
        var p = profile ?? DerivedProfile();
        return BasicSkillAiScenarios.Evaluate(state.Battle, SelfData.Request(), p.Preparation, p.Scenarios, rng ?? state.Battle.TurnBattle.Turns.RngState);
    }
    private static BasicSkillAiDerivedCompositionResult Compose(FacingTurnState state, BasicSkillAiDerivedProfile? profile = null, RandomState? rng = null) =>
        BasicSkillAiDerivedComposition.Evaluate(state.Battle, SelfData.Request(), profile ?? DerivedProfile(), rng ?? state.Battle.TurnBattle.Turns.RngState);
    private static BasicSkillAiDerivedSelectionResult Select(FacingTurnState state, BasicSkillAiDerivedProfile? profile = null, RandomState? rng = null) =>
        BasicSkillAiDerivedSelection.Evaluate(state.Battle, SelfData.Request(), profile ?? DerivedProfile(), Selection, rng ?? state.Battle.TurnBattle.Turns.RngState);

    [Fact]
    public void In_reach_scenarios_derive_ranged_D0_D1_and_one_refresh_plus_each_source_score()
    {
        var state = Ready(); var before = Snapshot(state.Battle); var result = Scenarios(state);
        Assert.True(result.Accepted, result.Failure?.ToString()); var o = result.Outcome!; var m = o.Movement;
        Assert.False(m.OutOfReach); Assert.Equal(14, m.Movement.Length); Assert.Equal(new RandomState(2111915288, 22), result.RngState);
        Assert.Equal(new TilePosition(1, 3, 0), m.BaselineDestination); Assert.Equal(new TilePosition(1, 4, 0), m.ActFirstDestination);
        Assert.Equal(3, m.BaselineDistanceLimit); Assert.Equal(4, m.TargetReach); Assert.Null(m.ReachAdjustment);
        Assert.Equal(m.ScoreMask.ToArray(), m.Scenario1.ToArray()); Assert.Empty(m.Scenario2); Assert.All(m.Cells, c => Assert.True(c.PriorityWritten));
        Assert.Equal(m.BaselineDestination, m.Movement[m.BaselineIndex].Outcome.Destination);
        Assert.All(o.Preparation.Units, u => { Assert.Equal(4, u.InitializedEntryCount); Assert.Equal(1, u.HelpfulCount); });
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Out_of_reach_actor_OR_preserves_unwritten_cells_overlap_and_range_four_rejection()
    {
        foreach (int distance in new[] { 3, 4, 5 })
        {
            var state = Ready(move: 1, target: new(1 + distance, 1, 0)); var result = Scenarios(state);
            Assert.True(result.Accepted); var m = result.Outcome!.Movement;
            Assert.True(m.OutOfReach); Assert.Equal(new RandomState(662824084, 10), result.RngState);
            Assert.Equal(new[] { new TilePosition(2, 1, 0) }, m.ScoreMask.ToArray());
            Assert.Contains(Origin, m.Scenario1); Assert.Contains(Origin, m.Scenario2);
            Assert.Null(m.Cells.Single(c => c.Tile == Origin).WrittenPriority); Assert.Equal((byte)(10 - distance), m.Cells.Single(c => c.Tile == Origin).Proximity);
            Assert.Equal(m.BaselineDestination, m.ActFirstDestination); Assert.Null(m.BaselineDistanceLimit);
            var composition = Compose(state);
            if (distance <= 4)
            {
                Assert.False(composition.Accepted); Assert.Equal(BasicSkillAiDerivedStage.Provenance, composition.Failure!.Stage);
                Assert.Null(composition.Outcome); Assert.Equal(new RandomState(1, 7), composition.RngState);
            }
            else
            {
                Assert.True(composition.Accepted, composition.Failure?.ToString()); Assert.DoesNotContain(Origin, composition.Outcome!.ScanOrigins);
                Assert.Contains(composition.Outcome.Composition.Accumulate.Usefulness.Targets, t => t.UnitId == A && t.Raw.HasValue);
            }
        }
    }

    [Fact]
    public void Move_zero_no_match_fallback_scores_the_actor_and_makes_a_range_four_cast_safe()
    {
        var state = Ready(move: 0, target: new(5, 1, 0), evade: 0); var result = Select(state);
        Assert.True(result.Accepted, result.Failure?.ToString()); var o = result.Outcome!; var m = o.Scenarios.Movement;
        Assert.True(m.FilterUsedReachableFallback); Assert.Single(m.ScoreMask); Assert.Equal(Origin, m.ScoreMask[0]);
        Assert.True(Assert.Single(m.Cells).PriorityWritten); Assert.Empty(m.Scenario2);
        Assert.Equal(BasicSkillAiPlanKind.ThrowStone, o.Selection.Plan.Kind); Assert.True(o.Selection.Plan.ActBeforeMove);
        Assert.Equal(new RandomState(267834847, 17), result.RngState);
    }

    [Fact]
    public void In_reach_owned_composition_matches_the_existing_supplied_entry_without_an_extra_refresh_or_score()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (int move in new[] { 0, 3 })
        foreach (int evade in new[] { 0, 97 })
        {
            var state = Ready(move: move, evade: evade, word: 7); var derived = Compose(state, DerivedProfile(team));
            Assert.True(derived.Accepted, derived.Failure?.ToString()); var o = derived.Outcome!; var m = o.Scenarios.Movement;
            var supplied = BasicSkillAiComposition.Evaluate(state.Battle, SelfData.Request(),
                new(m.ScoreMask, o.ScanOrigins, m.BaselineDestination, m.ActFirstDestination), Profile(team), state.Battle.TurnBattle.Turns.RngState);
            Assert.True(supplied.Accepted); Assert.Equal(Serialize(supplied.Outcome), Serialize(o.Composition)); Assert.Equal(supplied.RngState, derived.RngState);
        }
    }

    [Fact]
    public void Literal_Move_zero_Attack_stream_runs_Attack_then_odd_self_coin_then_Stone_and_one_lottery()
    {
        var state = Ready(move: 0, evade: 0); var result = Select(state); Assert.True(result.Accepted, result.Failure?.ToString());
        var o = result.Outcome!.Selection;
        Assert.Equal(BasicSkillAiPlanKind.Attack, o.Plan.Kind); Assert.Equal(Origin, o.Plan.Destination);
        Assert.Equal(new RandomState(595337866, 20), o.LotteryEntryRng); Assert.Equal(new RandomState(2937909499, 21), result.RngState);
        Assert.Equal((uint)7419, o.Composition.Accumulate.Usefulness.Targets.Single(t => t.UnitId == A).Raw);
        Assert.Empty(o.Composition.Accumulate.Candidates); Assert.Single(o.Lottery); Assert.Single(o.Composition.Movement);
    }

    [Fact]
    public void Useful_self_remains_in_source_order_and_never_boosts_the_later_hypothetical_Stone()
    {
        var state = Ready(move: 0, evade: 97, word: 7); var before = Snapshot(state.Battle); var result = Select(state);
        Assert.True(result.Accepted, result.Failure?.ToString()); var o = result.Outcome!.Selection;
        Assert.Equal(BasicSkillAiPlanKind.Accumulate, o.Plan.Kind); Assert.Equal(new RandomState(929657700, 24), result.RngState);
        var self = Assert.Single(o.Composition.Accumulate.Candidates); Assert.Equal(6, self.Projection!.Outcome!.PhysicalAttackAfter);
        Assert.Equal(10, o.Composition.ThrowStone.Candidate!.Evaluation.Projection!.Outcome!.NormalDamage);
        Assert.Equal((uint)31790, o.Composition.Accumulate.Usefulness.Targets.Single(t => t.UnitId == A).Raw);
        Assert.Equal(3, o.Lottery.Length); Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Safe_out_of_reach_range_five_proves_actor_key_unread_and_preserves_team_specific_draw_order()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        {
            var state = Ready(move: 1, target: new(6, 1, 0), evade: 0); var result = Select(state, DerivedProfile(team));
            Assert.True(result.Accepted, result.Failure?.ToString()); var o = result.Outcome!;
            Assert.Equal(new[] { new TilePosition(2, 1, 0) }, o.ScanOrigins.ToArray()); Assert.Null(o.Scenarios.Movement.Cells.Single(c => c.Tile == Origin).WrittenPriority);
            Assert.Equal(BasicSkillAiPlanKind.ThrowStone, o.Selection.Plan.Kind); Assert.False(o.Selection.Plan.ActBeforeMove);
            Assert.Equal(team == SwordAiScenarioTeam.NonBlue ? new RandomState(267834847, 17) : new RandomState(3210001534, 16), result.RngState);
            if (team == SwordAiScenarioTeam.NonBlue) Assert.Equal((uint)10113, o.Scenarios.Movement.ReachAdjustment!.Raw);
            else Assert.Null(o.Scenarios.Movement.ReachAdjustment);
        }
    }

    [Fact]
    public void Empty_fallback_still_visits_eight_lottery_slots_after_the_owned_scenario_and_skipped_self()
    {
        var result = Select(Ready(move: 0, target: new(6, 1, 0))); Assert.True(result.Accepted, result.Failure?.ToString());
        Assert.Equal(BasicSkillAiPlanKind.MoveOnly, result.Outcome!.Selection.Plan.Kind); Assert.Equal(Origin, result.Outcome.Selection.Plan.Destination);
        Assert.Equal(8, result.Outcome.Selection.Lottery.Length); Assert.Equal(new RandomState(836760821, 19), result.RngState);
    }

    [Fact]
    public void Exact_stage_budgets_fit_and_late_lottery_or_provenance_failure_hides_the_entire_prefix()
    {
        var state = Ready(move: 0, evade: 0);
        Assert.Equal(ulong.MaxValue, Scenarios(state, rng: new(1, ulong.MaxValue - 2)).RngState.DrawCount);
        var composition = Compose(state, rng: new(1, ulong.MaxValue - 13)); Assert.True(composition.Accepted); Assert.Equal(ulong.MaxValue, composition.RngState.DrawCount);
        var selection = Select(state, rng: new(1, ulong.MaxValue - 14)); Assert.True(selection.Accepted); Assert.Equal(ulong.MaxValue, selection.RngState.DrawCount);
        var incoming = new RandomState(1, ulong.MaxValue - 13); var shortLottery = Select(state, rng: incoming);
        Assert.False(shortLottery.Accepted); Assert.Equal(BasicSkillAiDerivedStage.Lottery, shortLottery.Failure!.Stage);
        Assert.Null(shortLottery.Outcome); Assert.Equal(incoming, shortLottery.RngState);
        var unknown = Ready(move: 1, target: new(5, 1, 0)); incoming = new(1, ulong.MaxValue - 3);
        var provenance = Compose(unknown, rng: incoming); Assert.Equal(BasicSkillAiDerivedStage.Provenance, provenance.Failure!.Stage);
        Assert.Null(provenance.Outcome); Assert.Equal(incoming, provenance.RngState);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Remaining_source_facts_and_terminal_defaults_are_required_without_a_public_cache_escape()
    {
        var state = Ready(); var p = DerivedProfile();
        foreach (var bad in new[] { p with { SourceGeneralEntry = false }, p with { FreshEmptyRankedList = false }, p with { ClearDirectTrajectories = false } })
        {
            var result = Select(state, bad); Assert.False(result.Accepted); Assert.Null(result.Outcome);
            Assert.Equal(BasicSkillAiDerivedStage.Admission, result.Failure!.Stage); Assert.Equal(new RandomState(1, 7), result.RngState);
        }
        var rejected = Select(state, p with { Scenarios = p.Scenarios with { NoOtherScenarioOverrides = false } });
        Assert.False(rejected.Accepted); Assert.Equal(BasicSkillAiDerivedStage.Scenarios, rejected.Failure!.Stage); Assert.Null(rejected.Outcome);
        var noDefend = BasicSkillAiDerivedSelection.Evaluate(state.Battle, SelfData.Request(), p, Selection with { NoDefendSupport = false }, new(1, 7));
        Assert.False(noDefend.Accepted); Assert.Equal(BasicSkillAiDerivedStage.Admission, noDefend.Failure!.Stage); Assert.Null(noDefend.Outcome);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Derived_queries_replay_for_both_teams_identities_and_collection_orders_without_mutation()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(second: second, word: 7); var before = Snapshot(state.Battle);
            var first = BasicSkillAiDerivedSelection.Evaluate(state.Battle, SelfData.Request(second), DerivedProfile(team), Selection, new(7, 7));
            var reverse = BasicSkillAiDerivedSelection.Evaluate(Ready(second: second, reverse: true, word: 7).Battle,
                SelfData.Request(second) with { Turn = SelfData.RefreshRequest(second) with { Policies = SelfData.RefreshRequest(second).Policies.Reverse().ToImmutableArray() } },
                DerivedProfile(team), Selection, new(7, 7));
            Assert.True(first.Accepted, first.Failure?.ToString()); Assert.True(reverse.Accepted, reverse.Failure?.ToString());
            Assert.Equal(first.RngState, reverse.RngState); Assert.Equal(Serialize(first.Outcome), Serialize(reverse.Outcome)); Assert.Equal(before, Snapshot(state.Battle));
        }
    }
}
