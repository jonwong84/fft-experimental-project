using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class StoredSpellCastTests
{
    [Fact]
    public void Current_follow_position_drives_source_order_results_without_rechecking_blue_range()
    {
        var q = StoredSpellTestData.Request(); var rng = new RandomState(57, 9);
        var r = StoredSpellCastQuery.Resolve(q, rng);
        Assert.True(r.Accepted); Assert.Equal((ushort)21, r.Outcome!.CandidateMp); Assert.Equal(9, r.Outcome.RecordedCost);
        Assert.Equal(new[] { "first", "second" }, r.Outcome.Targets.Select(t => t.TargetId.Value));
        Assert.Equal((short)34, r.Outcome.Targets[0].Result.HpDamage); Assert.True(r.Outcome.Targets[0].Result.Effects.AddedEffect);
        Assert.Equal((short)68, r.Outcome.Targets[1].Result.HpDamage); Assert.True(r.Outcome.Targets[1].Result.Effects.Weakness);
        Assert.Equal(new RandomState(1579910103, 19), r.RngState);
        foreach (byte type in new byte[] { 5, 7 })
        {
            var tile = StoredSpellCastQuery.Resolve(q with { Aim = q.Aim with { TargetingType = type, TargetUnitId = new("unknown") }, AimProfile = new(true) }, rng);
            Assert.True(tile.Accepted); Assert.Equal("old", Assert.Single(tile.Outcome!.Targets).TargetId.Value);
            Assert.Equal((short)34, tile.Outcome.Targets[0].Result.HpDamage); Assert.Equal(new RandomState(2224270474, 14), tile.RngState);
        }
        var replay = StoredSpellCastQuery.Resolve(q, rng);
        Assert.Equal(r.Outcome.Targets.ToArray(), replay.Outcome!.Targets.ToArray()); Assert.Equal((ushort)30, q.Cast.Caster.Mp);
    }

    [Fact]
    public void Inactive_or_jumping_anchor_centres_the_area_without_becoming_an_affected_target()
    {
        var q = StoredSpellTestData.Request(); var anchor = q.Roster.Single(u => u.Occupant.UnitId.Value == "first");
        foreach (var changed in new[] { anchor with { Occupant = anchor.Occupant with { Active = false }, Current = null },
                     anchor with { Occupant = anchor.Occupant with { Jump = true } } })
        {
            var r = StoredSpellCastQuery.Resolve(q with { Roster = q.Roster.Replace(anchor, changed) }, new(57, 9));
            Assert.True(r.Accepted); Assert.Equal("second", Assert.Single(r.Outcome!.Targets).TargetId.Value);
            Assert.Equal((short)68, r.Outcome.Targets[0].Result.HpDamage); Assert.Equal(new RandomState(2224270474, 14), r.RngState);
        }
    }

    [Fact]
    public void Missing_conflicting_or_late_failure_facts_return_no_partial_cost_results_or_rng()
    {
        var q = StoredSpellTestData.Request(); var rng = new RandomState(57, 9); var second = q.Roster[0];
        StoredSpellCastRequest[] invalid = [q with { Aim = q.Aim with { TargetUnitId = new("absent") } },
            q with { AimProfile = null }, q with { AimProfile = new(true) }, q with { RosterProfile = null },
            q with { Roster = q.Roster.Add(q.Roster[^1]) },
            q with { Roster = q.Roster.SetItem(0, second with { Current = second.Current! with { Statuses = new(Dead: true) } }) }];
        foreach (var bad in invalid)
        { var r = StoredSpellCastQuery.Resolve(bad, rng); Assert.False(r.Accepted); Assert.Null(r.Outcome); Assert.Equal(rng, r.RngState); }
        rng = new(57, ulong.MaxValue - 7); var exhausted = StoredSpellCastQuery.Resolve(q, rng);
        Assert.False(exhausted.Accepted); Assert.Equal(FaithMagicResolutionRejection.RandomDrawCountExhausted, exhausted.Rejection);
        Assert.Null(exhausted.Outcome); Assert.Equal(rng, exhausted.RngState);
    }
}
