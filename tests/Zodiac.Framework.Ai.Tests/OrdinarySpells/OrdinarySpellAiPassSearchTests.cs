using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class OrdinarySpellAiPassSearchTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard");
    private static OrdinarySpellAiPassSearchResult Query(OrdinarySpellAiCatalog catalog, OrdinaryMagicBattleState state,
        OrdinarySpellAiAimPassKind pass = OrdinarySpellAiAimPassKind.UnitsStillAtDeadline, RandomState? rng = null) =>
        OrdinarySpellAiPassSearchQuery.Evaluate(state, catalog, new(Mage, 0, pass), new(true), rng ?? new(57, 0));

    [Theory]
    [InlineData(0, 20UL, 2769352549U)]
    [InlineData(2, 19UL, 1564473436U)]
    public void Adverse_self_without_a_caster_hit_consumes_the_global_aim_and_stops_ranked_origins(byte charge, ulong draws, uint seed)
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: charge);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.False(outcome.Pass.Potential.ConsumeEvaluatedAims);
        Assert.True(outcome.Pass.RankedOrigins);
        var trace = Assert.Single(outcome.Traces);
        Assert.Equal(new TilePosition(1, 3, 0), trace.Origin.CastOrigin);
        var candidate = Assert.Single(trace.Evaluations).Candidate.Outcome!;
        Assert.DoesNotContain(Mage, candidate.Accounting.IncludedTargets);
        Assert.Equal((short)49, candidate.Score!.Priority);
        Assert.Single(trace.ConsumedAims);
        Assert.Empty(outcome.RemainingAims);
        Assert.True(outcome.PassesBaseline);
        Assert.Equal(new RandomState(seed, draws), result.RngState);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Explicit_deadline_pass_setting_consumes_even_when_the_caster_was_hit()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 3, range: 1, area: 2, targetX: 2);
        var result = Query(catalog, state, OrdinarySpellAiAimPassKind.UnitsMovingByDeadline);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.False(outcome.Origins.Potential.Ability.Usage.ConsumeAim);
        Assert.True(outcome.Pass.Potential.ConsumeEvaluatedAims);
        var trace = Assert.Single(outcome.Traces);
        Assert.Equal(new TilePosition(2, 3, 0), trace.Origin.CastOrigin);
        var candidate = Assert.Single(trace.Evaluations).Candidate.Outcome!;
        Assert.Contains(Mage, candidate.Accounting.IncludedTargets);
        Assert.Equal((ushort)46, candidate.Units.Single(u => u.Id == Mage).Vitals.Hp);
        Assert.Equal((short)-22, candidate.Score!.Priority);
        Assert.Single(trace.ConsumedAims);
        Assert.Empty(outcome.RemainingAims);
        Assert.False(outcome.PassesBaseline);
        Assert.Equal(21UL, result.RngState.DrawCount);
    }

    [Fact]
    public void Retained_self_hit_aims_survive_until_later_origins_and_consumed_aims_are_skipped()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 0, range: 4, area: 2, targetX: 2);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state, OrdinarySpellAiAimPassKind.Tiles);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(new[] { new TilePosition(0, 4, 0), new(0, 0, 0), new(1, 4, 0), new(0, 3, 0),
            new(0, 1, 0), new(1, 0, 0), new(1, 3, 0) }, outcome.Traces.Select(t => t.Origin.CastOrigin));
        Assert.Equal(new[] { 7, 5, 3, 1, 1, 0, 1 }, outcome.Traces.Select(t => t.Evaluations.Length));
        Assert.Equal(new[] { 4, 3, 2, 1, 1, 0, 1 }, outcome.Traces.Select(t => t.ConsumedAims.Length));
        Assert.Equal(new[] { 8, 5, 3, 2, 1, 1, 0 }, outcome.Traces.Select(t => t.RemainingAtExit.Length));
        Assert.Equal(new ulong[] { 60, 93, 112, 117, 122, 122, 127 }, outcome.Traces.Select(t => t.RngState.DrawCount));
        var retained = new TilePosition(0, 2, 0);
        Assert.All(outcome.Traces.Take(2), t => Assert.Contains(t.RemainingAtExit, a => a.Position == retained));
        Assert.Contains(outcome.Traces[2].ConsumedAims, a => a.Position == retained);
        for (int i = 1; i < outcome.Traces.Length; i++)
        {
            Assert.Equal(outcome.Traces[i - 1].RngState, outcome.Traces[i].EntryRng);
            Assert.Equal(outcome.Traces[i - 1].RemainingAtExit.ToArray(), outcome.Traces[i].RemainingAtEntry.ToArray());
            Assert.DoesNotContain(outcome.Traces[i].Evaluations, e =>
                outcome.Traces.Take(i).SelectMany(t => t.ConsumedAims).Any(a => a.Position == e.Aim.Position));
        }
        Assert.All(outcome.Traces.SelectMany(t => t.Evaluations), e => {
            var c = e.Candidate.Outcome!;
            Assert.Equal((ushort)69, c.Units.Single(u => u.Id == Mage).Current.Mp);
            Assert.Equal(c.Accounting.IncludedTargets.Contains(Mage) ? (ushort)46 : (ushort)100,
                c.Units.Single(u => u.Id == Mage).Vitals.Hp);
        });
        Assert.Equal(new TilePosition(0, 4, 0), outcome.Best!.Origin.CastOrigin);
        Assert.Equal(new TilePosition(1, 1, 0), outcome.Best.Evaluation.Aim.Position);
        Assert.Empty(outcome.RemainingAims);
        Assert.Equal(new RandomState(1094920576, 127), result.RngState);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
        Assert.Equal(JsonSerializer.Serialize(outcome.Traces), JsonSerializer.Serialize(Query(catalog, state,
            OrdinarySpellAiAimPassKind.Tiles).Outcome!.Traces));
    }

    [Fact]
    public void Equal_rank_uses_final_ACT_first_destination_and_baseline_admission_remains_rank_only()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 0, range: 1, area: 2, targetX: 1);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(new[] { new TilePosition(1, 3, 0), new(0, 2, 0), new(1, 1, 0) },
            outcome.Traces.Select(t => t.Origin.CastOrigin));
        Assert.All(outcome.Traces, t => Assert.Empty(t.ConsumedAims));
        Assert.All(outcome.Traces.SelectMany(t => t.Evaluations), e => Assert.Equal((short)-22, e.Candidate.Outcome!.Score!.Priority));
        Assert.Equal(2, outcome.Traces.SelectMany(t => t.Evaluations).Count(e => e.ImprovedBest));
        Assert.True(outcome.Best!.Origin.ActBeforeMove);
        Assert.Equal(new TilePosition(0, 2, 0), outcome.Best.Origin.CastOrigin);
        Assert.Equal(new TilePosition(0, 4, 0), outcome.Best.Origin.MovementDestination);
        Assert.Equal((byte)7, outcome.Best.DestinationProximity);
        Assert.Single(outcome.RemainingAims);
        Assert.False(outcome.PassesBaseline);
        Assert.Equal(new RandomState(400935288, 39), result.RngState);
    }

    [Fact]
    public void Exhaustive_scan_uses_map_order_and_keeps_the_first_exact_tie()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 0, range: 1, area: 1, targetX: 2);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.False(outcome.Pass.RankedOrigins);
        Assert.Equal(new[] { new TilePosition(2, 1, 0), new(1, 2, 0), new(2, 3, 0) },
            outcome.Traces.Select(t => t.Origin.CastOrigin));
        Assert.All(outcome.Traces, t => Assert.Empty(t.ConsumedAims));
        Assert.Single(outcome.Traces.SelectMany(t => t.Evaluations), e => e.ImprovedBest);
        Assert.Equal(new TilePosition(2, 1, 0), outcome.Best!.Origin.CastOrigin);
        Assert.Single(outcome.RemainingAims);
        Assert.Equal(new RandomState(2064222289, 40), result.RngState);
    }

    [Fact]
    public void Unresolved_hit255_is_compared_before_exit_without_consumption_and_later_origins_continue()
    {
        var (catalog, state) = OrdinarySpellAiSearchTests.Congested(healing: false);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state, OrdinarySpellAiAimPassKind.Units);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.True(outcome.Pass.Potential.ConsumeEvaluatedAims);
        Assert.Equal(6, outcome.Traces.Length);
        Assert.All(outcome.Traces, t => {
            Assert.True(t.OriginStoppedUnresolved);
            Assert.Empty(t.ConsumedAims);
            Assert.Equal(15, t.RemainingAtExit.Length);
            var candidate = Assert.Single(t.Evaluations).Candidate;
            Assert.Equal(OrdinarySpellAiCandidateDisposition.ScoredUnresolved, candidate.Disposition);
            Assert.Equal((byte)255, candidate.Outcome!.Score!.HitPercent);
            Assert.Equal((short)-6774, candidate.Outcome.Score.Priority);
            Assert.Equal((ushort)75, candidate.Outcome.Units.Single(u => u.Id == Mage).Current.Mp);
            Assert.Equal(255UL, t.RngState.DrawCount - t.EntryRng.DrawCount);
        });
        for (int i = 1; i < outcome.Traces.Length; i++)
            Assert.Equal(outcome.Traces[i - 1].RngState, outcome.Traces[i].EntryRng);
        Assert.Equal(new TilePosition(0, 4, 0), outcome.Best!.Origin.CastOrigin);
        Assert.True(outcome.PassesBaseline);
        Assert.Equal(new RandomState(4232521854, 1537), result.RngState);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Nonvaluable_self_hits_prevent_dynamic_consumption_and_restore_private_resources()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 0, range: 3, area: 3, targetX: 2, alliesOnly: true);
        var result = Query(catalog, state, OrdinarySpellAiAimPassKind.Tiles);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(outcome.Pass.Origins.Length, outcome.Traces.Length);
        Assert.Null(outcome.Best);
        Assert.False(outcome.PassesBaseline);
        Assert.Equal(outcome.Pass.Potential.Aims.ToArray(), outcome.RemainingAims.ToArray());
        Assert.All(outcome.Traces, t => Assert.Empty(t.ConsumedAims));
        var evaluations = outcome.Traces.SelectMany(t => t.Evaluations).ToArray();
        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => {
            Assert.Equal(OrdinarySpellAiCandidateDisposition.NotValuable, e.Candidate.Disposition);
            Assert.Contains(Mage, e.Candidate.Outcome!.Accounting.IncludedTargets);
            Assert.Equal(4UL, e.Candidate.RngState.DrawCount - e.EntryRng.DrawCount);
            Assert.Equal((ushort)75, e.Candidate.Outcome.Units.Single(u => u.Id == Mage).Current.Mp);
        });
        Assert.Equal(outcome.Traces[0].EntryRng.DrawCount + 4UL * (ulong)evaluations.Length, result.RngState.DrawCount);
    }

    [Fact]
    public void Late_overflow_discards_the_earlier_scored_origin_and_restores_the_whole_entry_rng()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 0, range: 1, area: 2, targetX: 1);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var entry = new RandomState(57, ulong.MaxValue - 21); // Prefix12 + first origin9 fit; next origin fails.
        var result = Query(catalog, state, rng: entry);
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.Equal(entry, result.RngState);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Caster_frame_excluded_after_a_move_opportunity_does_not_set_the_source_self_hit_flag()
    {
        var setup = OrdinarySpellAiAimTests.Setup(charge: 9, range: 4, area: 2, targetX: 2);
        var d = setup.State.Definition;
        Assert.True(d.Map.TryGetTile(new(0, 0, 0), out var tile));
        var map = new BattleMapDefinition(7, 1, 1, Enumerable.Repeat(tile, 7).ToArray());
        var mage = d.Units[0] with { Move = 0, Occupant = d.Units[0].Occupant with { Position = new(0, 0, 0) } };
        var guard = d.Units[1] with { Occupant = d.Units[1].Occupant with { Position = new(2, 0, 0) },
            BaseAttributes = d.Units[1].BaseAttributes with { Speed = 1 }, RawStats = d.Units[1].RawStats with { Speed = 16384 } };
        var definition = new OrdinaryMagicBattleDefinition(map, [mage, guard], d.Spells.Values, d.Jobs,
            d.JobThresholds, d.InitialRng, d.MovementGround, d.Learning);
        var catalog = new OrdinarySpellAiCatalog(definition, setup.Catalog.PrimaryByJob);
        var state = OrdinaryMagicBattle.Apply(definition.Start(), new OrdinaryMagicCommand.Advance()).State;
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var trace = Assert.Single(result.Outcome!.Traces);
        var candidate = Assert.Single(trace.Evaluations).Candidate.Outcome!;
        Assert.Contains(candidate.Frames, f => f.TargetId == Mage);
        Assert.Contains(Mage, candidate.Clock!.HadMoveOpportunity);
        Assert.DoesNotContain(Guard, candidate.Clock.HadMoveOpportunity);
        Assert.DoesNotContain(Mage, candidate.Accounting.IncludedTargets);
        Assert.Contains(Guard, candidate.Accounting.IncludedTargets);
        Assert.False(result.Outcome.Pass.Potential.ConsumeEvaluatedAims);
        Assert.Single(trace.ConsumedAims);
        Assert.Equal((ushort)100, candidate.Units.Single(u => u.Id == Mage).Vitals.Hp);
        Assert.Equal((short)49, candidate.Score!.Priority);
        Assert.Equal(11UL, result.RngState.DrawCount); // Prefix2, one clock draw, two four-draw formula recipients.
    }

    [Fact]
    public void Empty_deadline_group_accepts_without_candidates_but_an_unavailable_pass_rejects_atomically()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup();
        var empty = Query(catalog, state, OrdinarySpellAiAimPassKind.UnitsMovingByDeadline);
        Assert.True(empty.Accepted, empty.Rejection);
        Assert.Empty(empty.Outcome!.Traces);
        Assert.Null(empty.Outcome.Best);
        Assert.False(empty.Outcome.PassesBaseline);
        Assert.Equal(empty.Outcome.Origins.Movement.Movement[^1].RngState, empty.RngState);
        var forced = OrdinarySpellAiAimTests.Setup(force: true);
        var rejected = Query(forced.Catalog, forced.State);
        Assert.False(rejected.Accepted);
        Assert.Null(rejected.Outcome);
        Assert.Equal(new RandomState(57, 0), rejected.RngState);
    }

    [Fact]
    public void Empty_recipient_list_still_completes_processing_and_consumes_an_adverse_no_self_aim_without_formula_draws()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(alliesOnly: true);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var trace = Assert.Single(result.Outcome!.Traces);
        var candidate = Assert.Single(trace.Evaluations).Candidate;
        Assert.Equal(OrdinarySpellAiCandidateDisposition.NotValuable, candidate.Disposition);
        Assert.Empty(candidate.Outcome!.Frames);
        Assert.Empty(candidate.Outcome.Accounting.IncludedTargets);
        Assert.Single(trace.ConsumedAims);
        Assert.Equal(trace.EntryRng, trace.RngState);
        Assert.Equal(15UL, result.RngState.DrawCount);
        Assert.Null(result.Outcome.Best);
    }

    [Fact]
    public void Equal_baseline_rank_is_not_admitted_despite_a_closer_best_destination()
    {
        var setup = OrdinarySpellAiAimTests.Setup(charge: 0);
        var d = setup.State.Definition;
        var spell = d.Spells.Values.Single();
        spell = spell with { MpCost = 0, Spell = spell.Spell with { Power = 0 } };
        var definition = new OrdinaryMagicBattleDefinition(d.Map, d.Units, [spell], d.Jobs, d.JobThresholds,
            d.InitialRng, d.MovementGround, d.Learning);
        var catalog = new OrdinarySpellAiCatalog(definition, setup.Catalog.PrimaryByJob.Select(p => KeyValuePair.Create(p.Key,
            p.Value with { Slots = p.Value.Slots.Select(s => s with { Spell = spell }).ToImmutableArray() })));
        var state = OrdinaryMagicBattle.Apply(definition.Start(), new OrdinaryMagicCommand.Advance()).State;
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal((short)0, outcome.Best!.Evaluation.Candidate.Outcome!.Score!.Priority);
        Assert.Equal((short)0, outcome.Origins.Movement.BaselineRank.Priority);
        Assert.True(outcome.Best.DestinationProximity < outcome.Origins.Movement.Cells
            .Single(c => c.Tile == outcome.Origins.Movement.BaselineDestination).Proximity);
        Assert.False(outcome.PassesBaseline);
    }
}
