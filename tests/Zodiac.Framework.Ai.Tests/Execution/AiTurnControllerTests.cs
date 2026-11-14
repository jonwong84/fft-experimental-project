using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;
using static Zodiac.Framework.Ai.Tests.RosterAiSessionTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AiTurnControllerTests
{
    public static IEnumerable<object[]> SourceIds()
    {
        using var doc = Fixture();
        return doc.RootElement.GetProperty("vectors").EnumerateArray().Select(r => new object[] { r.GetProperty("id").GetString()! }).ToArray();
    }
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "roster-turn-contract-v1.json")));
    private static JsonElement Row(JsonDocument doc, string id) => doc.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
    private static TilePosition Tile(JsonElement a) => new(a[0].GetInt32(), a[1].GetInt32(), 0);
    private static AiCommandResult Command(Sample s, RosterFacingTurnState? state = null,
        AiTurnConfiguration? configuration = null, AiPriorityWorkspace? workspace = null)
    {
        var c = configuration ?? s.Configuration;
        return AiCommandExecutor.Execute(state ?? s.State, s.Setup.Request, c.Decision, c.Dispatch, c.Facing, workspace);
    }

    [Theory]
    [MemberData(nameof(SourceIds))]
    public void Source_command_and_complete_turn_tapes_match_real_choices_effects_rng_and_priority_publication(string id)
    {
        using var doc = Fixture(); var row = Row(doc, id); var d = row.GetProperty("input"); var e = row.GetProperty("expected"); var s = Load(d);
        var before = Snapshot(s.State.Battle); var workspace = AiPriorityWorkspace.StartUnknown(s.State.Battle.TurnBattle.Battle.Map);
        RosterFacingTurnState state; AiCommandOutcome[] decisions; AiPriorityWorkspace successor;
        bool failed; bool continuation;
        if (row.GetProperty("wholeTurn").GetBoolean())
        {
            var r = AiTurnController.Execute(s.State, s.Setup.Request, s.Configuration, workspace);
            state = r.State; decisions = r.Decisions.ToArray(); successor = r.Workspace; failed = !r.Completed; continuation = false;
            Assert.Equal(failed, r.Recovery is not null);
            var replay = AiTurnController.Execute(s.State, s.Setup.Request, s.Configuration, workspace);
            Assert.Equal(Snapshot(state.Battle), Snapshot(replay.State.Battle));
            Assert.Equal(JsonSerializer.Serialize(r.Events), JsonSerializer.Serialize(replay.Events));
        }
        else
        {
            var r = Command(s, workspace: workspace); state = r.State; decisions = r.Outcome is { } o ? [o] : [];
            successor = r.Workspace; failed = !r.Accepted; continuation = r.Continuation is not null;
            Assert.NotEmpty(r.Events);
        }
        Assert.Equal(before, Snapshot(s.State.Battle)); Assert.Empty(workspace.KnownPriorities);
        Assert.Equal(e.GetProperty("failure").ValueKind != JsonValueKind.Null, failed);
        Assert.Equal(e.GetProperty("continuation").GetBoolean(), continuation);
        Assert.Equal(e.GetProperty("pending").GetBoolean(), state.PendingFacing is not null);
        Assert.Equal(e.GetProperty("active").GetBoolean(), state.Battle.TurnBattle.Turns.ActiveUnitId == s.Setup.Request.ActorId);
        Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), state.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(state.Battle.TurnBattle.Turns.RngState, state.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(e.GetProperty("ticks").GetUInt64(), state.Battle.TurnBattle.Turns.Ticks);
        var expectedDecisions = e.GetProperty("decisions").EnumerateArray().ToArray(); Assert.Equal(expectedDecisions.Length, decisions.Length);
        for (int i = 0; i < decisions.Length; i++)
        {
            var x = expectedDecisions[i]; var a = decisions[i];
            Assert.Equal(x.GetProperty("afterAct").GetBoolean(), a.AfterAct);
            Assert.Equal(x.GetProperty("kind").GetString(), Name(a.Kind));
            var selected = a.Decision.SelectedRankedIndex is { } index ? a.Decision.RankedActions[index]!.OccurrenceIndex : (int?)null;
            Assert.Equal(x.GetProperty("occurrence").ValueKind == JsonValueKind.Null ? null : x.GetProperty("occurrence").GetInt32(), selected);
            Assert.Equal(new RandomState(x.GetProperty("word").GetUInt32(), x.GetProperty("count").GetUInt64()), a.DecisionRng);
        }
        foreach (var u in e.GetProperty("units").EnumerateArray())
        {
            var key = new BattleUnitId(u.GetProperty("id").GetString()!);
            var body = state.Battle.TurnBattle.Battle.Units.Single(v => v.Id == key);
            var turn = state.Battle.TurnBattle.Turns.Units.Single(v => v.Id == key);
            var actual = state.Battle.Combatants.Single(v => v.UnitId == key);
            Assert.Equal(Tile(u.GetProperty("tile")), body.Position); Assert.Equal(u.GetProperty("hp").GetInt32(), actual.Hp);
            Assert.Equal(u.GetProperty("pa").GetInt32(), actual.PhysicalAttack); Assert.Equal(u.GetProperty("ko").GetInt32(), actual.KoCount);
            Assert.Equal(u.GetProperty("ct").GetInt32(), turn.Ct); Assert.Equal(u.GetProperty("moved").GetBoolean(), turn.MovementTaken);
            Assert.Equal(u.GetProperty("acted").GetBoolean(), turn.ActionTaken); Assert.Equal(Enum.Parse<BattleFacing>(u.GetProperty("face").GetString()!), actual.Facing);
        }
        var priorities = e.GetProperty("workspace").EnumerateArray().ToArray(); Assert.Equal(priorities.Length, successor.KnownPriorities.Count);
        foreach (var p in priorities) Assert.Equal(p.GetProperty("priority").GetInt16(), successor.KnownPriorities[Tile(p.GetProperty("tile"))]);
    }
    private static string Name(AiCommandKind kind) => kind switch {
        AiCommandKind.Accumulate => "Self", AiCommandKind.ThrowStone => "Stone", _ => kind.ToString() };

    [Fact]
    public void Exact_session_recovery_keeps_actual_KO_and_retries_only_the_failed_fresh_step()
    {
        using var doc = Fixture(); var s = Load(Row(doc, "accepted-ACT-first-KO-retained-at-deferred-corpse-guard").GetProperty("input"));
        var r = AiTurnController.Execute(s.State, s.Setup.Request, s.Configuration);
        Assert.False(r.Completed); Assert.Single(r.Decisions); Assert.NotEmpty(r.Events); Assert.NotEmpty(r.Workspace.KnownPriorities);
        Assert.Equal(AiTurnStage.PostActCommand, r.Recovery!.NextStep);
        var retry = AiTurnController.Resume(r.State, r.Recovery);
        Assert.False(retry.Completed); Assert.Same(r.State, retry.State); Assert.Same(r.Workspace, retry.Workspace);
        Assert.Empty(retry.Events); Assert.Empty(retry.Decisions); Assert.NotNull(retry.Recovery);
        var abandoned = RosterFacingTurnBattle.WaitWithFacing(r.State, s.Setup.Request.ActorId); Assert.True(abandoned.Accepted);
        var stale = AiTurnController.Resume(abandoned.State, r.Recovery);
        Assert.Equal(AiTurnStage.RecoveryBinding, stale.Failure!.Stage); Assert.Same(abandoned.State, stale.State); Assert.Null(stale.Recovery);
        var foreign = Load(Row(doc, "accepted-ACT-first-KO-retained-at-deferred-corpse-guard").GetProperty("input"));
        Assert.Equal(AiTurnStage.RecoveryBinding, AiTurnController.Resume(foreign.State, r.Recovery).Failure!.Stage);
    }

    [Fact]
    public void Late_invalid_facing_retains_Move_and_ACT_and_recovery_does_not_replay_them()
    {
        using var doc = Fixture(); var s = Load(Row(doc, "complete-frozen-Stone-followup-and-facing").GetProperty("input"));
        var c = s.Configuration with { Facing = (BattleFacing)255 };
        var r = AiTurnController.Execute(s.State, s.Setup.Request, c);
        Assert.False(r.Completed); Assert.Single(r.Decisions); Assert.NotNull(r.State.PendingFacing);
        Assert.Equal(AiTurnStage.FinishFacing, r.Recovery!.NextStep);
        var retry = AiTurnController.Resume(r.State, r.Recovery);
        Assert.Same(r.State, retry.State); Assert.Same(r.Workspace, retry.Workspace); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
        Assert.Equal(FacingTurnRejection.UnsupportedFacing, retry.Failure!.FacingFailure!.FacingRejection);
        var manual = RosterFacingTurnBattle.FinishFacing(r.State, s.Setup.Request.ActorId, r.State.PendingFacing, BattleFacing.East);
        Assert.True(manual.Accepted); Assert.Null(manual.State.PendingFacing);
        var finishOnly = AiTurnController.Execute(r.State, s.Setup.Request, s.Configuration);
        Assert.True(finishOnly.Completed); Assert.Empty(finishOnly.Decisions); Assert.Equal(r.State.Battle.TurnBattle.Turns.RngState, finishOnly.State.Battle.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Failed_first_selection_or_late_wait_settlement_publishes_no_private_workspace_or_rng()
    {
        using var doc = Fixture();
        foreach (string id in new[] { "late-unknown-priority-before-any-actual-publication", "same-tile-empty-ranking-END" })
        {
            var s = Load(Row(doc, id).GetProperty("input")); var w = AiPriorityWorkspace.StartUnknown(s.State.Battle.TurnBattle.Battle.Map);
            var r = Command(s, configuration: s.Configuration with { Facing = (BattleFacing)255 }, workspace: w);
            Assert.False(r.Accepted); Assert.Same(s.State, r.State); Assert.Same(w, r.Workspace);
            Assert.Empty(r.Events); Assert.Null(r.Path); Assert.Null(r.Continuation); Assert.Null(r.Outcome);
        }
    }

    [Fact]
    public void Authoritative_roster_facts_and_explicit_dispatch_flags_are_required_before_selection()
    {
        using var doc = Fixture(); var s = Load(Row(doc, "first-attack-ACT-first").GetProperty("input"));
        var wrong = new FlatRosterBattleProfile(Ground, s.Configuration.Decision.Simulation.Battle.Members.Select(u => u with { StableTeam = (u.StableTeam + 1) % 4 }));
        var configs = new[] { s.Configuration with { Dispatch = new() },
            s.Configuration with { Decision = s.Configuration.Decision with { Simulation = new(wrong, s.Configuration.Decision.Simulation.Units, true) } } };
        foreach (var c in configs)
        {
            var r = Command(s, configuration: c); Assert.False(r.Accepted);
            Assert.Equal(AiCommandRejection.UnsupportedDispatchContext, r.Failure!.Reason);
            Assert.Same(s.State, r.State); Assert.Empty(r.Events); Assert.Null(r.Outcome);
        }
        var equal = new FlatRosterBattleProfile(Ground, s.Configuration.Decision.Simulation.Battle.Members.Reverse());
        Assert.True(Command(s, configuration: s.Configuration with { Decision = s.Configuration.Decision with { Simulation = new(equal, s.Configuration.Decision.Simulation.Units, true) } }).Accepted);
    }

    [Fact]
    public void Kept_facing_storage_permutation_and_retained_followup_replay_are_deterministic()
    {
        using var doc = Fixture(); var d = Row(doc, "complete-frozen-Stone-followup-and-facing").GetProperty("input");
        var s = Load(d); var c = s.Configuration with { Facing = null };
        var first = Command(s, configuration: c); Assert.True(first.Accepted); Assert.NotNull(first.Continuation); Assert.NotNull(first.Path);
        var retained = RosterMoveDecisionBattle.Continue(first.State, s.Setup.Request.ActorId, first.Continuation); Assert.True(retained.Accepted);
        var replay = RosterMoveDecisionBattle.Continue(first.State, s.Setup.Request.ActorId, first.Continuation);
        Assert.Equal(Snapshot(retained.State.Battle), Snapshot(replay.State.Battle));
        var all = AiTurnController.Execute(s.State, s.Setup.Request, c);
        Assert.True(all.Completed); Assert.Single(all.Decisions);
        Assert.Equal(retained.State.Battle.Combatants.Single(u => u.UnitId == s.Setup.Request.ActorId).Facing,
            all.State.Battle.Combatants.Single(u => u.UnitId == s.Setup.Request.ActorId).Facing);
        var permuted = Load(d, true);
        var alternate = AiTurnController.Execute(permuted.State, permuted.Setup.Request, permuted.Configuration with { Facing = null });
        Assert.True(alternate.Completed); Assert.Equal(Snapshot(all.State.Battle), Snapshot(alternate.State.Battle));
        Assert.Equal(JsonSerializer.Serialize(all.Events), JsonSerializer.Serialize(alternate.Events));
    }

    [Fact]
    public void Actual_resolution_draw_exhaustion_rolls_back_successful_private_selection()
    {
        using var doc = Fixture(); var d = Row(doc, "first-attack-ACT-first").GetProperty("input");
        var ordinary = Load(d); var selected = Command(ordinary); Assert.True(selected.Accepted);
        ulong decisionDraws = selected.Outcome!.DecisionRng.DrawCount - ordinary.State.Battle.TurnBattle.Turns.RngState.DrawCount;
        var data = System.Text.Json.Nodes.JsonNode.Parse(d.GetRawText())!; data["count"] = ulong.MaxValue - decisionDraws;
        using var limited = JsonDocument.Parse(data.ToJsonString()); var s = Load(limited.RootElement);
        var w = AiPriorityWorkspace.StartUnknown(s.State.Battle.TurnBattle.Battle.Map); var r = Command(s, workspace: w);
        Assert.False(r.Accepted); Assert.Equal(AiCommandRejection.BoundaryRejected, r.Failure!.Reason);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, r.Execution.Transition.AttackRejection);
        Assert.Same(s.State, r.State); Assert.Same(w, r.Workspace); Assert.Empty(r.Events); Assert.Null(r.Outcome); Assert.Null(r.Continuation);
    }

    [Fact]
    public void Late_post_ACT_settlement_failure_retains_only_the_earlier_actual_ACT()
    {
        using var doc = Fixture(); var s = Load(Row(doc, "complete-attack-then-fresh-post-ACT").GetProperty("input"));
        var first = Command(s); Assert.True(first.Accepted); Assert.Equal(AiCommandKind.Attack, first.Outcome!.Kind);
        var bad = s.Configuration with { Facing = (BattleFacing)255 };
        var r = AiTurnController.Execute(s.State, s.Setup.Request, bad);
        Assert.False(r.Completed); Assert.Single(r.Decisions); Assert.Equal(AiTurnStage.PostActCommand, r.Recovery!.NextStep);
        Assert.Equal(Snapshot(first.State.Battle), Snapshot(r.State.Battle));
        Assert.Equal(JsonSerializer.Serialize(first.Events), JsonSerializer.Serialize(r.Events));
        Assert.Equal(first.Workspace.KnownPriorities.OrderBy(p => p.Key.X).ThenBy(p => p.Key.Y), r.Workspace.KnownPriorities.OrderBy(p => p.Key.X).ThenBy(p => p.Key.Y));
        var retry = AiTurnController.Resume(r.State, r.Recovery);
        Assert.Same(r.State, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions); Assert.Same(r.Workspace, retry.Workspace);
        var manual = Command(s, r.State, workspace: r.Workspace); Assert.True(manual.Accepted); Assert.Null(manual.State.PendingFacing);
    }

    [Fact]
    public void Retained_ACT_failure_and_retry_keep_the_published_Move_without_reselection()
    {
        using var doc = Fixture(); var d = Row(doc, "complete-frozen-Stone-followup-and-facing").GetProperty("input");
        var ordinary = Load(d); var first = Command(ordinary); Assert.True(first.Accepted); Assert.NotNull(first.Continuation);
        ulong decisionDraws = first.Outcome!.DecisionRng.DrawCount - ordinary.State.Battle.TurnBattle.Turns.RngState.DrawCount;
        var data = System.Text.Json.Nodes.JsonNode.Parse(d.GetRawText())!; data["count"] = ulong.MaxValue - decisionDraws;
        using var limited = JsonDocument.Parse(data.ToJsonString()); var s = Load(limited.RootElement);
        var move = Command(s); Assert.True(move.Accepted); Assert.NotNull(move.Path); Assert.Equal(ulong.MaxValue, move.Outcome!.DecisionRng.DrawCount);
        var r = AiTurnController.Execute(s.State, s.Setup.Request, s.Configuration);
        Assert.False(r.Completed); Assert.Single(r.Decisions); Assert.Equal(AiTurnStage.RetainedCommand, r.Recovery!.NextStep);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, r.Failure!.ContinuationFailure!.Transition.AttackRejection);
        Assert.Equal(Snapshot(move.State.Battle), Snapshot(r.State.Battle)); Assert.Equal(JsonSerializer.Serialize(move.Events), JsonSerializer.Serialize(r.Events));
        var retry = AiTurnController.Resume(r.State, r.Recovery);
        Assert.False(retry.Completed); Assert.Same(r.State, retry.State); Assert.Same(r.Workspace, retry.Workspace);
        Assert.Empty(retry.Events); Assert.Empty(retry.Decisions); Assert.Equal(AiTurnStage.RetainedCommand, retry.Recovery!.NextStep);
        var abandoned = RosterFacingTurnBattle.WaitWithFacing(r.State, s.Setup.Request.ActorId); Assert.True(abandoned.Accepted);
        Assert.Equal(AiTurnStage.RecoveryBinding, AiTurnController.Resume(abandoned.State, r.Recovery).Failure!.Stage);
    }
}
