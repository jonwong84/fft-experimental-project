using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicReadyPlanTests
{
    [Fact]
    public void Nineteen_independent_phase_descriptions_cover_selection_charge_current_inputs_and_atomic_results()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "spell-aim-pipeline-contract-v1.json")));
        foreach (var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var d = row.GetProperty("input"); var e = row.GetProperty("expected"); var (result, counter) = SpellAimPipelineTestData.Run(d);
            Assert.Equal(e.GetProperty("accepted").GetBoolean(), result.Accepted);
            Assert.Equal(e.GetProperty("block").ValueKind == JsonValueKind.Null ? (MagicMpBlock?)null :
                Enum.Parse<MagicMpBlock>(e.GetProperty("block").GetString()!), result.ResourceBlock);
            Assert.Equal(e.GetProperty("counter").ValueKind == JsonValueKind.Null ? (byte?)null : e.GetProperty("counter").GetByte(), counter);
            var r = result.Resolution; Assert.Equal(Enum.Parse<FaithMagicResolutionRejection>(e.GetProperty("rejection").GetString()!), r.Rejection);
            Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), r.RngState);
            if (!result.Accepted) { Assert.Null(r.Outcome); Assert.Equal(new RandomState(d.GetProperty("word").GetUInt32(), d.GetProperty("count").GetUInt64()), r.RngState); continue; }
            Assert.Equal(e.GetProperty("mp").GetUInt16(), r.Outcome!.CandidateMp); Assert.Equal(e.GetProperty("cost").GetInt32(), r.Outcome.RecordedCost);
            Assert.Equal(e.GetProperty("ids").EnumerateArray().Select(v => v.GetString()), r.Outcome.Targets.Select(t => t.TargetId.Value));
            Assert.Equal(e.GetProperty("damage").EnumerateArray().Select(v => v.GetInt16()), r.Outcome.Targets.Select(t => t.Result.HpDamage));
            Assert.Equal(e.GetProperty("added").EnumerateArray().Select(v => v.GetBoolean()), r.Outcome.Targets.Select(t => t.Result.Effects.AddedEffect));
        }
    }

    internal static MagicReadyPlanRequest Request(MagicFormulaEvaluationMode mode = MagicFormulaEvaluationMode.Execute, byte type = 6)
    {
        var q = StoredSpellTestData.Request(mode); var plan = MagicChargePlanQuery.Prepare(MagicChargePlanTests.Request(type)).Outcome!;
        for (int i = 0; i < 9; i++) plan = MagicChargePlanQuery.Tick(plan, false).Plan;
        var ready = MagicCastTestData.Ready(mode);
        return new(plan, q.Map, q.Roster, ready.Weather, mode, ready.ResultProfile,
            ready.ReadyProfile! with { ChargeReadyKnown = false }, q.GeometryProfile, q.RosterProfile, q.AimProfile,
            q.Entry! with { MapAimPreviouslyValidatedKnown = false }, new(true, true));
    }

    [Theory]
    [InlineData(MagicFormulaEvaluationMode.Execute, "two-targets-one-cost")]
    [InlineData(MagicFormulaEvaluationMode.AiSimulation, "two-target-AiSimulation")]
    [InlineData(MagicFormulaEvaluationMode.Preview, "two-target-Preview")]
    public void Source_cast_literals_run_through_confirm_charge_and_current_ready_resolution(MagicFormulaEvaluationMode mode, string id)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "magic-cast-contract-v1.json")));
        var e = doc.RootElement.GetProperty("casts").EnumerateArray().Single(r => r.GetProperty("id").GetString() == id).GetProperty("expected");
        var q = Request(mode); var r = MagicReadyPlanQuery.Resolve(q, new(57, 9));
        Assert.True(r.Accepted); Assert.Null(r.ResourceBlock); var actual = r.Resolution;
        Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(), e.GetProperty("count").GetUInt64()), actual.RngState);
        var o = e.GetProperty("outcome"); Assert.Equal(o.GetProperty("mp").GetUInt16(), actual.Outcome!.CandidateMp);
        Assert.Equal(o.GetProperty("cost").GetInt32(), actual.Outcome.RecordedCost); Assert.Equal(2, actual.Outcome.Targets.Length);
        for (int i = 0; i < 2; i++)
        { var target = o.GetProperty("targets")[i]; Assert.Equal(target.GetProperty("id").GetString(), actual.Outcome.Targets[i].TargetId.Value);
            Assert.Equal(MagicCurrentTestData.Frame(target.GetProperty("result")), actual.Outcome.Targets[i].Result); }
    }

    [Fact]
    public void Readiness_uses_current_mp_and_ma_while_retaining_the_original_definition()
    {
        var q = Request(); var actor = q.Roster.Single(u => u.Occupant.UnitId == q.Plan.CasterId);
        var r = MagicReadyPlanQuery.Resolve(q with { Roster = q.Roster.Replace(actor, actor with { Current = actor.Current! with { Mp = 20, MagicAttack = 10 } }) }, new(57, 9));
        Assert.True(r.Accepted); Assert.Equal((ushort)11, r.Resolution.Outcome!.CandidateMp);
        Assert.Equal((short)68, r.Resolution.Outcome.Targets[0].Result.HpDamage); Assert.Equal((short)137, r.Resolution.Outcome.Targets[1].Result.HpDamage);
        Assert.Equal(9, q.Plan.Definition.MpCost); Assert.Equal((byte)14, q.Plan.Definition.Spell.Power);
        Assert.Equal((ushort)30, actor.Current!.Mp);
    }

    [Fact]
    public void Delayed_tile_and_yes_aims_keep_the_old_tile_while_unit_follow_moves()
    {
        foreach (byte type in new byte[] { 5, 7 })
        {
            var q = Request(type: type); var r = MagicReadyPlanQuery.Resolve(q, new(57, 9));
            Assert.True(r.Accepted); Assert.Equal("old", Assert.Single(r.Resolution.Outcome!.Targets).TargetId.Value);
            Assert.Equal(new RandomState(2224270474, 14), r.Resolution.RngState);
        }
        var follow = MagicReadyPlanQuery.Resolve(Request(), new(57, 9)); Assert.Equal(2, follow.Resolution.Outcome!.Targets.Length);
    }

    [Fact]
    public void Unready_blocked_unknown_or_unaffordable_queries_return_no_payable_proposal()
    {
        var q = Request(); var rng = new RandomState(57, 9);
        MagicReadyPlanRequest[] invalid = [q with { Plan = MagicChargePlanQuery.Prepare(MagicChargePlanTests.Request()).Outcome! },
            q with { Profile = new(true, true, true) }, q with { Profile = new(false, true) }, q with { Profile = new(true, false) },
            q with { ReadyProfile = null }, q with { ReadyProfile = q.ReadyProfile! with { ActionStateBookkeepingKnown = false } }];
        foreach (var bad in invalid)
        { var r = MagicReadyPlanQuery.Resolve(bad, rng); Assert.False(r.Accepted); Assert.Null(r.Resolution.Outcome); Assert.Equal(rng, r.Resolution.RngState); }
        var actor = q.Roster.Single(u => u.Occupant.UnitId == q.Plan.CasterId);
        var blocked = MagicReadyPlanQuery.Resolve(q with { Roster = q.Roster.Replace(actor, actor with { Current = actor.Current! with { Mp = 8 } }) }, rng);
        Assert.False(blocked.Accepted); Assert.Equal(MagicMpBlock.NotEnoughMp, blocked.ResourceBlock);
        Assert.Null(blocked.Resolution.Outcome); Assert.Equal(rng, blocked.Resolution.RngState);
    }

    [Fact]
    public void Late_failures_and_unknown_anchor_positions_discard_all_candidate_results_and_rng()
    {
        var q = Request(); var rng = new RandomState(57, 9);
        var late = q with { Roster = q.Roster.SetItem(0, q.Roster[0] with { Current = q.Roster[0].Current! with { Statuses = new(Dead: true) } }) };
        foreach (var bad in new[] { late, q with { AimProfile = new(true) }, q with { Entry = new(true, false) } })
        { var r = MagicReadyPlanQuery.Resolve(bad, rng); Assert.False(r.Accepted); Assert.Null(r.ResourceBlock); Assert.Null(r.Resolution.Outcome); Assert.Equal(rng, r.Resolution.RngState); }
        rng = new(57, ulong.MaxValue - 7); var exhausted = MagicReadyPlanQuery.Resolve(q, rng);
        Assert.Equal(FaithMagicResolutionRejection.RandomDrawCountExhausted, exhausted.Resolution.Rejection); Assert.Null(exhausted.Resolution.Outcome); Assert.Equal(rng, exhausted.Resolution.RngState);
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Ready_plan_replay_adopts_no_state_and_retains_input_snapshots()
    {
        var q = Request(); var copy = q.Roster.ToArray(); var rng = new RandomState(57, 9);
        var a = MagicReadyPlanQuery.Resolve(q, rng); var b = MagicReadyPlanQuery.Resolve(q, rng);
        Assert.Equal(a.Resolution.Outcome!.Targets.ToArray(), b.Resolution.Outcome!.Targets.ToArray()); Assert.Equal(a.Resolution.RngState, b.Resolution.RngState);
        Assert.Equal(copy, q.Roster.ToArray()); Assert.Equal((byte)0, q.Plan.Counter); Assert.Equal(new RandomState(57, 9), rng);
    }
}
