using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class MagicProjectionQueryTests
{
    [Fact]
    public void Ready_calculation_produces_one_bound_hp_mp_rng_candidate_from_existing_owners()
    {
        var s = MagicProjectionTestData.Snapshot(); var q = MagicProjectionTestData.Request(s);
        var profile = MagicCastProjectionTests.Known with { EligibleSourceOrderKnown = false,
            Application = MagicLivingApplicationTests.Known with { ActiveUnitKnown = false } };
        var r = MagicProjectionQuery.Calculate(s, q, profile);
        Assert.True(r.Accepted); Assert.Same(s, r.Proposal!.Source); Assert.Same(s.Map, r.Proposal.Candidate.Map);
        Assert.Equal(new RandomState(1579910103, 19), r.RngState); Assert.Equal(r.RngState, r.Proposal.Candidate.RngState);
        var units = r.Proposal.Candidate.Units.ToDictionary(u => u.Occupant.UnitId.Value);
        Assert.Equal((ushort)21, units["actor"].Current!.Mp); Assert.Equal((ushort)66, units["first"].Vitals!.Hp); Assert.Equal((ushort)32, units["second"].Vitals!.Hp);
        Assert.Equal(9, r.Proposal.Cast.RecordedCost); Assert.Equal(new RandomState(57, 9), s.RngState);
    }

    [Theory] [InlineData(MagicFormulaEvaluationMode.Preview)] [InlineData(MagicFormulaEvaluationMode.AiSimulation)]
    public void Other_evaluation_modes_never_create_execution_projection_proposals(MagicFormulaEvaluationMode mode)
    {
        var s = MagicProjectionTestData.Snapshot(); var r = MagicProjectionQuery.Calculate(s, MagicProjectionTestData.Request(s, mode), MagicCastProjectionTests.Known);
        Assert.False(r.Accepted); Assert.Equal(MagicProjectionRejection.UnsupportedMode, r.Rejection); Assert.Null(r.Proposal); Assert.Equal(s.RngState, r.RngState);
    }

    [Fact]
    public void Foreign_map_or_caster_inputs_cannot_replace_snapshot_facts()
    {
        var s = MagicProjectionTestData.Snapshot(); var q = MagicProjectionTestData.Request(s);
        var foreignMap = new BattleMapDefinition(5, 1, 1, Enumerable.Repeat(new BattleTile(0), 5).ToArray());
        var i = Enumerable.Range(0, q.Roster.Length).Single(i => q.Roster[i].Occupant.UnitId == q.Plan.CasterId);
        foreach (var bad in new[] { q with { Map = foreignMap }, q with { Roster = q.Roster.SetItem(i, q.Roster[i] with { Current = q.Roster[i].Current! with { MagicAttack = 99 } }) } })
        { var r = MagicProjectionQuery.Calculate(s, bad, MagicCastProjectionTests.Known); Assert.Equal(MagicProjectionRejection.SnapshotMismatch, r.Rejection); Assert.Null(r.Proposal); Assert.Equal(s.RngState, r.RngState); }
    }

    [Fact]
    public void Late_living_admission_and_rng_failures_discard_the_entire_candidate()
    {
        var s = MagicProjectionTestData.Snapshot(); var second = s.Units[0];
        foreach (ushort hp in new ushort[] { 65, 85 })
        {
            var before = new MagicProjectionSnapshot(s.Map, s.Units.SetItem(0, second with { Vitals = second.Vitals! with { Hp = hp } }), s.RngState);
            var r = MagicProjectionQuery.Calculate(before, MagicProjectionTestData.Request(before), MagicCastProjectionTests.Known);
            Assert.False(r.Accepted); Assert.Null(r.Proposal); Assert.Equal(before.RngState, r.RngState);
            Assert.Equal(hp == 65 ? MagicLivingApplicationRejection.WouldKnockOut : MagicLivingApplicationRejection.WouldChangeStatus, r.ApplicationRejection);
        }
        var exhausted = new MagicProjectionSnapshot(s.Map, s.Units, new(57, ulong.MaxValue - 7));
        var fail = MagicProjectionQuery.Calculate(exhausted, MagicProjectionTestData.Request(exhausted), MagicCastProjectionTests.Known);
        Assert.False(fail.Accepted); Assert.Null(fail.Proposal); Assert.Equal(exhausted.RngState, fail.RngState);
        Assert.Equal(FaithMagicResolutionRejection.RandomDrawCountExhausted, fail.CalculationRejection);
    }

    [Fact]
    public void Healing_and_absorption_reuse_existing_routes_then_clamp_target_hp()
    {
        var s = MagicProjectionTestData.Snapshot(); var first = s.Units.Single(u => u.Occupant.UnitId.Value == "first");
        var absorb = first with { Current = first.Current! with { Affinities = new(Absorb: MagicElement.Fire) }, Vitals = first.Vitals! with { Hp = 80 } };
        var before = new MagicProjectionSnapshot(s.Map, s.Units.Replace(first, absorb), s.RngState);
        var r = MagicProjectionQuery.Calculate(before, MagicProjectionTestData.Request(before), MagicCastProjectionTests.Known);
        Assert.True(r.Accepted); Assert.Equal((ushort)100, r.Proposal!.Candidate.Units.Single(u => u.Occupant.UnitId.Value == "first").Vitals!.Hp);
        Assert.True(r.Proposal.Cast.Targets[0].Result.Effects.Absorption);
        var q = MagicProjectionTestData.Request(s); var original = MagicChargePlanTests.Request();
        var definition = original.Selection.Definition with { Spell = original.Selection.Definition.Spell with { Kind = FaithMagicAmountKind.Healing } };
        var start = original with { Selection = SpellCastPhaseTestData.Confirm(definition) };
        var plan = MagicChargePlanQuery.Prepare(start).Outcome!;
        for (int i = 0; i < 9; i++) plan = MagicChargePlanQuery.Tick(plan, false).Plan;
        var healed = MagicProjectionQuery.Calculate(s, q with { Plan = plan }, MagicCastProjectionTests.Known);
        Assert.True(healed.Accepted); Assert.Equal(s.RngState, healed.RngState); Assert.All(healed.Proposal!.Candidate.Units, u => Assert.Equal((ushort)100, u.Vitals!.Hp));
        Assert.Equal((ushort)21, healed.Proposal.Candidate.Units.Single(u => u.Occupant.UnitId.Value == "actor").Current!.Mp);
    }

    [Fact]
    public void Resource_failure_and_unknown_application_work_return_no_proposal()
    {
        var s = MagicProjectionTestData.Snapshot(); var actor = s.Units.Single(u => u.Occupant.UnitId.Value == "actor");
        var low = new MagicProjectionSnapshot(s.Map, s.Units.Replace(actor, actor with { Current = actor.Current! with { Mp = 8 } }), s.RngState);
        var r = MagicProjectionQuery.Calculate(low, MagicProjectionTestData.Request(low), MagicCastProjectionTests.Known);
        Assert.False(r.Accepted); Assert.Equal(MagicMpBlock.NotEnoughMp, r.ResourceBlock); Assert.Null(r.Proposal); Assert.Equal(low.RngState, r.RngState);
        Assert.False(MagicProjectionQuery.Calculate(s, MagicProjectionTestData.Request(s), null).Accepted);
        var noReactionProof = MagicCastProjectionTests.Known with { Application = MagicLivingApplicationTests.Known with { TargetReactionsKnownAbsent = false } };
        var rejected = MagicProjectionQuery.Calculate(s, MagicProjectionTestData.Request(s), noReactionProof);
        Assert.False(rejected.Accepted); Assert.Null(rejected.Proposal); Assert.Equal(s.RngState, rejected.RngState);
    }

    [Fact]
    [Trait("Contract", "QueryIsolation")]
    public void Repeated_calculation_preserves_source_snapshot_and_returns_equal_private_values()
    {
        var s = MagicProjectionTestData.Snapshot(); var copy = s.Units.ToArray(); var q = MagicProjectionTestData.Request(s);
        var a = MagicProjectionQuery.Calculate(s, q, MagicCastProjectionTests.Known); var b = MagicProjectionQuery.Calculate(s, q, MagicCastProjectionTests.Known);
        Assert.True(a.Accepted); Assert.Equal(a.RngState, b.RngState); Assert.Equal(a.Proposal!.Candidate.Units.ToArray(), b.Proposal!.Candidate.Units.ToArray());
        Assert.Equal(copy, s.Units.ToArray()); Assert.Same(s, a.Proposal.Source); Assert.Equal(new RandomState(57, 9), s.RngState);
    }
}
