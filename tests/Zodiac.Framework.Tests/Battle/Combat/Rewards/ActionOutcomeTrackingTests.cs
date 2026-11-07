using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class ActionOutcomeTrackingTests
{
    [Fact]
    public void Literal_markers_preserve_assignment_context_and_miss_order()
    {
        using var fixture = ActionRewardTestData.Fixture();
        foreach (var row in fixture.RootElement.GetProperty("markers").EnumerateArray())
        {
            var d = row.GetProperty("input");
            Assert.Equal((AbilityOutcomeMarker)row.GetProperty("expected").GetByte(),
                ActionOutcomeTracking.ApplyOrdinary((AbilityOutcomeMarker)d[0].GetByte(), d[1].GetBoolean(), d[2].GetBoolean(), d[3].GetBoolean()));
        }
    }

    [Fact]
    public void Later_primary_actor_miss_can_replace_its_earlier_self_target_affected_marker()
    {
        var target = ActionOutcomeTracking.ApplyOrdinary(AbilityOutcomeMarker.None, true, true, true);
        var actor = ActionOutcomeTracking.ApplyOrdinary(target, true, false, false);
        Assert.Equal(AbilityOutcomeMarker.Affected, target);
        Assert.Equal(AbilityOutcomeMarker.Targeted, actor);
    }

    [Fact]
    public void Reaction_miss_preserves_unknown_bits_but_reaction_change_assigns_affected()
    {
        var prior = (AbilityOutcomeMarker)0x85;
        Assert.Equal(prior, ActionOutcomeTracking.ApplyOrdinary(prior, false, false, true));
        Assert.Equal(AbilityOutcomeMarker.Affected, ActionOutcomeTracking.ApplyOrdinary(prior, false, true, true));
    }
}
