using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SpellAimCaptureTests
{
    [Fact]
    public void Player_confirmation_literals_preserve_exact_types_fallback_and_previous_ignored_identity()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spell-follow-contract-v1.json")));
        var choice = SpellCastPhaseTestData.Choice(); var cursor = new TilePosition(1, 0, 0);
        foreach (var row in doc.RootElement.GetProperty("confirmations").EnumerateArray())
        {
            var d = row.GetProperty("input"); if (!d.GetProperty("player").GetBoolean()) continue;
            var occupant = d.GetProperty("occupantId"); var id = new BattleUnitId("slot-" + d.GetProperty("storedId").GetInt32());
            var r = SpellAimCapture.Capture(choice, cursor, d.GetProperty("answer").GetByte(), occupant.ValueKind == JsonValueKind.Null ? null :
                new(new("slot-" + occupant.GetInt32()), 1, cursor, SpellTargetTeam.Blue, SpellTargetTeam.Blue), id, new(true, true, true));
            var e = row.GetProperty("expected"); Assert.Equal(e.GetProperty("startsAttack").GetBoolean(), r.Accepted);
            if (!r.Accepted) { Assert.Null(r.Outcome); continue; }
            Assert.Equal(e.GetProperty("type").GetByte(), r.Outcome!.Aim.TargetingType);
            Assert.Equal("slot-" + e.GetProperty("targetId").GetInt32(), r.Outcome.Aim.TargetUnitId!.Value.Value);
            Assert.Same(choice.Definition, r.Outcome.Definition); Assert.Equal(choice.CasterId, r.Outcome.CasterId);
        }
    }

    [Fact]
    public void Unknown_occupancy_or_changed_context_never_infers_a_unit_or_admits_an_unmarked_tile()
    {
        var c = SpellCastPhaseTestData.Choice(); var p = new TilePosition(1, 0, 0);
        Assert.False(SpellAimCapture.Capture(c, p, 6, null, null, new(true, true)).Accepted);
        Assert.False(SpellAimCapture.Capture(c, p, 5, null, null, null).Accepted);
        Assert.False(SpellAimCapture.Capture(c, p, 5, null, null, new(false, true, true)).Accepted);
        Assert.False(SpellAimCapture.Capture(c, p, 5, null, null, new(true, false, true)).Accepted);
        Assert.Equal(SpellTargetingRejection.InvalidAim, SpellAimCapture.Capture(c, new(4, 0, 0), 5, null, null, new(true, true)).Rejection);
        Assert.True(SpellAimCapture.Capture(c, p, 5, null, null, new(true, true)).Accepted);
        Assert.Equal((byte)5, SpellAimCapture.Capture(c, p, 6, null, null, new(true, true, true)).Outcome!.Aim.TargetingType);
    }

    [Fact]
    public void Cursor_eligibility_is_distinct_from_combat_mount_filtering_and_raw_yes_is_not_unit_follow()
    {
        var c = SpellCastPhaseTestData.Choice(); var p = new TilePosition(1, 0, 0);
        var mount = new SpellTargetOccupant(new("mount"), 1, p, SpellTargetTeam.Blue, SpellTargetTeam.Blue, Mount: true);
        var r = SpellAimCapture.Capture(c, p, 7, mount, null, new(true, true, true)); Assert.True(r.Accepted);
        Assert.Equal((byte)7, r.Outcome!.Aim.TargetingType);
        Assert.True(SpellAimResolver.Resolve(c.Range!.Map, r.Outcome.Aim, null, new(true)).Accepted);
        Assert.False(SpellAimCapture.Capture(c, p, 6, mount with { Jump = true }, null, new(true, true, true)).Accepted);
        Assert.False(SpellAimCapture.Capture(c, p, 6, mount with { Position = default }, null, new(true, true, true)).Accepted);
    }
}
