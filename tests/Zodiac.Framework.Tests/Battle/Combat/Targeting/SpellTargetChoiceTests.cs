using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SpellTargetChoiceTests
{
    [Fact]
    public void Ordinary_source_choice_literals_preserve_auto_priority_no_follow_and_no_panels()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spell-follow-contract-v1.json")));
        var r = MagicCastTestData.Ready(); var actor = new SpellTargetOccupant(r.Caster.UnitId, 0, default, SpellTargetTeam.Blue, SpellTargetTeam.Blue);
        var map = new BattleMapDefinition(1, 1, 1, [new(0)]);
        foreach (var row in doc.RootElement.GetProperty("choices").EnumerateArray())
        {
            var d = row.GetProperty("input"); if (d.GetProperty("weaponRange").GetBoolean()) continue;
            var def = r.Definition with { Targeting = new(d.GetProperty("range").GetByte(), 0, 0,
                CannotTargetSelf: d.GetProperty("panelCount").GetInt32() == 0, NoFollowTarget: d.GetProperty("noFollow").GetBoolean(), Auto: d.GetProperty("auto").GetBoolean()) };
            var result = SpellTargetChoiceQuery.Evaluate(map, actor, def, new(true, true, true, true));
            Assert.True(result.Accepted); Assert.Equal((SpellTargetChoiceMode)row.GetProperty("expected").GetInt32(), result.Outcome!.Mode);
            Assert.Same(def, result.Outcome.Definition);
        }
    }

    [Fact]
    public void Auto_zero_range_does_not_run_blue_collision_or_self_filtering()
    {
        var r = MagicCastTestData.Ready(); var map = new BattleMapDefinition(1, 1, 1,
            [new(TerrainSurfaceId.Unclassified, 0, movementFlags: BattleTileMovementFlags.Blocked)]);
        var d = r.Definition with { Targeting = new(0, 0, 0, CannotTargetSelf: true, NoFollowTarget: true, Auto: true) };
        var result = SpellTargetChoiceQuery.Evaluate(map, new(r.Caster.UnitId, 0, default, SpellTargetTeam.Blue, SpellTargetTeam.Blue), d, new(true, true, true, true));
        Assert.True(result.Accepted); Assert.Equal(SpellTargetChoiceMode.NoTargeting, result.Outcome!.Mode); Assert.Null(result.Outcome.Range);
    }

    [Fact]
    public void Unknown_or_inactive_context_produces_no_choice()
    {
        var r = MagicCastTestData.Ready(); var map = new BattleMapDefinition(1, 1, 1, [new(0)]);
        var actor = new SpellTargetOccupant(r.Caster.UnitId, 0, default, SpellTargetTeam.Blue, SpellTargetTeam.Blue);
        var d = r.Definition with { Targeting = new(0, 0, 0) };
        Assert.False(SpellTargetChoiceQuery.Evaluate(map, actor, d, null).Accepted);
        Assert.False(SpellTargetChoiceQuery.Evaluate(map, actor with { Active = false }, d, new(true, true, true, true)).Accepted);
        Assert.False(SpellTargetChoiceQuery.Evaluate(map, actor, d with { Targeting = null }, new(true, true, true, true)).Accepted);
    }
}
