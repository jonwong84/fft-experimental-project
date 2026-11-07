using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;
using static Zodiac.Framework.Ai.Tests.RosterAiSessionTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterDashAiTests
{
    private static readonly AssetId Dash = new("test:dash");
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "dash-ai-contract-v1.json")));
    private static JsonElement Row(JsonDocument doc, string id) => doc.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
    public static IEnumerable<object[]> Ids()
    { using var doc = Fixture(); return doc.RootElement.GetProperty("vectors").EnumerateArray().Select(r => new object[] { r.GetProperty("id").GetString()! }).ToArray(); }
    private static TilePosition Tile(JsonElement t) => new(t[0].GetInt32(), t[1].GetInt32(), 0);
    private static void Rng(JsonElement e, RandomState rng) => Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), rng);
    private static AiDecisionResult Select(Sample s, AiAbilityEvaluationProfile? p = null, RandomState? rng = null) =>
        AiDecisionMaker.Decide(s.State.Battle, s.Setup.Request, s.Configuration.Decision with { Abilities = p ?? s.Configuration.Decision.Abilities },
            rng ?? s.State.Battle.TurnBattle.Turns.RngState);
    private static AiCommandResult Command(Sample s, RosterFacingTurnState? state = null, AiPriorityWorkspace? w = null) =>
        AiCommandExecutor.Execute(state ?? s.State, s.Setup.Request, s.Configuration.Decision, s.Configuration.Dispatch, s.Configuration.Facing, w);

    [Theory]
    [MemberData(nameof(Ids))]
    public void Twenty_independent_source_descriptions_match_owned_preparation_candidate_search_selection_and_real_turns(string id)
    {
        using var doc = Fixture(); var row = Row(doc, id); var d = row.GetProperty("input"); var e = row.GetProperty("expected");
        var s = Load(d, allSwordBindings: true); var state = s.State; var kind = row.GetProperty("kind").GetString();
        if (id == "already-moved")
        {
            var data = System.Text.Json.Nodes.JsonNode.Parse(d.GetRawText())!; data["units"]![0]!["tile"] = new System.Text.Json.Nodes.JsonArray(0, 1);
            using var initial = JsonDocument.Parse(data.ToJsonString()); s = Load(initial.RootElement, allSwordBindings: true);
            var moved = RosterFacingTurnBattle.Move(s.State, new(A, new(1, 1, 0))); Assert.True(moved.Accepted); state = moved.State;
        }
        var inputState = state; string before = Snapshot(state.Battle);
        switch (kind)
        {
            case "preparation":
                var p = RosterAiPreparation.Evaluate(state.Battle, s.Setup.Request, s.Configuration.Decision.Simulation, Refresh, s.Configuration.Decision.Lists, state.Battle.TurnBattle.Turns.RngState);
                Assert.True(p.Accepted); Rng(e, p.RngState); Assert.Equal(e.GetProperty("harmful").GetInt32(), p.Outcome!.EnemyHarmfulCount);
                foreach (var u in p.Outcome.Units) Assert.Equal(e.GetProperty("share").GetProperty(u.UnitId.Value).GetInt32(), u.HelpfulShare);
                var entries = p.Outcome.ActorEntries.Where(a => a.Source.Kind == AiHumanAbilityKind.Dash).ToArray(); Assert.Equal(2, entries.Length);
                Assert.All(entries, a => { Assert.True(a.Usable); Assert.True(a.ConsumeAim); Assert.False(a.Source.Evadeable); Assert.False(a.Source.HasTrajectory); Assert.False(a.Source.ExhaustiveOriginScan); Assert.Equal(1, a.Source.Range); });
                break;
            case "candidate":
                var candidate = new DashAiCandidate(A, Dash, new(d.GetProperty("target").GetString()!), Tile(d.GetProperty("destination")), d.GetProperty("order").GetString() == "ActThenMove");
                var c = RosterDashAiCandidate.Evaluate(state.Battle, candidate, s.Configuration.Decision.Simulation, s.Configuration.Decision.Abilities.Dash!, new(true, true, true), state.Battle.TurnBattle.Turns.RngState);
                Assert.Equal(AiScoringDisposition.Scored, c.Disposition); Rng(e, c.RngState);
                Assert.Equal(e.GetProperty("damage").GetInt32(), c.Outcome!.NormalDamage); Assert.Equal(100, c.Outcome.HitPercent);
                Assert.Equal(e.GetProperty("endCt").GetInt32(), c.Outcome.TurnEndCt); Assert.Equal(e.GetProperty("next").GetString(), c.Outcome.MoveOpportunityUnitId.Value);
                Assert.Equal(e.GetProperty("ticks").GetUInt64(), c.Outcome.Ticks); Assert.Equal(e.GetProperty("priority").GetInt32(), c.Score!.Priority);
                foreach (var u in e.GetProperty("units").EnumerateArray())
                {
                    var actual = c.Outcome.Units.Single(v => v.UnitId.Value == u.GetProperty("id").GetString());
                    Assert.Equal(u.GetProperty("hp").GetInt32(), actual.Hp); Assert.Equal(u.GetProperty("ct").GetInt32(), actual.Ct);
                    Assert.Equal(u.GetProperty("status").GetString(), actual.Status.ToString()); Assert.Equal(u.GetProperty("counter").GetInt32(), actual.DeathCounter);
                }
                break;
            case "entry":
                var r = RosterDashAiEntry.Evaluate(state.Battle, s.Setup.Request, d.GetProperty("occurrence").GetInt32(), s.Configuration.Decision.Simulation,
                    Refresh, s.Configuration.Decision.Lists, s.Configuration.Decision.General, Movement, s.Configuration.Decision.Abilities.Dash!, true, state.Battle.TurnBattle.Turns.RngState);
                Assert.True(r.Accepted, r.Failure?.ToString()); Rng(e, r.RngState); var o = r.Outcome!;
                Assert.Equal(e.GetProperty("skipped").GetBoolean(), o.Skipped); Assert.Equal(NullableIndex(e.GetProperty("best")), o.BestCandidateIndex);
                Assert.Equal(e.GetProperty("ranked").EnumerateArray().Select(Tile), o.RankedOrigins.Select(v => v.Tile));
                Assert.Equal(e.GetProperty("origins").EnumerateArray().Select(Tile), o.EvaluatedOrigins);
                var cs = e.GetProperty("candidates").EnumerateArray().ToArray(); Assert.Equal(cs.Length, o.Candidates.Length);
                for (int i = 0; i < cs.Length; i++)
                {
                    var x = cs[i]; var a = o.Candidates[i]; Assert.Equal(Tile(x.GetProperty("origin")), a.Origin);
                    Assert.Equal(Tile(x.GetProperty("destination")), a.Candidate.Destination); Assert.Equal(x.GetProperty("actFirst").GetBoolean(), a.Candidate.ActBeforeMove);
                    Assert.Equal(x.GetProperty("target").GetString(), a.Candidate.TargetId.Value); Assert.Equal(x.GetProperty("priority").GetInt32(), a.Evaluation.Score!.Priority);
                    Assert.Equal(x.GetProperty("hit").GetInt32(), a.Evaluation.Score.HitPercent); Assert.Equal(x.GetProperty("proximity").GetInt32(), a.Proximity); Rng(x, a.Evaluation.RngState);
                }
                Assert.Equal(e.GetProperty("remainingOrigins").GetInt32(), o.RemainingOriginCount); Assert.Equal(e.GetProperty("remainingAims").GetInt32(), o.RemainingAimCount);
                break;
            case "list":
                var q = Select(s); Assert.True(q.Accepted, q.Failure?.ToString()); Rng(e, q.RngState); var list = q.Outcome!;
                Assert.Equal(NullableIndex(e.GetProperty("selected")), list.SelectedRankedIndex);
                Assert.Equal(e.GetProperty("entries").GetArrayLength(), list.Entries.Length);
                var es = e.GetProperty("entries").EnumerateArray().ToArray();
                for (int i = 0; i < es.Length; i++)
                {
                    var x = es[i]; var a = list.Entries[i]; Assert.Equal(new RandomState(x.GetProperty("entryWord").GetUInt32(), x.GetProperty("entryCount").GetUInt64()), a.EntryRng);
                    Assert.Equal(new RandomState(x.GetProperty("exitWord").GetUInt32(), x.GetProperty("exitCount").GetUInt64()), a.ExitRng);
                    Assert.Equal(NullableIndex(x.GetProperty("insertion")), a.InsertionIndex);
                }
                var ranks = e.GetProperty("ranked").EnumerateArray().ToArray(); Assert.Equal(8, list.RankedActions.Length);
                for (int i = 0; i < 8; i++)
                {
                    if (ranks[i].ValueKind == JsonValueKind.Null) { Assert.Null(list.RankedActions[i]); continue; }
                    var a = list.RankedActions[i]!; var x = ranks[i]; Assert.Equal(x.GetProperty("occurrence").GetInt32(), a.OccurrenceIndex);
                    Assert.Equal(x.GetProperty("priority").GetInt32(), a.Score.Priority); Assert.Equal(x.GetProperty("hit").GetInt32(), a.Score.HitPercent);
                    Assert.Equal(Tile(x.GetProperty("destination")), a.Destination); Assert.Equal(x.GetProperty("actFirst").GetBoolean(), a.ActBeforeMove);
                }
                break;
            case "turn":
                var turn = AiTurnController.Execute(state, s.Setup.Request, s.Configuration);
                Check(e, turn.State, turn.Workspace); Assert.Equal(e.GetProperty("failure").ValueKind == JsonValueKind.Null, turn.Completed);
                int dashCount = e.GetProperty("commands").EnumerateArray().Count(v => v.GetString() is "Dash" or "RetainedDash"); Assert.Equal(dashCount, turn.Events.OfType<DashResolvedEvent>().Count());
                var replay = AiTurnController.Execute(state, s.Setup.Request, s.Configuration);
                Assert.Equal(Snapshot(turn.State.Battle), Snapshot(replay.State.Battle)); Assert.Equal(Events(turn.Events), Events(replay.Events));
                if (!turn.Completed)
                {
                    var retry = AiTurnController.Resume(turn.State, turn.Recovery!); Assert.Same(turn.State, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions); Assert.Same(turn.Workspace, retry.Workspace);
                }
                break;
            case "replay":
                var workspace = AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map); int turns = 0;
                foreach (var step in e.EnumerateArray())
                {
                    var request = s.Setup.Request with { ActorId = new(step.GetProperty("actor").GetString()!) };
                    var result = AiTurnController.Execute(state, request, s.Configuration, workspace); Assert.True(result.Completed, result.Failure?.ToString());
                    Check(step.GetProperty("turn"), result.State, result.Workspace); state = result.State; workspace = result.Workspace; turns++;
                    if (step.GetProperty("clock").ValueKind == JsonValueKind.Null) continue;
                    var advanced = RosterFacingTurnBattle.Advance(state); Assert.True(advanced.Accepted); state = advanced.State;
                    var clock = step.GetProperty("clock"); Rng(clock, state.Battle.TurnBattle.Turns.RngState);
                    Assert.Equal(clock.GetProperty("actor").GetString(), state.Battle.TurnBattle.Turns.ActiveUnitId!.Value.Value); Assert.Equal(clock.GetProperty("atTick").GetUInt64(), state.Battle.TurnBattle.Turns.Ticks);
                }
                Assert.Equal(6, turns); break;
        }
        Assert.Equal(before, Snapshot(inputState.Battle));
    }
    private static string Events(IEnumerable<AttackResultEvent> es) => string.Join("\n", es.Select(e => e.GetType().Name + JsonSerializer.Serialize(e, e.GetType())));
    private static int? NullableIndex(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();
    private static void Check(JsonElement e, RosterFacingTurnState state, AiPriorityWorkspace w)
    {
        Rng(e, state.Battle.TurnBattle.Turns.RngState); Assert.Equal(state.Battle.TurnBattle.Turns.RngState, state.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(e.GetProperty("active").GetBoolean(), state.Battle.TurnBattle.Turns.ActiveUnitId is not null); Assert.Equal(e.GetProperty("pending").GetBoolean(), state.PendingFacing is not null);
        foreach (var u in e.GetProperty("units").EnumerateArray())
        {
            string id = u.GetProperty("id").GetString()!; var combat = state.Battle.Combatants.Single(v => v.UnitId.Value == id);
            var body = state.Battle.TurnBattle.Battle.Units.Single(v => v.Id.Value == id); var turn = state.Battle.TurnBattle.Turns.Units.Single(v => v.Id.Value == id);
            Assert.Equal(Tile(u.GetProperty("tile")), body.Position); Assert.Equal(u.GetProperty("hp").GetInt32(), combat.Hp); Assert.Equal(u.GetProperty("pa").GetInt32(), combat.PhysicalAttack);
            Assert.Equal(u.GetProperty("ko").GetInt32(), combat.KoCount); Assert.Equal(u.GetProperty("face").GetString(), combat.Facing.ToString());
            Assert.Equal(u.GetProperty("ct").GetInt32(), turn.Ct); Assert.Equal(u.GetProperty("moved").GetBoolean(), turn.MovementTaken); Assert.Equal(u.GetProperty("acted").GetBoolean(), turn.ActionTaken);
        }
        var cells = e.GetProperty("workspace").EnumerateArray().ToArray(); Assert.Equal(cells.Length, w.KnownPriorities.Count);
        foreach (var p in cells) Assert.Equal(p.GetProperty("priority").GetInt16(), w.KnownPriorities[Tile(p.GetProperty("tile"))]);
    }

    [Fact]
    public void Legacy_pair_preparation_keeps_its_supported_list_guard()
    {
        var p = BasicSkillAiListPreparationTests.Config([AiHumanAbilityKind.Dash]); var state = BasicSkillAiTestData.Ready();
        var r = BasicSkillAiListPreparation.Evaluate(state.Battle, AccumulateAiTestData.Request(), p, new(1, 7));
        Assert.False(r.Accepted); Assert.Equal(AccumulateAiPreparationRejection.UnsupportedList, r.Failure!.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dash_actual_draw_exhaustion_rolls_back_private_work_or_retains_only_accepted_Move(bool moveFirst)
    {
        using var doc = Fixture(); var d = Row(doc, moveFirst ? "complete-Dash-Move-first" : "complete-Dash-ACT-first").GetProperty("input");
        var ordinary = Load(d); var first = Command(ordinary); Assert.True(first.Accepted);
        ulong draws = first.Outcome!.DecisionRng.DrawCount - ordinary.State.Battle.TurnBattle.Turns.RngState.DrawCount;
        var data = System.Text.Json.Nodes.JsonNode.Parse(d.GetRawText())!; data["count"] = ulong.MaxValue - draws;
        using var limited = JsonDocument.Parse(data.ToJsonString()); var s = Load(limited.RootElement);
        var r = AiTurnController.Execute(s.State, s.Setup.Request, s.Configuration); Assert.False(r.Completed);
        if (!moveFirst)
        { Assert.Same(s.State, r.State); Assert.Empty(r.Events); Assert.Empty(r.Decisions); Assert.Empty(r.Workspace.KnownPriorities); }
        else
        {
            Assert.Single(r.Decisions); Assert.Equal(AiTurnStage.RetainedCommand, r.Recovery!.NextStep);
            Assert.Empty(r.Events.OfType<DashResolvedEvent>()); Assert.True(r.State.Battle.TurnBattle.Turns.Units.Single(u => u.Id == A).MovementTaken);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted, r.Failure!.ContinuationFailure!.Transition.AttackRejection);
            var retry = AiTurnController.Resume(r.State, r.Recovery); Assert.Same(r.State, retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
            var ended = RosterFacingTurnBattle.WaitWithFacing(r.State, A); Assert.True(ended.Accepted);
            Assert.Equal(AiTurnStage.RecoveryBinding, AiTurnController.Resume(ended.State, r.Recovery).Failure!.Stage);
        }
    }

    [Fact]
    public void Candidate_scoring_grants_range_and_late_clock_failure_remain_explicit_and_transactional()
    {
        using var doc = Fixture(); var s = Load(Row(doc, "ACT-first").GetProperty("input")); var c = new DashAiCandidate(A, Dash, B, new(1, 0, 0), true); var rng = s.State.Battle.TurnBattle.Turns.RngState;
        var profile = s.Configuration.Decision.Abilities.Dash!;
        var noScores = RosterDashAiCandidate.Evaluate(s.State.Battle, c, s.Configuration.Decision.Simulation, profile, new(), rng);
        Assert.Equal(RosterDashAiCandidateRejection.UnsupportedScoring, noScores.Rejection); Assert.Equal(rng, noScores.RngState);
        var noGrants = new DashAbilityBattleProfile(Ground, [new(Dash)], s.State.Battle.Combatants.Select(u => new TargetedAbilityUnitGrant(u.UnitId, [], 0)));
        Assert.Equal(RosterDashAiCandidateRejection.AbilityNotGranted, RosterDashAiCandidate.Evaluate(s.State.Battle, c, s.Configuration.Decision.Simulation, noGrants, new(true, true, true), rng).Rejection);
        Assert.Equal(RosterDashAiCandidateRejection.OutOfRange, RosterDashAiCandidate.Evaluate(s.State.Battle, c with { ActBeforeMove = false }, s.Configuration.Decision.Simulation, profile, new(true, true, true), rng).Rejection);
        var late = RosterDashAiCandidate.Evaluate(s.State.Battle, c, s.Configuration.Decision.Simulation, profile, new(true, true, true), new(1, ulong.MaxValue));
        Assert.Equal(RosterDashAiCandidateRejection.RandomDrawCountExhausted, late.Rejection); Assert.Null(late.Outcome); Assert.Null(late.Score); Assert.Equal(new RandomState(1, ulong.MaxValue), late.RngState);
        var q = Select(s, s.Configuration.Decision.Abilities with { Dash = null }); Assert.False(q.Accepted); Assert.Equal(1, q.Failure!.OccurrenceIndex); Assert.Equal(rng, q.RngState); Assert.Empty(q.Workspace.KnownPriorities);
        var old = s.State.Battle.TurnBattle;
        var ticks = new AttackBattleSnapshot(new(old.Battle, new(old.Turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed, u.Id == B ? 95 : u.Ct, u.TieKey, u.HasTurn, u.MovementTaken, u.ActionTaken)),
            old.Turns.RngState, ulong.MaxValue, old.Turns.Phase)), s.State.Battle.Combatants);
        var exhausted = RosterDashAiCandidate.Evaluate(ticks, c, s.Configuration.Decision.Simulation, profile, new(true, true, true), rng);
        Assert.Equal(RosterDashAiCandidateRejection.TickCountExhausted, exhausted.Rejection); Assert.Equal(rng, exhausted.RngState); Assert.Null(exhausted.Score);
    }
}
