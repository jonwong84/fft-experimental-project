using System.Collections.Immutable;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class BattleActionRewardsTests
{
    internal static ActionRewardUnit Unit(string id = "actor", int order = 0, SpellTargetTeam team = SpellTargetTeam.Blue, int slot = 1) =>
        new(new(id), order, team, team, slot, 10, 90, 1, 20, 199);
    internal static ActionRewardProjectionResult Run(ImmutableArray<ActionRewardUnit> units, byte earned = 14, byte job = 75) =>
        BattleActionRewards.Project(units, new("actor"), job, earned, false, true, JobPointArithmeticTests.Thresholds);

    [Fact]
    public void Eight_independent_roster_descriptions_match_complete_reward_projections()
    {
        using var fixture = ActionRewardTestData.Fixture();
        foreach (var row in fixture.RootElement.GetProperty("rosters").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected");
            int slot = JobPointArithmetic.OriginalRewardSlot(d.GetProperty("job").GetByte());
            var units = d.GetProperty("units").EnumerateArray().Select(u =>
            {
                var flags = u[9].EnumerateArray().Select(x => x.GetString()).ToHashSet();
                return new ActionRewardUnit(new(u[0].GetString()!), u[1].GetInt32(), (SpellTargetTeam)u[2].GetInt32(),
                    (SpellTargetTeam)u[3].GetInt32(), slot, u[4].GetByte(), u[5].GetByte(), u[6].GetByte(), u[7].GetUInt16(), u[8].GetUInt16(),
                    !flags.Contains("inactive"), flags.Contains("crystal"), flags.Contains("dead"), flags.Contains("petrify"),
                    flags.Contains("treasure"), flags.Contains("monster"), flags.Contains("male"), flags.Contains("female"), flags.Contains("jpUp"));
            }).ToImmutableArray();
            var r = BattleActionRewards.Project(units, new("actor"), d.GetProperty("job").GetByte(), d.GetProperty("earned").GetByte(),
                d.GetProperty("hamedo").GetBoolean(), d.GetProperty("canEarn").GetBoolean(), JobPointArithmeticTests.Thresholds);
            Assert.Equal(e.GetProperty("disposition").GetString(), r.Disposition.ToString());
            if (r.Disposition != ActionRewardDisposition.Projected) { Assert.Null(r.Projection); continue; }
            var p = r.Projection!; var a = e.GetProperty("amounts"); var b = e.GetProperty("balance"); var display = e.GetProperty("display");
            Assert.Equal(new(a[0].GetByte(), a[1].GetInt32(), a[2].GetInt32(), a[3].GetInt32()), p.Amounts);
            Assert.Equal(new(b[0].GetByte(), b[1].GetByte(), b[2].GetBoolean()), p.ActorExperience);
            Assert.Equal(display[0].GetByte(), p.LevelForDisplay); Assert.Equal(display[1].GetByte(), p.JobLevelForDisplay);
            var points = e.GetProperty("points"); Assert.Equal(points.GetArrayLength(), p.JobPoints.Length);
            for (int i = 0; i < p.JobPoints.Length; i++)
            {
                var expected = points[i]; var actual = p.JobPoints[i];
                Assert.Equal(expected[0].GetString(), actual.UnitId.Value); Assert.Equal(expected[1].GetInt32(), actual.Award);
                Assert.Equal(new(expected[2].GetUInt16(), expected[3].GetUInt16(), expected[4].GetInt32()), actual.Balance);
                Assert.Equal(slot, actual.JobSlot);
            }
        }
    }

    [Fact]
    public void Gate_order_skips_actor_and_career_reads_and_clears_reward_data()
    {
        var hamedo = BattleActionRewards.Project(default, default, 0, 99, true, false, default);
        var skip = BattleActionRewards.Project(default, default, 0, 99, false, false, default);
        Assert.Equal(ActionRewardDisposition.Rejected, hamedo.Disposition); Assert.Null(hamedo.Projection);
        Assert.Equal(ActionRewardDisposition.Skipped, skip.Disposition); Assert.Null(skip.Projection);
        Assert.Equal(ActionRewardDisposition.Rejected, Run([Unit("other")]).Disposition);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("crystal")] [InlineData("dead")] [InlineData("petrify")] [InlineData("treasure")]
    public void Actor_exclusions_return_no_projection(string state)
    {
        var a = Unit() with { Active = state != "inactive", Crystal = state == "crystal", Dead = state == "dead", Petrify = state == "petrify", Treasure = state == "treasure" };
        var r = Run([a]); Assert.Equal(ActionRewardDisposition.Rejected, r.Disposition); Assert.Null(r.Projection);
    }

    [Fact]
    public void Charmed_team_spillover_uses_initial_recipient_team_source_order_and_pre_bonus_amount()
    {
        var actor = Unit() with { CurrentTeam = SpellTargetTeam.Red, GainedJpUp = true };
        var first = Unit("first", 1, SpellTargetTeam.Red) with { CurrentTeam = SpellTargetTeam.Blue, Petrify = true };
        var later = Unit("later", 3, SpellTargetTeam.Red);
        var rejected = new[] { Unit("blue", 2), Unit("inactive", 4, SpellTargetTeam.Red) with { Active = false },
            Unit("dead", 5, SpellTargetTeam.Red) with { Dead = true }, Unit("crystal", 6, SpellTargetTeam.Red) with { Crystal = true },
            Unit("treasure", 7, SpellTargetTeam.Red) with { Treasure = true }, Unit("monster", 8, SpellTargetTeam.Red) with { IsMonster = true } };
        var roster = ImmutableArray.Create(later, actor, first).AddRange(rejected);
        var r = Run(roster).Projection!;
        Assert.Equal(new(14, 12, 18, 3), r.Amounts);
        Assert.Equal(new[] { "first", "later", "actor" }, r.JobPoints.Select(p => p.UnitId.Value));
        Assert.Equal(new[] { 3, 3, 18 }, r.JobPoints.Select(p => p.Award));
        Assert.Equal(new(0, 11, true), r.ActorExperience); Assert.Equal((byte)11, r.LevelForDisplay);
        Assert.Equal((byte)2, r.JobLevelForDisplay); Assert.True(r.RequiresUnlockRecalculation);
    }

    [Theory]
    [InlineData((byte)91, true, false, true)] [InlineData((byte)91, false, true, false)]
    [InlineData((byte)92, true, false, false)] [InlineData((byte)92, false, true, true)]
    [InlineData((byte)91, true, true, false)] [InlineData((byte)92, true, true, false)]
    public void Bard_dancer_filters_keep_independent_sex_bits(byte job, bool male, bool female, bool receives)
    {
        int slot = JobPointArithmetic.OriginalRewardSlot(job);
        var other = Unit("other", 1, slot: slot) with { IsMale = male, IsFemale = female };
        var r = Run([Unit(slot: slot), other], job: job).Projection!;
        Assert.Equal(receives, r.JobPoints.Any(p => p.UnitId == other.UnitId));
    }

    [Fact]
    public void Zero_exp_still_offers_zero_jp_clamps_storage_and_requests_unlock_recalculation()
    {
        var a = Unit() with { Experience = 0, CurrentJobPoints = 65535, TotalJobPoints = 65535 };
        var r = Run([a, Unit("other", 1)], 0).Projection!;
        Assert.Equal(new(0, 0, 0, 0), r.Amounts); Assert.Equal(2, r.JobPoints.Length);
        Assert.Equal(new(9999, 9999, 8), r.JobPoints.Single(p => p.UnitId == a.UnitId).Balance);
        Assert.Equal((byte)8, r.JobLevelForDisplay); Assert.True(r.RequiresUnlockRecalculation);
    }

    [Fact]
    public void Incompatible_actor_sex_can_skip_own_jp_storage_while_display_still_reports_earned_amount()
    {
        var r = Run([Unit(slot: 17) with { IsFemale = true }], job: 91).Projection!;
        Assert.Equal(12, r.Amounts.ActorJp); Assert.Empty(r.JobPoints);
        Assert.Equal((byte)0, r.JobLevelForDisplay); Assert.False(r.RequiresUnlockRecalculation);
    }

    [Fact]
    public void Monster_only_projects_exp_and_prior_level_flag_can_request_display_without_new_growth()
    {
        var monster = Unit() with { IsMonster = true, JobLevel = 255, JobSlot = -1 };
        var r = BattleActionRewards.Project([monster], monster.UnitId, 0, 14, false, true, default).Projection!;
        Assert.Equal(new(14, 0, 0, 0), r.Amounts); Assert.Empty(r.JobPoints); Assert.False(r.RequiresUnlockRecalculation);
        Assert.True(r.ActorExperience.RequiresStatGrowth);
        var oldLevel = BattleActionRewards.Project([Unit() with { Experience = 0 }], new("actor"), 75, 0, false, true,
            JobPointArithmeticTests.Thresholds, priorLevelGained: true).Projection!;
        Assert.Equal((byte)10, oldLevel.LevelForDisplay); Assert.False(oldLevel.ActorExperience.RequiresStatGrowth);
    }

    [Fact]
    public void Malformed_roster_or_late_wrong_job_capture_never_exposes_a_partial_projection()
    {
        Assert.Throws<ArgumentException>(() => Run(default));
        Assert.Throws<ArgumentException>(() => Run([Unit(), Unit()]));
        Assert.Throws<ArgumentException>(() => Run([Unit(), Unit("other")]));
        Assert.Throws<ArgumentException>(() => Run([Unit(), Unit("other", 1, slot: 2)]));
        Assert.Throws<ArgumentException>(() => Run([Unit() with { JobSlot = 2 }]));
        Assert.Throws<ArgumentException>(() => Run([Unit() with { InitialTeam = (SpellTargetTeam)255 }]));
    }

    [Fact]
    public void Replay_preserves_inputs_and_has_no_original_fixed_roster_limit()
    {
        var roster = Enumerable.Range(0, 26).Select(i => Unit(i == 0 ? "actor" : "unit" + i, i)).Reverse().ToImmutableArray();
        var copy = roster.ToArray(); var a = Run(roster).Projection!; var b = Run(roster).Projection!;
        Assert.Equal(26, a.JobPoints.Length); Assert.Equal(copy, roster.ToArray());
        Assert.Equal(a.Amounts, b.Amounts); Assert.Equal(a.ActorExperience, b.ActorExperience);
        Assert.Equal(a.JobPoints.ToArray(), b.JobPoints.ToArray());
    }

    [Fact]
    public void Two_target_magic_vital_proposal_feeds_real_outcomes_exp_maximum_and_reward_owners()
    {
        var before = MagicProjectionTestData.Snapshot();
        var magic = MagicProjectionQuery.Calculate(before, MagicProjectionTestData.Request(before), MagicCastProjectionTests.Known).Proposal!;
        byte earned = 0;
        foreach (var target in magic.Cast.Targets)
        {
            var prior = before.Units.Single(u => u.Occupant.UnitId == target.TargetId);
            var next = magic.Candidate.Units.Single(u => u.Occupant.UnitId == target.TargetId);
            bool changed = prior.Vitals!.Hp != next.Vitals!.Hp || prior.Current!.Mp != next.Current!.Mp;
            Assert.Equal(AbilityOutcomeMarker.Affected, ActionOutcomeTracking.ApplyOrdinary(AbilityOutcomeMarker.None, true, target.Result.Hit, changed));
            earned = ActionExperience.Accumulate(earned, 10, (byte)(target.TargetId.Value == "first" ? 10 : 14), 0,
                changed ? ActionTargetOutcome.Changed : ActionTargetOutcome.Unchanged, true, false);
        }
        var reward = Run([Unit() with { GainedJpUp = true }, Unit("first", 1), Unit("second", 2)], earned).Projection!;
        Assert.Equal((byte)14, earned); Assert.Equal(new(14, 12, 18, 3), reward.Amounts);
        Assert.True(reward.ActorExperience.RequiresStatGrowth); Assert.True(reward.RequiresUnlockRecalculation);
        Assert.Equal((ushort)30, before.Units.Single(u => u.Occupant.UnitId.Value == "actor").Current!.Mp);
        Assert.Equal(new Zodiac.Framework.Random.RandomState(1579910103, 19), magic.Candidate.RngState);
    }
}
