using System.Text.Json;
using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Tests;

internal static class SpellTargetTestData
{
    internal static SpellTargetOccupant Occupant(JsonElement u) => new(new(u.GetProperty("id").GetString()!),
        u.GetProperty("order").GetInt32(), Position(u.GetProperty("position")), (SpellTargetTeam)u.GetProperty("initial").GetInt32(),
        (SpellTargetTeam)u.GetProperty("current").GetInt32(), u.GetProperty("active").GetBoolean(), u.GetProperty("crystal").GetBoolean(),
        u.GetProperty("treasure").GetBoolean(), u.GetProperty("jump").GetBoolean(), u.GetProperty("mount").GetBoolean());
    internal static TilePosition Position(JsonElement p) => new(p[0].GetInt32(), p[1].GetInt32(), p[2].GetInt32());
    internal static BattleMapDefinition Map(JsonElement m)
    {
        int w = m.GetProperty("width").GetInt32(), d = m.GetProperty("depth").GetInt32(), ls = m.GetProperty("layers").GetInt32();
        var ts = Enumerable.Range(0, w * d * ls).Select(i => new BattleTile(TerrainSurfaceId.Unclassified, 10,
            movementFlags: i >= w * d ? BattleTileMovementFlags.Blocked : 0)).ToArray();
        foreach (var r in m.GetProperty("overrides").EnumerateArray())
        { var p = Position(r.GetProperty("position")); ts[(p.Layer * d + p.Y) * w + p.X] = Tile(r.GetProperty("tile")); }
        return new(w, d, ls, ts);
    }
    internal static DefaultAbilityGeometry Geometry(JsonElement g) => new(g.GetProperty("range").GetByte(),
        g.GetProperty("area").GetByte(), g.GetProperty("vertical").GetByte(), g.GetProperty("rangeVertical").GetBoolean(),
        g.GetProperty("cannotTargetSelf").GetBoolean(), g.GetProperty("cannotHitCaster").GetBoolean(),
        g.GetProperty("topDown").GetBoolean(), g.GetProperty("allies").GetBoolean(), g.GetProperty("enemies").GetBoolean());
    internal static SpellTargetingProfile Profile(JsonElement p) => new(p.GetProperty("ordinary").GetBoolean(),
        p.GetProperty("noSpecial").GetBoolean(), p.GetProperty("noCross").GetBoolean(), p.GetProperty("padding").GetBoolean());
    internal static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "Fixtures", "spell-targeting-contract-v1.json")));
    internal static BattleTile Tile(JsonElement t) => new(TerrainSurfaceId.Unclassified, t.GetProperty("h").GetInt32(),
        t.GetProperty("half").GetInt32(), t.GetProperty("water").GetInt32(), movementFlags:
        (t.GetProperty("blocked").GetBoolean() ? BattleTileMovementFlags.Blocked : 0) |
        (t.GetProperty("untargetable").GetBoolean() ? BattleTileMovementFlags.Untargetable : 0));
}

[Trait("Ruleset", "OriginalFft")]
public sealed class DefaultAbilityTargetingTests
{
    [Fact]
    public void Literal_rosters_preserve_team_panel_mutations_targetability_and_source_order()
    {
        using var doc = SpellTargetTestData.Fixture();
        foreach (var r in doc.RootElement.GetProperty("rosters").EnumerateArray())
        {
            var area = DefaultAbilityTargeting.Area(SpellTargetTestData.Map(r.GetProperty("map")),
                SpellTargetTestData.Position(r.GetProperty("caster")), SpellTargetTestData.Position(r.GetProperty("aim")),
                SpellTargetTestData.Geometry(r.GetProperty("geometry")), SpellTargetTestData.Profile(r.GetProperty("profile"))).Outcome!;
            var roster = r.GetProperty("units").EnumerateArray().Select(SpellTargetTestData.Occupant).ToImmutableArray();
            var actual = DefaultAbilityTargeting.SelectTargets(area, new(r.GetProperty("casterId").GetString()!), roster,
                new(r.GetProperty("complete").GetBoolean()), r.GetProperty("nearest").GetBoolean());
            Assert.True(actual.Accepted); var e = r.GetProperty("expected");
            Assert.Equal(e.GetProperty("ids").EnumerateArray().Select(p => p.GetString()), actual.Outcome!.UnitIds.Select(p => p.Value));
            Assert.Equal(e.GetProperty("panels").EnumerateArray().Select(SpellTargetTestData.Position), actual.Outcome.Panels);
        }
    }

