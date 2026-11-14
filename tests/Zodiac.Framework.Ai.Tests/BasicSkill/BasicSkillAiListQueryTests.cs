using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiListPreparationTests;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiListQueryTests
{
    internal static readonly BasicSkillAiListSelectionProfile ListSelection = new(true, true);
    internal static BasicSkillAiListProfile ListProfile(AiHumanAbilityKind[] actions, AiHumanAbilityKind[]? peer = null,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue) => new(Config(actions, peer, team), new(true, true, true), true, true, true);
    internal static BasicSkillAiListSelectionResult SelectList(FacingTurnState state, AiHumanAbilityKind[] actions,
        AiPriorityWorkspace? history = null, RandomState? rng = null) => BasicSkillAiListSelection.Evaluate(state.Battle, SelfData.Request(),
            ListProfile(actions), ListSelection, rng ?? state.Battle.TurnBattle.Turns.RngState, history);

    [Fact]
    public void Stone_presence_derives_ranged_D0_floor_without_changing_the_single_movement_prefix()
    {
        var state = Ready();
        var melee = BasicSkillAiListScenarios.Evaluate(state.Battle, SelfData.Request(), ListProfile([]), new(1, 7));
        var ranged = BasicSkillAiListScenarios.Evaluate(state.Battle, SelfData.Request(), ListProfile([AiHumanAbilityKind.ThrowStone]), new(1, 7));
        Assert.True(melee.Accepted); Assert.True(ranged.Accepted);
        Assert.Equal(new RandomState(2111915288, 22), melee.RngState); Assert.Equal(melee.RngState, ranged.RngState);
        Assert.Equal(0, melee.Outcome!.Movement.BaselineDistanceLimit); Assert.Equal(3, ranged.Outcome!.Movement.BaselineDistanceLimit);
        Assert.Equal(new TilePosition(2, 2, 0), melee.Outcome.Movement.BaselineDestination);
        Assert.Equal(new TilePosition(1, 3, 0), ranged.Outcome.Movement.BaselineDestination);
        Assert.Equal(new TilePosition(1, 4, 0), melee.Outcome.Movement.ActFirstDestination);
        Assert.Equal(14, melee.Outcome.Movement.Movement.Length); Assert.Equal(14, ranged.Outcome.Movement.Movement.Length);
    }

    [Fact]
    public void Missing_Stone_never_reads_range_four_keys_or_invokes_absent_ability_grants()
    {
        var state = Ready(move: 1, evade: 0, target: new(4, 1, 0)); var p = ListProfile([]) with { Preparation = Config([], selfGranted: false, stoneGranted: false), ClearDirectTrajectories = false };
        var result = BasicSkillAiListSelection.Evaluate(state.Battle, SelfData.Request(), p, ListSelection, new(1, 7));
        Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Empty(result.Outcome!.Composition.CastOrigins);
        Assert.Null(result.Outcome.Composition.Scenarios.Movement.Cells.Single(c => c.Tile == Origin).WrittenPriority);
        Assert.Equal(BasicSkillAiPlanKind.MoveOnly, result.Outcome.Plan.Kind); Assert.Equal(8, result.Outcome.Lottery.Length);
        Assert.Equal(new RandomState(180171308, 18), result.RngState); Assert.Equal(AiHumanAbilityKind.Attack, result.Outcome.Composition.LastEnteredKind);
        Assert.All(result.Outcome.Composition.Entries, e => { Assert.Null(e.Accumulate); Assert.Null(e.ThrowStone); });
        var unknown = SelectList(state, [AiHumanAbilityKind.ThrowStone]);
        Assert.False(unknown.Accepted); Assert.Equal(BasicSkillAiDerivedStage.Provenance, unknown.Failure!.Stage); Assert.Equal(new RandomState(1, 7), unknown.RngState);
        var known = SelectList(state, [AiHumanAbilityKind.ThrowStone], BasicSkillAiPriorityWorkspaceQueryTests.Seed(state)); Assert.True(known.Accepted);
    }

    [Fact]
    public void Repeated_self_occurrences_have_separate_coins_and_independent_PA_projections_and_ranks()
    {
        var state = Ready(move: 0, word: 10); var before = Snapshot(state.Battle);
        var result = BasicSkillAiListComposition.Evaluate(state.Battle, SelfData.Request(),
            ListProfile([AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate]), new(10, 7));
        Assert.True(result.Accepted, result.Failure?.ToString()); var entries = result.Outcome!.Entries.Where(e => e.Accumulate is not null).ToArray();
        Assert.Equal(2, entries.Length); Assert.Equal((uint)27592, entries[0].Accumulate!.Usefulness.Targets.Single(t => t.UnitId == A).Raw);
        Assert.Equal((uint)30710, entries[1].Accumulate!.Usefulness.Targets.Single(t => t.UnitId == A).Raw);
        Assert.All(entries, e => Assert.Equal(6, Assert.Single(e.Accumulate!.Candidates).Projection!.Outcome!.PhysicalAttackAfter));
        Assert.Equal(new[] { 1, 2 }, result.Outcome.RankedActions.Where(r => r?.Action.Kind == BasicSkillAiActionKind.Accumulate).Select(r => r!.OccurrenceIndex));
        Assert.Equal(before, Snapshot(state.Battle));
    }

    [Fact]
    public void Absent_self_skips_its_coin_while_two_occurrences_can_use_only_the_second()
    {
        var state = Ready(move: 0, evade: 97);
        var twice = SelectList(state, [AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.Accumulate]); Assert.True(twice.Accepted);
        var entries = twice.Outcome!.Composition.Entries;
        Assert.Empty(entries[1].Accumulate!.Candidates); Assert.Single(entries[2].Accumulate!.Candidates);
        Assert.Equal(BasicSkillAiPlanKind.Accumulate, twice.Outcome.Plan.Kind); Assert.Equal(2, twice.Outcome.SelectedOccurrenceIndex);
        Assert.Equal(new RandomState(836760821, 19), twice.RngState); Assert.Equal(AiHumanAbilityKind.Accumulate, twice.Outcome.Composition.LastEnteredKind);
        var absent = SelectList(Ready(move: 0, evade: 0), []); Assert.True(absent.Accepted);
        Assert.Equal(new RandomState(2633739833, 15), absent.RngState); Assert.Equal(BasicSkillAiPlanKind.Attack, absent.Outcome!.Plan.Kind);
    }

    [Fact]
    public void Reordered_entries_keep_exact_source_rng_checkpoints_and_independent_later_damage()
    {
        var state = Ready(move: 0, word: 7);
        var result = BasicSkillAiListComposition.Evaluate(state.Battle, SelfData.Request(), ListProfile([AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.Accumulate]), new(7, 7));
        Assert.True(result.Accepted); var entries = result.Outcome!.Entries;
        Assert.Equal(AiHumanAbilityKind.ThrowStone, entries[1].Entry.Source.Kind); Assert.Equal(AiHumanAbilityKind.Accumulate, entries[2].Entry.Source.Kind);
        Assert.Equal(entries[1].ExitRng, entries[2].EntryRng); Assert.Equal(10, entries[1].ThrowStone!.Candidate!.Evaluation.Projection!.Outcome!.NormalDamage);
        Assert.Equal(AiHumanAbilityKind.Accumulate, result.Outcome.LastEnteredKind); Assert.Single(result.Outcome.Scenarios.Movement.Movement);
    }

    [Fact]
    public void Duplicate_Stone_scans_are_not_cached_or_deduplicated_even_after_the_eight_ranked_slots_fill()
    {
        var state = Ready(move: 0, target: new(5, 1, 0));
        var result = BasicSkillAiListComposition.Evaluate(state.Battle, SelfData.Request(),
            ListProfile(Enumerable.Repeat(AiHumanAbilityKind.ThrowStone, 10).ToArray()), new(1, 7));
        Assert.True(result.Accepted, result.Failure?.ToString()); Assert.Equal(10, result.Outcome!.Entries.Count(e => e.ThrowStone is not null));
        Assert.Equal(Enumerable.Range(1, 8), result.Outcome.RankedActions.Select(r => r!.OccurrenceIndex));
        Assert.Null(result.Outcome.Entries[9].InsertionIndex); Assert.Null(result.Outcome.Entries[10].InsertionIndex);
        Assert.Equal((ulong)60, result.RngState.DrawCount); Assert.Single(result.Outcome.Scenarios.Movement.Movement);
    }

    [Fact]
    public void Each_NotValuable_Stone_still_consumes_its_own_formula_work_before_the_empty_lottery()
    {
        var state = Ready(move: 0, evade: 100, target: new(5, 1, 0));
        var result = SelectList(state, [AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.ThrowStone]); Assert.True(result.Accepted);
        foreach (var e in result.Outcome!.Composition.Entries.Where(e => e.ThrowStone is not null))
        {
            Assert.Equal(AiScoringDisposition.NotValuable, e.ThrowStone!.Candidate!.Evaluation.Disposition);
            Assert.Equal((ulong)4, e.ExitRng.DrawCount - e.EntryRng.DrawCount);
        }
        Assert.Equal(8, result.Outcome.Lottery.Length); Assert.Equal(BasicSkillAiPlanKind.MoveOnly, result.Outcome.Plan.Kind);
    }

    [Fact]
    public void Canonical_list_selection_matches_the_existing_typed_plan_stream_and_shared_candidates()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(move: 0, word: 7, second: second); var p = ListProfile([AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.ThrowStone], team: team);
            var result = BasicSkillAiListSelection.Evaluate(state.Battle, SelfData.Request(second), p, ListSelection, new(7, 7));
            var old = BasicSkillAiDerivedSelection.Evaluate(state.Battle, SelfData.Request(second), new(p.Preparation, p.Scenarios, true, true, true), new(true, true, true), new(7, 7));
            Assert.True(result.Accepted); Assert.True(old.Accepted); Assert.Equal(old.RngState, result.RngState);
            Assert.Equal(Serialize(old.Outcome!.Selection.Plan), Serialize(result.Outcome!.Plan));
            Assert.Equal(Serialize(old.Outcome.Selection.Composition.Accumulate), Serialize(result.Outcome.Composition.Entries[1].Accumulate));
        }
    }

    [Fact]
    public void Late_lottery_failure_restores_the_entire_query_and_priority_knowledge()
    {
        var state = Ready(move: 0, evade: 0); var history = AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
        var p = ListProfile([]);
        var exact = BasicSkillAiListSelection.Evaluate(state.Battle, SelfData.Request(), p, ListSelection, new(1, ulong.MaxValue - 8), history);
        Assert.True(exact.Accepted); Assert.Equal(ulong.MaxValue, exact.RngState.DrawCount);
        var shortRng = new RandomState(1, ulong.MaxValue - 7);
        var failed = BasicSkillAiListSelection.Evaluate(state.Battle, SelfData.Request(), p, ListSelection, shortRng, history);
        Assert.False(failed.Accepted); Assert.Equal(BasicSkillAiDerivedStage.Lottery, failed.Failure!.Stage);
        Assert.Null(failed.Outcome); Assert.Same(history, failed.Workspace); Assert.Equal(shortRng, failed.RngState);
    }
}
