using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests.Saves;

public sealed class TwoUnitBattleSaveTests
{
    public static IEnumerable<object[]> Checkpoints => Enumerable.Range(0, 13).Select(index => new object[] { index });

    [Theory]
    [MemberData(nameof(Checkpoints))]
    public void Every_command_boundary_restores_full_state_and_identical_remaining_events(int checkpoint)
    {
        var source = SyntheticTwoUnitBattle.CreateSaveProfile();
        var uninterrupted = At(source, checkpoint);
        string json = TwoUnitBattleSave.ToJson(source, uninterrupted);
        var selected = SyntheticTwoUnitBattle.CreateSaveProfile(); // Fresh definitions; no source object graph retained.
        var loaded = TwoUnitBattleSave.ReadJson(selected, json);
        Assert.False(loaded.WasMigrated);
        Assert.Equal(2, loaded.SourceSchemaVersion);
        var resumed = loaded.State;
        Assert.Same(selected.Definition, resumed.Definition);
        Assert.Equal(Snapshot(uninterrupted), Snapshot(resumed));
        Assert.Equal(json, TwoUnitBattleSave.ToJson(selected, resumed));
        Assert.All(resumed.Battle.TurnBattle.Battle.Units, unit => Assert.Same(
            selected.Definition.InitialBattle.TurnBattle.Battle.Units.Single(template => template.Id == unit.Id).MovementProfile, unit.MovementProfile));
        Assert.Same(selected.Definition.InitialBattle.TurnBattle.Battle.Map, resumed.Battle.TurnBattle.Battle.Map);
        for (int index = checkpoint; index < SyntheticTwoUnitBattle.Commands.Length; index++)
        {
            var expected = TwoUnitBattleScenario.Apply(uninterrupted, SyntheticTwoUnitBattle.Commands[index]);
            var actual = TwoUnitBattleScenario.Apply(resumed, SyntheticTwoUnitBattle.Commands[index]);
            Assert.True(expected.Accepted); Assert.True(actual.Accepted);
            Assert.Equal(expected.Events.ToArray(), actual.Events.ToArray());
            Assert.Equal(Snapshot(expected.State), Snapshot(actual.State));
            uninterrupted = expected.State; resumed = actual.State;
        }
        Assert.Equal(TwoUnitBattleOutcome.FirstUnitWon, resumed.Outcome);
        Assert.Equal(new FftRandomState(2473755763, 22), resumed.Battle.TurnBattle.Turns.RngState);
        var rejected = TwoUnitBattleScenario.Apply(resumed, new TwoUnitBattleCommand.Advance());
        Assert.False(rejected.Accepted); Assert.Equal(TwoUnitBattleRejection.AlreadyCompleted, rejected.ScenarioRejection);
        Assert.Same(resumed, rejected.State); Assert.Empty(rejected.Events);
    }