    [Fact]
    public void Incomplete_conflicting_or_blue_rosters_reject_without_a_selection()
    {
        var map = new BattleMapDefinition(1, 1, 1, [new(0)]); var g = new DefaultAbilityGeometry(0, 0, 0);
        var area = DefaultAbilityTargeting.Area(map, default, default, g, new(true, true, true, true)).Outcome!;
        var a = new SpellTargetOccupant(new("actor"), 0, default, SpellTargetTeam.Blue, SpellTargetTeam.Blue);
        ImmutableArray<SpellTargetOccupant>[] invalid = [default, [], [a, a], [a with { SourceOrder = -1 }],
            [a with { Position = new(2, 0, 0) }], [a with { Mount = true }], [a with { InitialTeam = (SpellTargetTeam)4 }]];
        foreach (var roster in invalid)
        { var r = DefaultAbilityTargeting.SelectTargets(area, a.UnitId, roster, new(true)); Assert.False(r.Accepted); Assert.Null(r.Outcome); }
        Assert.False(DefaultAbilityTargeting.SelectTargets(area, a.UnitId, [a], null).Accepted);
        var blue = DefaultAbilityTargeting.Range(map, default, g, new(true, true, true, true)).Outcome!;
        Assert.False(DefaultAbilityTargeting.SelectTargets(blue, a.UnitId, [a], new(true)).Accepted);
    }

    [Fact]
    public void Literal_panel_vectors_preserve_blue_green_height_and_collision_boundaries()
    {
        using var doc = SpellTargetTestData.Fixture();
        foreach (var r in doc.RootElement.GetProperty("panels").EnumerateArray())
        {
            var map = SpellTargetTestData.Map(r.GetProperty("map")); var g = SpellTargetTestData.Geometry(r.GetProperty("geometry"));
            var caster = SpellTargetTestData.Position(r.GetProperty("caster")); var profile = SpellTargetTestData.Profile(r.GetProperty("profile"));
            var actual = r.GetProperty("kind").GetString() == "Range" ? DefaultAbilityTargeting.Range(map, caster, g, profile) :
                DefaultAbilityTargeting.Area(map, caster, SpellTargetTestData.Position(r.GetProperty("aim")), g, profile);
            var expected = r.GetProperty("expected"); Assert.Equal(expected.GetProperty("accepted").GetBoolean(), actual.Accepted);
            if (!actual.Accepted) { Assert.Null(actual.Outcome); continue; }
            Assert.Equal(expected.GetProperty("panels").EnumerateArray().Select(SpellTargetTestData.Position), actual.Outcome!.Panels);
        }
    }

    [Fact]
    public void Map_query_rejects_unknown_profiles_and_non_source_tile_widths_without_panels()
    {
        var m = new BattleMapDefinition(1, 1, 1, [new(256)]); var g = new DefaultAbilityGeometry(1, 1, 1);
        Assert.Equal(SpellTargetingRejection.UnsupportedMap, DefaultAbilityTargeting.Range(m, default, g, new(true, true, true, true)).Rejection);
        Assert.Equal(SpellTargetingRejection.UnsupportedContext, DefaultAbilityTargeting.Range(m, default, g, null).Rejection);
    }

    [Fact]
    public void Literal_reference_and_column_vectors_preserve_source_width_sentinels_and_ties()
    {
        using var doc = SpellTargetTestData.Fixture();
        foreach (var r in doc.RootElement.GetProperty("references").EnumerateArray())
            Assert.Equal(r.GetProperty("expected").GetByte(), DefaultAbilityTargeting.ReferenceHeight(SpellTargetTestData.Tile(r.GetProperty("tile"))));
        foreach (var r in doc.RootElement.GetProperty("columns").EnumerateArray())
        {
            var u = r.GetProperty("upper"); var e = r.GetProperty("expected");
            Assert.Equal(e.ValueKind == JsonValueKind.Null ? (int?)null : e.GetInt32(),
                DefaultAbilityTargeting.SelectLayer(SpellTargetTestData.Tile(r.GetProperty("lower")),
                    u.ValueKind == JsonValueKind.Null ? null : SpellTargetTestData.Tile(u), r.GetProperty("reference").GetByte(),
                    r.GetProperty("tolerance").GetByte(), r.GetProperty("topDown").GetBoolean()));
        }
    }

    [Fact]
    public void Primitive_rejects_fields_outside_original_packing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DefaultAbilityTargeting.ReferenceHeight(new BattleTile(256)));
        Assert.Throws<ArgumentOutOfRangeException>(() => DefaultAbilityTargeting.SelectLayer(new BattleTile(1),
            new BattleTile(TerrainSurfaceId.Unclassified, 1, waterDepth: 8), 0, 1));
    }
}
