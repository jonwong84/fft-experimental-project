using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using SelfData = Zodiac.Framework.Ai.Tests.AccumulateAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiTestData;
using static Zodiac.Framework.Ai.Tests.BasicSkillAiDerivedQueryTests;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BasicSkillAiPriorityWorkspaceQueryTests
{
    internal static AttackBattleSnapshot OnMap(AttackBattleSnapshot state, BattleMapDefinition map, BattleUnitId? damaged = null)
    {
        var old = state.TurnBattle;
        return new(new(new(map, old.Battle.Units, old.Battle.RngState), old.Turns), state.Combatants.Select(u =>
            damaged == u.UnitId ? new AttackUnitState(u.UnitId, 40, u.MaxHp, u.Mp, u.MaxMp, u.PhysicalAttack,
                u.ClassPhysicalEvade, u.RightHandEvade, u.LeftHandEvade, u.AccessoryEvade, u.Brave, u.ZodiacSign,
                u.Facing, AttackUnitStatus.None, u.KoCount, u.RightHandWeaponId) : u));
    }

    // History is produced by an owned real fresh query, never by importing public diagnostics or numeric seeds.
    internal static AiPriorityWorkspace Seed(FacingTurnState state, int sign = -1, bool second = false)
    {
        var prior = OnMap(Ready(move: 3, second: second).Battle, state.Battle.TurnBattle.Battle.Map,
            sign < 0 ? (second ? A : B) : sign > 0 ? (second ? B : A) : null);
        var p = DerivedProfile();
        var result = BasicSkillAiScenarios.EvaluateWithWorkspace(prior, SelfData.Request(second), p.Preparation, p.Scenarios,
            AiPriorityWorkspace.StartUnknown(prior.TurnBattle.Battle.Map), new(1, 7));
        Assert.True(result.Query.Accepted, result.Query.Failure?.ToString());
        Assert.Equal(sign * 26, result.Workspace.KnownPriorities[Origin]); return result.Workspace;
    }

    internal static BasicSkillAiWorkspaceSelectionResult Select(FacingTurnState state, AiPriorityWorkspace history,
        SwordAiScenarioTeam team = SwordAiScenarioTeam.NonBlue, bool second = false, RandomState? rng = null) =>
        BasicSkillAiDerivedSelection.EvaluateWithWorkspace(state.Battle, SelfData.Request(second), DerivedProfile(team), Selection,
            history, rng ?? state.Battle.TurnBattle.Turns.RngState);

    [Fact]
    public void Unknown_start_and_owned_scenario_writes_preserve_input_and_commit_only_completed_queries()
    {
        var state = Ready(); var p = DerivedProfile(); var unknown = AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
        Assert.Empty(unknown.KnownPriorities);
        var first = BasicSkillAiScenarios.EvaluateWithWorkspace(state.Battle, SelfData.Request(), p.Preparation, p.Scenarios, unknown, new(1, 7));
        Assert.True(first.Query.Accepted); Assert.Equal(14, first.Workspace.KnownPriorities.Count); Assert.Empty(unknown.KnownPriorities);
        foreach (var tile in first.Query.Outcome!.Movement.Movement)
            Assert.Equal(tile.TilePriority, first.Workspace.KnownPriorities[tile.Outcome.Destination]);
        var failed = BasicSkillAiScenarios.EvaluateWithWorkspace(state.Battle, SelfData.Request(), p.Preparation, p.Scenarios,
            first.Workspace, new(1, ulong.MaxValue - 2));
        Assert.False(failed.Query.Accepted); Assert.Same(first.Workspace, failed.Workspace); Assert.Null(failed.Query.Outcome);
        Assert.Equal(new RandomState(1, ulong.MaxValue - 2), failed.Query.RngState);
    }

    [Fact]
    public void Retained_signed_priorities_change_first_Stone_origin_without_actor_rescore_or_extra_draws()
    {
        foreach (int sign in new[] { -1, 0, 1 })
        foreach (int distance in new[] { 3, 4 })
        {
            var state = Ready(move: 1, evade: 0, target: new(1 + distance, 1, 0)); var before = Snapshot(state.Battle);
            var history = Seed(state, sign); var result = Select(state, history);
            Assert.True(result.Query.Accepted, result.Query.Failure?.ToString()); var o = result.Query.Outcome!;
            Assert.Equal(new RandomState(267834847, 17), result.Query.RngState);
            Assert.Equal(BasicSkillAiPlanKind.ThrowStone, o.Selection.Plan.Kind); Assert.Equal(sign <= 0, o.Selection.Plan.ActBeforeMove);
            Assert.Single(o.Selection.Composition.Movement); Assert.Contains(Origin, o.ScanOrigins);
            var actor = result.CastOrigins.Single(c => c.Tile == Origin);
            Assert.Equal(AiCastPriorityProvenance.Retained, actor.Provenance); Assert.Equal(sign * 26, actor.Priority);
            Assert.Equal((byte)(10 - distance), actor.Proximity); Assert.Equal(actor.Priority * 65536 + actor.Proximity, actor.OriginKey);
            var scanned = o.Selection.Composition.ThrowStone.Candidate!;
            Assert.Equal(sign <= 0 ? Origin : new(2, 1, 0), scanned.Origin);
            Assert.Equal(new TilePosition(2, 1, 0), scanned.Candidate.Destination);
            Assert.Equal(history.KnownPriorities[Origin], result.Workspace.KnownPriorities[Origin]);
            Assert.Equal((short)0, result.Workspace.KnownPriorities[new(2, 1, 0)]); Assert.Equal(before, Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Fresh_proximity_and_later_coordinate_tie_order_apply_to_retained_priorities()
    {
        var ready = Ready(move: 1, evade: 0, target: new(4, 1, 0)); var history = Seed(ready, 0);
        var old = ready.Battle.TurnBattle;
        var turns = new TurnCycleState(old.Turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed,
            u.Id == B ? 0 : u.Ct, u.TieKey, u.HasTurn, u.MovementTaken, u.ActionTaken)), old.Turns.RngState, old.Turns.Ticks, old.Turns.Phase);
        var state = new AttackBattleSnapshot(new(old.Battle, turns), ready.Battle.Combatants);
        var result = BasicSkillAiDerivedSelection.EvaluateWithWorkspace(state, SelfData.Request(), DerivedProfile(), Selection, history, new(1, 7));
        Assert.True(result.Query.Accepted, result.Query.Failure?.ToString());
        Assert.All(result.CastOrigins, c => { Assert.Equal((byte)0, c.Proximity); Assert.Equal(0, c.OriginKey); });
        Assert.Equal(new TilePosition(2, 1, 0), result.Query.Outcome!.Selection.Composition.ThrowStone.Candidate!.Origin);
        Assert.False(result.Query.Outcome.Selection.Plan.ActBeforeMove);
    }

    [Fact]
    public void NotValuable_Stone_consumes_its_aim_at_the_selected_retained_or_fresh_origin()
    {
        foreach (int sign in new[] { -1, 1 })
        {
            var state = Ready(move: 1, evade: 100, target: new(4, 1, 0)); var result = Select(state, Seed(state, sign));
            Assert.True(result.Query.Accepted, result.Query.Failure?.ToString());
            var stone = result.Query.Outcome!.Selection.Composition.ThrowStone;
            Assert.NotNull(stone.Candidate); Assert.Equal(sign < 0 ? Origin : new(2, 1, 0), stone.Candidate.Origin);
            Assert.Equal(AiScoringDisposition.NotValuable, stone.Candidate.Evaluation.Disposition);
            Assert.Null(stone.Rank); Assert.Equal(BasicSkillAiPlanKind.MoveOnly, result.Query.Outcome.Selection.Plan.Kind);
            Assert.Equal(8, result.Query.Outcome.Selection.Lottery.Length); Assert.Single(result.Query.Outcome.Selection.Composition.Movement);
        }
    }

    [Fact]
    public void Current_writes_override_history_and_only_S_is_updated_using_fresh_proximity()
    {
        var state = Ready(move: 1, evade: 0); var history = Seed(state); var result = Select(state, history);
        Assert.True(result.Query.Accepted); Assert.All(result.CastOrigins, c => Assert.Equal(AiCastPriorityProvenance.Fresh, c.Provenance));
        Assert.Equal((short)0, result.Workspace.KnownPriorities[Origin]); Assert.Equal((short)-26, history.KnownPriorities[Origin]);
        var untouched = new TilePosition(1, 4, 0); Assert.Equal(history.KnownPriorities[untouched], result.Workspace.KnownPriorities[untouched]);
        Assert.Equal(Serialize(result.Query), Serialize(BasicSkillAiDerivedSelection.Evaluate(state.Battle, SelfData.Request(), DerivedProfile(), Selection, new(1, 7))));
    }

    [Fact]
    public void Unknown_read_forgetting_and_different_map_cannot_fabricate_priorities()
    {
        var state = Ready(move: 1, target: new(4, 1, 0)); var unknown = AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
        var failed = Select(state, unknown); Assert.False(failed.Query.Accepted); Assert.Equal(BasicSkillAiDerivedStage.Provenance, failed.Query.Failure!.Stage);
        Assert.Same(unknown, failed.Workspace); Assert.Empty(failed.CastOrigins); Assert.Null(failed.Query.Outcome); Assert.Equal(new RandomState(1, 7), failed.Query.RngState);
        var history = Seed(state); var forgotten = history.Forget(); Assert.Empty(forgotten.KnownPriorities); Assert.NotEmpty(history.KnownPriorities);
        Assert.False(Select(state, forgotten).Query.Accepted);
        Assert.Throws<ArgumentException>(() => Select(Ready(move: 1, target: new(4, 1, 0)), history));
    }

    [Fact]
    public void Late_lottery_failure_rolls_back_both_RNG_and_new_priority_writes()
    {
        var state = Ready(move: 1, evade: 0, target: new(4, 1, 0)); var history = Seed(state);
        var exact = Select(state, history, rng: new(1, ulong.MaxValue - 10)); Assert.True(exact.Query.Accepted);
        Assert.Equal(ulong.MaxValue, exact.Query.RngState.DrawCount);
        var incoming = new RandomState(1, ulong.MaxValue - 9); var shortResult = Select(state, history, rng: incoming);
        Assert.False(shortResult.Query.Accepted); Assert.Equal(BasicSkillAiDerivedStage.Lottery, shortResult.Query.Failure!.Stage);
        Assert.Same(history, shortResult.Workspace); Assert.Empty(shortResult.CastOrigins); Assert.Null(shortResult.Query.Outcome); Assert.Equal(incoming, shortResult.Query.RngState);
    }

    [Fact]
    public void Retained_actor_does_not_skip_self_or_change_later_actual_state_candidates()
    {
        var state = Ready(move: 1, word: 7, target: new(4, 1, 0)); var result = Select(state, Seed(state));
        Assert.True(result.Query.Accepted, result.Query.Failure?.ToString()); var c = result.Query.Outcome!.Selection.Composition;
        Assert.Single(c.Movement); Assert.Single(c.Accumulate.Usefulness.Targets.Where(t => t.UnitId == A));
        Assert.Equal(10, c.ThrowStone.Candidate!.Evaluation.Projection!.Outcome!.NormalDamage);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Workspace_queries_are_immutable_branches_and_replay_for_both_teams_and_actors()
    {
        foreach (var team in new[] { SwordAiScenarioTeam.NonBlue, SwordAiScenarioTeam.Blue })
        foreach (bool second in new[] { false, true })
        {
            var state = Ready(move: 1, evade: 0, second: second, target: new(4, 1, 0)); var history = Seed(state, second: second);
            var before = Serialize(history.KnownPriorities.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X));
            var first = Select(state, history, team, second); var replay = Select(state, history, team, second);
            Assert.True(first.Query.Accepted, first.Query.Failure?.ToString()); Assert.Equal(Serialize(first.Query), Serialize(replay.Query));
            Assert.Equal(Serialize(first.CastOrigins), Serialize(replay.CastOrigins));
            Assert.Equal(before, Serialize(history.KnownPriorities.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X)));
        }
    }
}
