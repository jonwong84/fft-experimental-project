using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiSessionTestData;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterAiBattleReplayTests
{
    [Theory]
    [InlineData("three-actors-mixed-supported-lists")]
    [InlineData("retained-Stone-and-shared-priorities")]
    [InlineData("allied-empty-list-clock-ties")]
    public void Six_actual_turns_preserve_all_peers_priority_history_and_explicit_clock_boundaries(string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "roster-replay-contract-v1.json")));
        var row = doc.RootElement.GetProperty("vectors").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        var s = Load(row.GetProperty("input"), allSwordBindings: true);
        var alternate = Load(row.GetProperty("input"), reverse: true, allSwordBindings: true);
        var state = s.State; var other = alternate.State; var original = Snapshot(state.Battle);
        var workspace = AiPriorityWorkspace.StartUnknown(state.Battle.TurnBattle.Battle.Map);
        var otherWorkspace = AiPriorityWorkspace.StartUnknown(other.Battle.TurnBattle.Battle.Map);
        int turns = 0;
        foreach (var step in row.GetProperty("expected").EnumerateArray())
        {
            var actor = new BattleUnitId(step.GetProperty("actor").GetString()!);
            Assert.Equal(actor, state.Battle.TurnBattle.Turns.ActiveUnitId);
            var request = s.Setup.Request with { ActorId = actor };
            var before = Snapshot(state.Battle);
            var result = RosterBasicSkillAiTurnExecution.Execute(state, request, s.Configuration, workspace);
            Assert.True(result.Completed, result.Failure?.ToString()); Assert.Null(result.Recovery); Assert.Null(result.State.PendingFacing);
            Assert.Equal(before, Snapshot(state.Battle));
            Check(step.GetProperty("turn"), result.State, result.Workspace);
            var replay = RosterBasicSkillAiTurnExecution.Execute(other, request, alternate.Configuration, otherWorkspace);
            Assert.True(replay.Completed); Assert.Equal(Snapshot(result.State.Battle), Snapshot(replay.State.Battle));
            Assert.Equal(JsonSerializer.Serialize(result.Events), JsonSerializer.Serialize(replay.Events));
            Assert.Equal(result.Decisions.Select(d => (d.Kind, d.AfterAct, d.DecisionRng)), replay.Decisions.Select(d => (d.Kind, d.AfterAct, d.DecisionRng)));
            state = result.State; other = replay.State; workspace = result.Workspace; otherWorkspace = replay.Workspace; turns++;
            if (step.GetProperty("clock").ValueKind == JsonValueKind.Null) continue;
            var clock = step.GetProperty("clock"); var advanced = RosterFacingTurnBattle.Advance(state); Assert.True(advanced.Accepted);
            var events = advanced.Events.OfType<AttackTurnBattleEvent>().Select(e => e.Event).OfType<TurnBattleClockEvent>().Select(e => e.Event).ToArray();
            Assert.Equal(clock.GetProperty("ticks").GetInt32(), events.OfType<TurnClockAdvancedEvent>().Count());
            var ready = Assert.Single(events.OfType<TurnReadyEvent>());
            Assert.Equal(clock.GetProperty("actor").GetString(), ready.UnitId.Value);
            Assert.Equal(clock.GetProperty("before").GetInt32(), ready.CtBeforeSelection); Assert.Equal(clock.GetProperty("ct").GetInt32(), ready.CtAfterSelection);
            Assert.Equal(clock.GetProperty("raw").GetUInt32(), ready.UpkeepRoll);
            Assert.Equal(clock.GetProperty("atTick").GetUInt64(), advanced.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(new RandomState(clock.GetProperty("word").GetUInt32(), clock.GetProperty("count").GetUInt64()), advanced.State.Battle.TurnBattle.Turns.RngState);
            var otherAdvance = RosterFacingTurnBattle.Advance(other); Assert.True(otherAdvance.Accepted);
            Assert.Equal(Snapshot(advanced.State.Battle), Snapshot(otherAdvance.State.Battle));
            Assert.Equal(JsonSerializer.Serialize(advanced.Events), JsonSerializer.Serialize(otherAdvance.Events));
            state = advanced.State; other = otherAdvance.State;
        }
        Assert.Equal(6, turns); Assert.Equal(original, Snapshot(s.State.Battle));
        Assert.Equal(TurnCyclePhase.PickUnit, state.Battle.TurnBattle.Turns.Phase);
    }

    private static void Check(JsonElement e, RosterFacingTurnState state, AiPriorityWorkspace workspace)
    {
        Assert.Equal(JsonValueKind.Null, e.GetProperty("failure").ValueKind);
        Assert.Null(state.Battle.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), state.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(e.GetProperty("ticks").GetUInt64(), state.Battle.TurnBattle.Turns.Ticks);
        foreach (var unit in e.GetProperty("units").EnumerateArray())
        {
            var id = new BattleUnitId(unit.GetProperty("id").GetString()!);
            var body = state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == id);
            var turn = state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == id);
            var combat = state.Battle.Combatants.Single(u => u.UnitId == id);
            var p = unit.GetProperty("tile"); Assert.Equal(new TilePosition(p[0].GetInt32(), p[1].GetInt32(), 0), body.Position);
            Assert.Equal(unit.GetProperty("hp").GetInt32(), combat.Hp); Assert.Equal(unit.GetProperty("pa").GetInt32(), combat.PhysicalAttack);
            Assert.Equal(unit.GetProperty("ko").GetInt32(), combat.KoCount); Assert.Equal(unit.GetProperty("face").GetString(), combat.Facing.ToString());
            Assert.Equal(unit.GetProperty("ct").GetInt32(), turn.Ct); Assert.Equal(unit.GetProperty("moved").GetBoolean(), turn.MovementTaken); Assert.Equal(unit.GetProperty("acted").GetBoolean(), turn.ActionTaken);
        }
        var priorities = e.GetProperty("workspace").EnumerateArray().ToArray(); Assert.Equal(priorities.Length, workspace.KnownPriorities.Count);
        foreach (var p in priorities)
        {
            var t = p.GetProperty("tile"); Assert.Equal(p.GetProperty("priority").GetInt16(), workspace.KnownPriorities[new(t[0].GetInt32(), t[1].GetInt32(), 0)]);
        }
    }
}