    [Fact]
    public void Rejected_attempts_do_not_change_save_bytes_or_rng_and_array_order_does_not_select_turn_order()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        var active = At(profile, 1);
        string before = TwoUnitBattleSave.ToJson(profile, active);
        var rejected = TwoUnitBattleScenario.Apply(active, new TwoUnitBattleCommand.Advance());
        Assert.False(rejected.Accepted);
        Assert.Equal(before, TwoUnitBattleSave.ToJson(profile, rejected.State));
        var root = JsonNode.Parse(before)!.AsObject();
        var units = root["snapshot"]!["battle"]!["units"]!.AsArray();
        var first = units[0]!.DeepClone(); var second = units[1]!.DeepClone();
        units.Clear(); units.Add(second); units.Add(first);
        Rehash(root);
        Assert.Equal(before, TwoUnitBattleSave.ToJson(profile, TwoUnitBattleSave.ReadJson(profile, root.ToJsonString()).State));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Versioned_synthetic_fixtures_resume_after_action_with_movement_still_available(int version)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves", $"two-unit-active-v{version}.json"));
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        var loaded = TwoUnitBattleSave.ReadJson(profile, json);
        Assert.Equal(version, loaded.SourceSchemaVersion);
        Assert.Equal(version == 1, loaded.WasMigrated);
        Assert.Equal(Snapshot(At(profile, 5)), Snapshot(loaded.State));
        var b = loaded.State.Battle.TurnBattle.Turns.Units.Single(unit => unit.Id == SyntheticTwoUnitBattle.SecondUnitId);
        Assert.True(b.HasTurn); Assert.True(b.ActionTaken); Assert.False(b.MovementTaken);
        string current = TwoUnitBattleSave.ToJson(profile, loaded.State);
        Assert.Equal(2, JsonNode.Parse(current)!["schemaVersion"]!.GetValue<int>());
        Assert.Contains(FftBiosRandom.AlgorithmId, current);
        Assert.Equal(Snapshot(loaded.State), Snapshot(TwoUnitBattleSave.ReadJson(profile, current).State));
    }

    public static IEnumerable<object[]> InvalidSnapshots()
    {
        yield return Case("profile ID", node => node["profileId"] = "other:profile/battle");
        yield return Case("profile version", node => node["profileVersion"] = 2);
        yield return Case("content version", node => node["contentVersion"] = 2);
        yield return Case("scenario ID", node => node["scenarioId"] = "sample:scenario/missing");
        yield return Case("RNG algorithm", node => node["rngAlgorithmId"] = "other:rng/unknown");
        yield return Case("missing profile", node => node.Remove("profileId"));
        yield return Case("null profile", node => node["profileId"] = null);
        yield return Case("invalid ID", node => node["profileId"] = "BAD");
        yield return Case("long ID", node => node["profileId"] = "sample:" + new string('a', 256));
        yield return Case("unknown field", node => node["graphType"] = "Zodiac.Framework.BattleState");
        yield return Case("null battle", node => node["battle"] = null);
        yield return Battle("unknown phase", node => node["phase"] = "future-phase");
        yield return Battle("active/phase mismatch", node => node["phase"] = "pick-unit");
        yield return Battle("missing active unit", node => node["activeUnitId"] = "sample:unit/absent");
        yield return Battle("no active unit", node => node["activeUnitId"] = null);
        yield return Battle("illegal AddCt", node => { node["phase"] = "add-ct"; node["activeUnitId"] = null; });
        yield return Battle("outcome mismatch", node => node["outcome"] = "first-unit-won");
        yield return Battle("unknown outcome", node => node["outcome"] = "draw");
        yield return Battle("negative ticks", node => node["ticks"] = -1);
        yield return Battle("RNG overflow", node => node["rngWord"] = (ulong)uint.MaxValue + 1);
        yield return Battle("draw overflow", node => node["rngDrawCount"] = "18446744073709551616");
        yield return Battle("missing unit", node => node["units"]!.AsArray().RemoveAt(0));
        yield return Battle("null units", node => node["units"] = null);
        yield return Battle("null unit", node => node["units"]![0] = null);
        yield return Battle("unknown unit", node => node["units"]![0]!["id"] = "sample:unit/absent");
        yield return Battle("duplicate unit", node => node["units"]![1]!["id"] = node["units"]![0]!["id"]!.GetValue<string>());
        yield return Unit("weapon missing", node => node["weaponId"] = "sample:item/absent");
        yield return Unit("HP range", node => node["hp"] = 51);
        yield return Unit("MP unsupported", node => node["mp"] = 1);
        yield return Unit("HP/status mismatch", node => node["status"] = "dead");
        yield return Unit("unknown status", node => node["status"] = "poison");
        yield return Unit("unknown facing", node => node["facing"] = "northeast");
        yield return Unit("off map", node => node["x"] = 2);
        yield return Unit("overlapping units", node => node["x"] = 1);
        yield return Unit("unsupported layer", node => node["layer"] = 1);
        yield return Unit("CT range", node => node["ct"] = 255);
        yield return Unit("invented prior KO", node => node["koCount"] = 1);
        yield return Battle("active CT not reduced", node => node["units"]![1]!["ct"] = 100);
        yield return Battle("active choices consumed", node => node["units"]![1]!["movementTaken"] = true);
        yield return Battle("dead active unit", node => { var unit = node["units"]![1]!; unit["hp"] = 0; unit["status"] = "dead"; unit["koCount"] = 1; });
        yield return Battle("both dead", node =>
        {
            node["activeUnitId"] = null; node["phase"] = "pick-unit";
            foreach (var unit in node["units"]!.AsArray()) { unit!["hp"] = 0; unit["status"] = "dead"; unit["koCount"] = 1; }
        });
        yield return Unit("missing choice", node => node.Remove("actionTaken"));
        yield return Unit("numeric boolean", node => node["actionTaken"] = 1);

        static object[] Case(string name, Action<JsonObject> change) => [name, change];
        static object[] Battle(string name, Action<JsonObject> change) => Case(name, root => change(root["battle"]!.AsObject()));
        static object[] Unit(string name, Action<JsonObject> change) => Battle(name, battle => change(battle["units"]![0]!.AsObject()));
    }

    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void Invalid_or_unresolved_snapshot_data_is_rejected_even_with_a_matching_checksum(string name, Action<JsonObject> change)
    {
        _ = name;
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        var state = At(profile, 5);
        string before = Snapshot(state);
        var root = JsonNode.Parse(TwoUnitBattleSave.ToJson(profile, state))!.AsObject();
        change(root["snapshot"]!.AsObject()); Rehash(root);
        Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.ReadJson(profile, root.ToJsonString()));
        Assert.Equal(before, Snapshot(state));
    }

    [Theory]
    [InlineData("format")]
    [InlineData("future")]
    [InlineData("old")]
    [InlineData("checksum")]
    [InlineData("corrupt")]
    [InlineData("duplicate")]
    [InlineData("nested-duplicate")]
    [InlineData("unknown-envelope")]
    [InlineData("missing-envelope")]
    [InlineData("null-snapshot")]
    [InlineData("null")]
    [InlineData("deep")]
    [InlineData("budget")]
    public void Corruption_unknown_versions_and_non_strict_json_fail_clearly(string problem)
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        string json = TwoUnitBattleSave.ToJson(profile, At(profile, 5));
        var root = JsonNode.Parse(json)!.AsObject();
        switch (problem)
        {
            case "format": root["format"] = "other.save"; break;
            case "future": root["schemaVersion"] = 3; break;
            case "old": root["schemaVersion"] = 0; break;
            case "checksum": root["snapshot"]!["battle"]!["units"]![0]!["hp"] = 24; break;
            case "unknown-envelope": root["objectGraph"] = true; break;
            case "missing-envelope": root.Remove("sha256"); break;
            case "null-snapshot": root["snapshot"] = null; root["sha256"] = Convert.ToHexStringLower(SHA256.HashData("null"u8)); break;
        }
        json = problem switch
        {
            "corrupt" => json[..^5],
            "duplicate" => json.Replace("\"schemaVersion\":2", "\"schemaVersion\":2,\"schemaVersion\":2"),
            "nested-duplicate" => json.Replace("\"hp\":25", "\"hp\":25,\"hp\":25"),
            "null" => "null",
            "deep" => new string('[', 20) + "0" + new string(']', 20),
            "budget" => new string('é', TwoUnitBattleSave.MaxJsonBytes / 2 + 1),
            _ => root.ToJsonString(),
        };
        var error = Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.ReadJson(profile, json));
        Assert.NotEmpty(error.Message);
        if (problem == "checksum") Assert.Contains("SHA-256", error.Message);
        if (problem is "future" or "old") Assert.Contains("Unsupported save schema", error.Message);
    }

    [Fact]
    public void Saves_are_explicit_state_and_ids_and_keep_full_unsigned_counter_widths()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        var root = JsonNode.Parse(TwoUnitBattleSave.ToJson(profile, At(profile, 5)))!.AsObject();
        var battle = root["snapshot"]!["battle"]!;
        battle["ticks"] = ulong.MaxValue;
        battle["rngDrawCount"] = ulong.MaxValue;
        battle["rngWord"] = uint.MaxValue; Rehash(root);
        var state = TwoUnitBattleSave.ReadJson(profile, root.ToJsonString()).State;
        Assert.Equal(ulong.MaxValue, state.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(new FftRandomState(uint.MaxValue, ulong.MaxValue), state.Battle.TurnBattle.Turns.RngState);
        string json = TwoUnitBattleSave.ToJson(profile, state);
        foreach (string field in new[] { "initialBattle", "movementProfile", "maxHp", "physicalAttack", "weapons", "tieKey", "speed", "journal", "$type" })
            Assert.DoesNotContain($"\"{field}\"", json);
        Assert.Equal(Snapshot(state), Snapshot(TwoUnitBattleSave.ReadJson(profile, json).State));
    }

    [Fact]
    public void Definition_restore_rejects_foreign_map_and_changed_turn_inputs()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        var foreign = SyntheticTwoUnitBattle.Create();
        Assert.Throws<ArgumentException>(() => profile.Definition.Restore(foreign.InitialBattle));
        var initial = profile.Definition.InitialBattle;
        var turns = initial.TurnBattle.Turns;
        var changedTurns = new TurnCycleState(turns.Units.Select(unit => new TurnUnitState(unit.Id, unit.Speed + 1, unit.Ct, unit.TieKey)), turns.RngState);
        Assert.Throws<ArgumentException>(() => profile.Definition.Restore(new(new(initial.TurnBattle.Battle, changedTurns), initial.Combatants)));
        Assert.Throws<BattleSaveValidationException>(() => TwoUnitBattleSave.ToJson(profile, foreign.Start()));
    }

    [Fact]
    public void Second_unit_victory_with_an_unfinished_active_turn_restores_without_forcing_Wait()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        var state = At(profile, 5);
        foreach (var command in new TwoUnitBattleCommand[] { new TwoUnitBattleCommand.Wait(SyntheticTwoUnitBattle.SecondUnitId),
            new TwoUnitBattleCommand.Advance(), new TwoUnitBattleCommand.Attack(SyntheticTwoUnitBattle.SecondUnitId, SyntheticTwoUnitBattle.FirstUnitId) })
        {
            var result = TwoUnitBattleScenario.Apply(state, command);
            Assert.True(result.Accepted); state = result.State;
        }
        Assert.Equal(TwoUnitBattleOutcome.SecondUnitWon, state.Outcome);
        Assert.Equal(SyntheticTwoUnitBattle.SecondUnitId, state.Battle.TurnBattle.Turns.ActiveUnitId);
        var loaded = TwoUnitBattleSave.ReadJson(profile, TwoUnitBattleSave.ToJson(profile, state)).State;
        Assert.Equal(Snapshot(state), Snapshot(loaded));
        Assert.Equal(TwoUnitBattleRejection.AlreadyCompleted, TwoUnitBattleScenario.Apply(loaded,
            new TwoUnitBattleCommand.Wait(SyntheticTwoUnitBattle.SecondUnitId)).ScenarioRejection);
    }

    [Fact]
    public void Critical_result_state_round_trips_with_its_HP_projection()
    {
        var profile = SyntheticTwoUnitBattle.CreateSaveProfile();
        var root = JsonNode.Parse(TwoUnitBattleSave.ToJson(profile, At(profile, 5)))!.AsObject();
        root["snapshot"]!["battle"]!["units"]![0]!["hp"] = 10;
        root["snapshot"]!["battle"]!["units"]![0]!["status"] = "critical";
        Rehash(root);
        var state = TwoUnitBattleSave.ReadJson(profile, root.ToJsonString()).State;
        var combat = state.Battle.Combatants.Single(unit => unit.UnitId == SyntheticTwoUnitBattle.FirstUnitId);
        Assert.Equal(10, combat.Hp); Assert.Equal(AttackUnitStatus.Critical, combat.Status);
        Assert.Equal(Snapshot(state), Snapshot(TwoUnitBattleSave.ReadJson(profile, TwoUnitBattleSave.ToJson(profile, state)).State));
    }

    internal static TwoUnitBattleState At(TwoUnitBattleSaveProfile profile, int count)
    {
        var state = profile.Definition.Start();
        foreach (var command in SyntheticTwoUnitBattle.Commands.Take(count))
        { var result = TwoUnitBattleScenario.Apply(state, command); Assert.True(result.Accepted); state = result.State; }
        return state;
    }

    internal static string Snapshot(TwoUnitBattleState state) => JsonSerializer.Serialize(new
    {
        state.Outcome, state.WinnerId, state.Battle.Combatants,
        units = state.Battle.TurnBattle.Battle.Units.Select(unit => new
        {
            unit.Id, unit.Position, unit.HasMoved, unit.MovementProfile.Move, unit.MovementProfile.Jump,
            unit.MovementProfile.BodyHeight, unit.MovementProfile.Mode, unit.MovementProfile.Traits,
            costs = unit.MovementProfile.TerrainCosts.OrderBy(item => item.Key.Value, StringComparer.Ordinal)
                .Select(item => new { id = item.Key.Value, item.Value }),
        }),
        state.Battle.TurnBattle.Turns,
        state.Battle.TurnBattle.Battle.RngState,
    });

    private static void Rehash(JsonObject root) => root["sha256"] = Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(root["snapshot"]!.ToJsonString())));
}
