using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicCastProjectionTests
{
    internal static MagicCastProjectionProfile Known => new(true, true, MagicLivingApplicationTests.Known);
    internal static MagicCastOutcome Cast(MagicProjectionSnapshot s) => MagicReadyPlanQuery.Resolve(MagicProjectionTestData.Request(s), s.RngState).Resolution.Outcome!;

    [Fact]
    public void Two_target_cast_projects_one_cost_and_preserves_roster_order_bystanders_and_input_rng()
    {
        var s = MagicProjectionTestData.Snapshot(); var cast = Cast(s); var r = MagicCastProjection.Project(s, new("actor"), cast, Known);
        Assert.True(r.Accepted); var next = r.Candidate!;
        Assert.Equal(s.Units.Select(u => u.Occupant.UnitId), next.Units.Select(u => u.Occupant.UnitId));
        Assert.True(next.TryGetUnit(new("actor"), out var actor)); Assert.Equal((ushort)21, actor!.Current!.Mp);
        Assert.True(next.TryGetUnit(new("first"), out var first)); Assert.Equal((ushort)66, first!.Vitals!.Hp);
        Assert.True(next.TryGetUnit(new("second"), out var second)); Assert.Equal((ushort)32, second!.Vitals!.Hp);
        Assert.True(next.TryGetUnit(new("old"), out var bystander)); Assert.Equal((ushort)100, bystander!.Vitals!.Hp);
        Assert.Equal(s.RngState, next.RngState); Assert.All(s.Units, u => Assert.Equal((ushort)100, u.Vitals!.Hp));
        Assert.Equal((ushort)30, s.Units.Single(u => u.Occupant.UnitId.Value == "actor").Current!.Mp);
    }

    [Fact]
    public void Self_target_and_miss_work_see_the_private_post_cost_mp_without_paying_again()
    {
        var s = MagicProjectionTestData.Snapshot(); var frame = new MagicResultFrame(true, 100, MagicResultMissKind.Hit, 34, 0, new(true), new());
        foreach (bool hit in new[] { true, false })
        {
            var cast = new MagicCastOutcome(21, 9, [new(new("actor"), frame with { Hit = hit }, null, null)]);
            var r = MagicCastProjection.Project(s, new("actor"), cast, Known); Assert.True(r.Accepted);
            var actor = r.Candidate!.Units.Single(u => u.Occupant.UnitId.Value == "actor");
            Assert.Equal((ushort)21, actor.Current!.Mp); Assert.Equal(hit ? (ushort)66 : (ushort)100, actor.Vitals!.Hp);
        }
    }

    [Fact]
    public void Late_ko_or_critical_rejection_discards_earlier_hp_and_caster_mp_candidates()
    {
        var s = MagicProjectionTestData.Snapshot(); var cast = Cast(s); var second = s.Units[0];
        foreach (ushort hp in new ushort[] { 65, 85 })
        {
            var before = new MagicProjectionSnapshot(s.Map, s.Units.SetItem(0, second with { Vitals = second.Vitals! with { Hp = hp } }), s.RngState);
            var r = MagicCastProjection.Project(before, new("actor"), cast, Known);
            Assert.False(r.Accepted); Assert.Null(r.Candidate);
            Assert.Equal(hp == 65 ? MagicLivingApplicationRejection.WouldKnockOut : MagicLivingApplicationRejection.WouldChangeStatus, r.ApplicationRejection);
            Assert.Equal((ushort)30, before.Units.Single(u => u.Occupant.UnitId.Value == "actor").Current!.Mp);
            Assert.Equal((ushort)100, before.Units.Single(u => u.Occupant.UnitId.Value == "first").Vitals!.Hp);
        }
    }

    [Fact]
    public void Uncertified_or_malformed_supplied_casts_never_return_a_candidate()
    {
        var s = MagicProjectionTestData.Snapshot(); var cast = Cast(s);
        Assert.False(MagicCastProjection.Project(s, new("actor"), cast, null).Accepted);
        Assert.False(MagicCastProjection.Project(s, new("actor"), cast, Known with { NoActorVitalWorkKnown = false }).Accepted);
        foreach (var bad in new[] { cast with { RecordedCost = 0 }, cast with { CandidateMp = 31 }, cast with { Targets = [] },
                     cast with { Targets = cast.Targets.Add(cast.Targets[0]) }, cast with { Targets = cast.Targets.SetItem(0, cast.Targets[0] with { TargetId = new("absent") }) } })
            Assert.Null(MagicCastProjection.Project(s, new("actor"), bad, Known).Candidate);
    }
}
