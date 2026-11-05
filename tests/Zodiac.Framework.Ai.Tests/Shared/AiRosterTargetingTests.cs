using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AiRosterTargetingTests
{
    internal static readonly BattleUnitId Actor = new("actor");
    internal static AiRosterUnit Unit(string id, int order, int x = 1, int y = 1, int layer = 0,
        AiRosterLiveFacts? live = null, AiRosterDecisionFacts? decision = null, int delay = 0, bool disabled = false) =>
        new(new(id), order, new(x, y, layer), live ?? new(), decision ?? new(), delay, disabled);

    [Fact]
    public void Managed_queries_match_all_seventeen_literal_source_descriptions()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ai-roster-targeting-contract-v1.json")));
        int count = 0;
        foreach (var row in fixture.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var input = row.GetProperty("input"); var expected = row.GetProperty("expected"); var roster = ReadRoster(input);
            switch (row.GetProperty("kind").GetString())
            {
                case "membership":
                    Assert.Equal(expected.EnumerateArray().Select(e => e.GetProperty("id").GetString()), roster.Units.Select(u => u.UnitId.Value));
                    foreach (var e in expected.EnumerateArray())
                    {
                        var unit = roster.ById[new(e.GetProperty("id").GetString()!)];
                        Assert.Equal(e.GetProperty("liveUntargetable").GetBoolean(), AiRosterEligibility.IsUntargetable(unit));
                        Assert.Equal(e.GetProperty("outOfPlay").GetBoolean(), AiRosterEligibility.IsOutOfPlay(unit));
                    }
                    break;
                case "nearest":
                    var nearest = AiRosterNearest.FindEligibleEnemy(roster, input.GetProperty("targetable").EnumerateArray().Select(e => new BattleUnitId(e.GetString()!)));
                    Assert.Equal(expected.GetProperty("target").GetString(), nearest.TargetId.Value);
                    Assert.Equal(expected.GetProperty("distance").ValueKind == JsonValueKind.Null ? (int?)null : expected.GetProperty("distance").GetInt32(), nearest.Distance);
                    Assert.Equal(Strings(expected, "eligible"), nearest.EligibleUnits.Select(u => u.Value));
                    break;
                case "active":
                    var specific = input.GetProperty("specific");
                    var active = AiRosterActiveTargets.Select(roster, (AiRosterTargetFilter)input.GetProperty("mode").GetInt32(),
                        specific.ValueKind == JsonValueKind.Null ? null : new BattleUnitId(specific.GetString()!), input.GetProperty("excludeActor").GetBoolean());
                    Assert.Equal(Strings(expected, "active"), active.SelectedUnits.Select(u => u.Value));
                    Assert.Equal(Strings(expected, "inactive"), roster.Units.Where(u => !active.UnitActive[u.UnitId]).Select(u => u.UnitId.Value));
                    Assert.Equal(roster.Units.Length, active.UnitActive.Count);
                    break;
                case "proximity":
                    var result = AiRosterProximity.Evaluate(roster, input.GetProperty("scenario0").EnumerateArray().Select(ReadPosition));
                    Assert.Equal(Strings(expected, "enemies"), result.TargetableEnemies.Select(u => u.Value));
                    Assert.Equal(expected.GetProperty("writes").GetArrayLength(), result.WrittenValues.Count);
                    foreach (var write in expected.GetProperty("writes").EnumerateArray())
                        Assert.Equal(write.GetProperty("value").GetByte(), result.WrittenValues[ReadPosition(write.GetProperty("tile"))]);
                    break;
                default: throw new InvalidOperationException("Unknown authored source description.");
            }
            count++;
        }
        Assert.Equal(17, count);
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Roster_owns_source_order_and_input_storage_without_source_slot_or_compact_limits()
    {
        var units = Enumerable.Range(0, 32).Select(i => Unit(i == 0 ? "actor" : $"unit-{i}", 1000 + i,
            x: 9, y: 0, decision: new(Enemy: i > 0))).Reverse().ToArray();
        var roster = new AiTargetRoster(Actor, 0, units); units[0] = Unit("replacement", 4000);
        Assert.Equal(32, roster.Units.Length); Assert.Equal(Actor, roster.Units[0].UnitId);
        Assert.Equal("unit-31", roster.Units[^1].UnitId.Value); Assert.DoesNotContain(new BattleUnitId("replacement"), roster.ById.Keys);
        // A capacity probe outside original roster extent, not an original-playable 32-unit claim.
        Assert.Equal((byte)31, AiRosterProximity.Evaluate(roster, [new(0, 0, 0)]).WrittenValues[new(0, 0, 0)]);
    }

    [Fact]
    public void Invalid_roster_identity_order_coordinates_and_unresolved_membership_reject()
    {
        var a = Unit("actor", 0);
        foreach (var units in new AiRosterUnit[][] { [], [a, a], [a, Unit("peer", 0)], [Unit("peer", 1)], [null!] })
            Assert.Throws<ArgumentException>(() => new AiTargetRoster(Actor, 0, units));
        Assert.Throws<ArgumentException>(() => Unit("", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Unit("actor", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Unit("actor", 0, x: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Unit("actor", 0, layer: 256));
        Assert.Throws<ArgumentOutOfRangeException>(() => Unit("actor", 0, delay: -1));
        var roster = new AiTargetRoster(Actor, 0, [a]);
        Assert.Throws<ArgumentException>(() => AiRosterNearest.FindEligibleEnemy(roster, [new("foreign")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiRosterProximity.Evaluate(roster, [new(256, 0, 0)]));
    }

    [Fact]
    public void Enemy_classification_uses_actor_current_team_target_initial_team_and_actor_override()
    {
        for (int current = 0; current < 4; current++)
        for (int initial = 0; initial < 4; initial++)
        {
            Assert.Equal(current != initial, AiRosterEligibility.IsEnemy(Actor, current, new("target"), initial));
            Assert.False(AiRosterEligibility.IsEnemy(Actor, current, Actor, initial));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => AiRosterEligibility.IsEnemy(Actor, 4, new("target"), 0));
        Assert.Equal("targetInitialTeam", Assert.Throws<ArgumentOutOfRangeException>(() =>
            AiRosterEligibility.IsEnemy(Actor, 0, new("target"), -1)).ParamName);
    }

    [Fact]
    public void Ordinary_delay_rounds_up_ready_units_are_zero_and_zero_speed_is_infinite()
    {
        Assert.Equal(15, AiRosterEligibility.OrdinaryTurnDelay(0, 7));
        Assert.Equal(1, AiRosterEligibility.OrdinaryTurnDelay(99, 255));
        Assert.Equal(0, AiRosterEligibility.OrdinaryTurnDelay(100, 1));
        Assert.Equal(0, AiRosterEligibility.OrdinaryTurnDelay(254, 1));
        Assert.Equal(int.MaxValue, AiRosterEligibility.OrdinaryTurnDelay(100, 0));
    }

    [Fact]
    public void Nearest_preserves_first_source_order_extreme_coordinates_and_duplicate_working_membership()
    {
        var a = Unit("actor", 3, 0, 0);
        var first = Unit("zulu", 1, 255, 255, 200, decision: new(Enemy: true, Considered: true));
        var later = Unit("alpha", 2, 255, 255, decision: new(Enemy: true, Considered: true));
        var roster = new AiTargetRoster(Actor, 0, [later, a, first]); var members = new[] { first.UnitId, later.UnitId, first.UnitId };
        var result = AiRosterNearest.FindEligibleEnemy(roster, members); members[0] = Actor;
        Assert.Equal(first.UnitId, result.TargetId); Assert.Equal(510, result.Distance); Assert.False(result.UsedActorFallback);
        Assert.Equal(new[] { first.UnitId, later.UnitId }, result.EligibleUnits);
        Assert.True(AiRosterNearest.FindEligibleEnemy(roster, []).UsedActorFallback);
    }

    [Fact]
    public void Active_masks_rebuild_without_leaking_prior_selection_and_accept_unknown_signed_modes()
    {
        var peer = Unit("peer", 1, decision: new(Enemy: true, Considered: true));
        var roster = new AiTargetRoster(Actor, 0, [Unit("actor", 0, decision: new(Considered: true)), peer]);
        var prior = AiRosterActiveTargets.Select(roster, AiRosterTargetFilter.Any);
        var missing = AiRosterActiveTargets.Select(roster, AiRosterTargetFilter.Specific, new("missing"));
        Assert.False(missing.AnyActive); Assert.All(missing.UnitActive, flag => Assert.False(flag.Value)); Assert.True(prior.AnyActive);
        foreach (int mode in new[] { int.MinValue, -1, 5, int.MaxValue })
            Assert.Equal(prior.SelectedUnits.ToArray(), AiRosterActiveTargets.Select(roster, (AiRosterTargetFilter)mode).SelectedUnits.ToArray());
        Assert.Empty(AiRosterActiveTargets.Select(roster, AiRosterTargetFilter.Specific, Actor, true).SelectedUnits);
    }

    [Fact]
    public void Proximity_sums_many_enemies_at_source_extent_on_both_layers_and_returns_only_mask_writes()
    {
        var units = new List<AiRosterUnit> { Unit("actor", 0, 0, 0) };
        for (int i = 0; i < 20; i++) units.Add(Unit($"enemy-{i}", i + 1, i % 10, i / 10 + 1,
            decision: new(Enemy: true), delay: 2));
        var roster = new AiTargetRoster(Actor, 2, units);
        var result = AiRosterProximity.Evaluate(roster, [new(0, 0, 1), new(0, 0, 0), new(0, 0, 1)]);
        Assert.Equal(20, result.TargetableEnemies.Length); Assert.Equal(2, result.WrittenValues.Count);
        Assert.Equal((byte)81, result.WrittenValues[new(0, 0, 0)]); Assert.Equal((byte)81, result.WrittenValues[new(0, 0, 1)]);
        Assert.False(result.WrittenValues.ContainsKey(new(1, 0, 0))); Assert.Empty(AiRosterProximity.Evaluate(roster, []).WrittenValues);
    }

    [Fact]
    public void Focus_active_targets_and_proximity_use_distinct_memberships_on_one_immutable_roster()
    {
        var unconsidered = Unit("unconsidered", 1, 2, 1, decision: new(Enemy: true), delay: 2);
        var later = Unit("later", 2, 4, 1, decision: new(Enemy: true, Considered: true), delay: 3);
        var roster = new AiTargetRoster(Actor, 2, [Unit("actor", 0, decision: new(Considered: true)), later, unconsidered]);
        var active = AiRosterActiveTargets.Select(roster, AiRosterTargetFilter.Any);
        var nearest = AiRosterNearest.FindEligibleEnemy(roster, active.SelectedUnits);
        var proximity = AiRosterProximity.Evaluate(roster, [new(1, 1, 0)]);
        Assert.Equal(later.UnitId, nearest.TargetId); Assert.Equal(new[] { unconsidered.UnitId }, proximity.TargetableEnemies);
        Assert.Equal((byte)9, proximity.WrittenValues[new(1, 1, 0)]); Assert.False(active.UnitActive[unconsidered.UnitId]);
    }

    private static AiTargetRoster ReadRoster(JsonElement data)
    {
        var units = data.GetProperty("units").EnumerateArray().Select(u => new AiRosterUnit(
            new(u.GetProperty("id").GetString()!), u.GetProperty("order").GetInt32(), ReadPosition(u.GetProperty("position")),
            new(Bool(u, "present", true), Bool(u, "crystal"), Bool(u, "treasure"), Bool(u, "mount"), Bool(u, "transparent"),
                Bool(u, "rider"), Bool(u, "riderMountTransparent"), Bool(u, "petrified"), Bool(u, "bloodSuck"), Bool(u, "confused"),
                Bool(u, "dead"), Bool(u, "critical"), Bool(u, "deathSentence")),
            new(Bool(u, "enemy"), Bool(u, "untargetable"), Bool(u, "deadWithoutReraise"), Bool(u, "considered")),
            u.TryGetProperty("delay", out var delay) ? delay.GetInt32() : 0, Bool(u, "disabledThroughActorDelay")));
        return new(new(data.GetProperty("actor").GetString()!), data.GetProperty("cachedActorDelay").GetByte(), units);
    }
    private static bool Bool(JsonElement u, string key, bool fallback = false) => u.TryGetProperty(key, out var value) ? value.GetBoolean() : fallback;
    private static TilePosition ReadPosition(JsonElement p) => new(p[0].GetInt32(), p[1].GetInt32(), p[2].GetInt32());
    private static IEnumerable<string?> Strings(JsonElement e, string key) => e.GetProperty(key).EnumerateArray().Select(v => v.GetString());
}
