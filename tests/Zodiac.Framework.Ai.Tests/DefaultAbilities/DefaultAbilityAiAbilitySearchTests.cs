using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiAbilitySearchTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard"), Other = new("other");
    private static DefaultAbilityAiAbilitySearchResult Query(DefaultAbilityAiCatalog catalog, OrdinaryMagicBattleState state,
        RandomState? rng = null, DefaultAbilityAiOriginScenario scenario = DefaultAbilityAiOriginScenario.Main) =>
        DefaultAbilityAiAbilitySearchQuery.Evaluate(new(state, catalog), new(Mage, 0, new(true), scenario), rng ?? new(57, 0));

    [Fact]
    public void Unit_best_survives_the_tile_pass_with_one_movement_prefix_and_continuous_rng()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, range: 4, area: 2, targetX: 2);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(new[] { DefaultAbilityAiAimPassKind.UnitsMovingByDeadline, DefaultAbilityAiAimPassKind.UnitsStillAtDeadline,
            DefaultAbilityAiAimPassKind.Tiles }, outcome.Passes.Select(p => p.Pass.Potential.Kind));
        Assert.Equal(new ulong[] { 13, 13, 18 }, outcome.Passes.Select(p => p.EntryRng.DrawCount));
        Assert.Equal(new ulong[] { 13, 18, 132 }, outcome.Passes.Select(p => p.RngState.DrawCount));
        Assert.Null(outcome.Passes[0].BestAtExit);
        var units = outcome.Passes[1];
        var tiles = outcome.Passes[2];
        Assert.Single(Assert.Single(units.Traces).ConsumedAims);
        Assert.Empty(units.RemainingAims);
        Assert.Equal(12, tiles.Traces[0].RemainingAtEntry.Length); // Fresh tile mask after the consumed unit mask.
        Assert.Same(units.BestAtExit, tiles.BestAtEntry);
        Assert.Same(tiles.BestAtEntry, tiles.BestAtExit);
        Assert.All(tiles.Traces.SelectMany(t => t.Evaluations), e => Assert.False(e.ImprovedBest));
        Assert.Equal(DefaultAbilityAiAimPassKind.UnitsStillAtDeadline, outcome.Best!.Pass);
        Assert.Equal(new TilePosition(0, 4, 0), outcome.Best.Candidate.Origin.CastOrigin);
        Assert.Equal(new TilePosition(2, 2, 0), outcome.Best.Candidate.Evaluation.Aim.Position);
        Assert.Equal((short)49, outcome.Best.Candidate.Evaluation.Candidate.Outcome!.Score!.Priority);
        Assert.True(outcome.PassesBaseline);
        Assert.Equal(new RandomState(399997557, 132), result.RngState);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
        Assert.Equal(JsonSerializer.Serialize(outcome.Passes), JsonSerializer.Serialize(Query(catalog, state).Outcome!.Passes));
        var reordered = Query(catalog, state.With(units: state.Units.Reverse().ToImmutableArray()));
        Assert.True(reordered.Accepted, reordered.Rejection);
        Assert.Equal(result.RngState, reordered.RngState);
        Assert.Equal(outcome.Passes.SelectMany(p => p.Traces).Select(t => t.Origin.CastOrigin),
            reordered.Outcome!.Passes.SelectMany(p => p.Traces).Select(t => t.Origin.CastOrigin));
        static IEnumerable<(TilePosition, RandomState, RandomState, bool, short?)> Evaluations(DefaultAbilityAiAbilitySearchOutcome o) =>
            o.Passes.SelectMany(p => p.Traces).SelectMany(t => t.Evaluations).Select(e =>
                (e.Aim.Position, e.EntryRng, e.Candidate.RngState, e.ImprovedBest, e.Candidate.Outcome!.Score?.Priority));
        Assert.Equal(Evaluations(outcome), Evaluations(reordered.Outcome));
    }

    [Fact]
    public void Moving_target_best_is_retained_through_the_empty_second_group_without_a_tile_pass()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 3);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(new[] { DefaultAbilityAiAimPassKind.UnitsMovingByDeadline, DefaultAbilityAiAimPassKind.UnitsStillAtDeadline },
            outcome.Passes.Select(p => p.Pass.Potential.Kind));
        Assert.Empty(outcome.Passes[1].Traces);
        Assert.Same(outcome.Passes[0].BestAtExit, outcome.Passes[1].BestAtExit);
        Assert.Equal(DefaultAbilityAiAimPassKind.UnitsMovingByDeadline, outcome.Best!.Pass);
        Assert.Equal(new[] { Guard }, outcome.Passes[0].Pass.Potential.UsefulTargets);
        Assert.Empty(outcome.Passes[1].Pass.Potential.UsefulTargets);
        Assert.Equal(19UL, result.RngState.DrawCount); // Prefix15, one four-draw delayed strike; no per-pass prefix rerun.
    }

    [Fact]
    public void Later_two_target_tile_cast_replaces_the_unit_best_without_adopting_prior_damage_or_mp()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, other: (4, 4, SpellTargetTeam.Red));
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal((short)-436, outcome.Passes[1].BestAtExit!.Candidate.Evaluation.Candidate.Outcome!.Score!.Priority);
        Assert.Equal(DefaultAbilityAiAimPassKind.Tiles, outcome.Best!.Pass);
        var best = outcome.Best.Candidate;
        Assert.Equal(new TilePosition(0, 3, 0), best.Origin.CastOrigin);
        Assert.Equal(new TilePosition(4, 3, 0), best.Evaluation.Aim.Position);
        Assert.Equal((short)-401, best.Evaluation.Candidate.Outcome!.Score!.Priority); // 499 - 450 - 450.
        Assert.All(best.Evaluation.Candidate.Outcome.Units.Where(u => u.Id != Mage), u => Assert.Equal((ushort)146, u.Vitals.Hp));
        Assert.Equal((ushort)69, best.Evaluation.Candidate.Outcome.Units.Single(u => u.Id == Mage).Current.Mp);
        Assert.Equal(59UL, result.RngState.DrawCount); // Prefix15, two unit attempts10, five single/one double tile attempt34.
        Assert.True(outcome.PassesBaseline);
    }

    [Theory]
    [InlineData(0, 40UL, 2064222289U)]
    [InlineData(2, 35UL, 1008742540U)]
    public void Equal_rank_in_a_later_tile_pass_replaces_best_only_for_lower_final_destination_proximity(
        byte charge, ulong draws, uint seed)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: charge);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        var unitBest = outcome.Passes[1].BestAtExit!.Candidate;
        var tileBest = outcome.Best!.Candidate;
        Assert.Equal(DefaultAbilityAiAimPassKind.Tiles, outcome.Best.Pass);
        Assert.Equal((short)49, unitBest.Evaluation.Candidate.Outcome!.Score!.Priority);
        Assert.Equal((short)49, tileBest.Evaluation.Candidate.Outcome!.Score!.Priority);
        Assert.Equal((byte)6, unitBest.DestinationProximity);
        Assert.Equal((byte)5, tileBest.DestinationProximity);
        Assert.Equal(new TilePosition(1, 4, 0), tileBest.Origin.CastOrigin);
        Assert.Equal(new TilePosition(3, 2, 0), tileBest.Evaluation.Aim.Position);
        Assert.Equal(new RandomState(seed, draws), result.RngState);
    }

    [Fact]
    public void Nonvaluable_self_hits_continue_into_tiles_without_resource_or_damage_adoption()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, range: 3, area: 3, targetX: 2, alliesOnly: true);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(new ulong[] { 13, 13, 53 }, outcome.Passes.Select(p => p.EntryRng.DrawCount));
        Assert.Equal(10, outcome.Passes[1].Traces.Length);
        Assert.Equal(12, outcome.Passes[2].Traces.Length);
        Assert.Equal(147, outcome.Passes[2].Traces.Sum(t => t.Evaluations.Length));
        Assert.All(outcome.Passes.SelectMany(p => p.Traces), t => {
            Assert.Empty(t.ConsumedAims);
            Assert.All(t.Evaluations, e => {
                Assert.Equal(DefaultAbilityAiCandidateDisposition.NotValuable, e.Candidate.Disposition);
                Assert.Contains(Mage, e.Candidate.Outcome!.Accounting.IncludedTargets);
                Assert.Equal((ushort)75, e.Candidate.Outcome.Units.Single(u => u.Id == Mage).Current.Mp);
                Assert.Equal((ushort)100, e.Candidate.Outcome.Units.Single(u => u.Id == Mage).Vitals.Hp);
            });
        });
        Assert.Null(outcome.Best);
        Assert.False(outcome.PassesBaseline);
        Assert.Equal(new RandomState(3390763518, 641), result.RngState); // Prefix13 + (10 unit +147 tile)×4.
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Split_deadline_membership_is_restored_then_moving_enemies_are_excluded_only_from_tile_usefulness()
    {
        var setup = DefaultAbilityAiAimTests.Setup(charge: 3, area: 2, targetX: 2, other: (4, 2, SpellTargetTeam.Red));
        var d = setup.State.Definition;
        var units = d.Units.Select(u => u.Occupant.UnitId == Other ? u with {
            BaseAttributes = u.BaseAttributes with { Speed = 1 }, RawStats = u.RawStats with { Speed = 16384 } } : u);
        var definition = new OrdinaryMagicBattleDefinition(d.Map, units, d.Spells.Values, d.Jobs, d.JobThresholds,
            d.InitialRng, d.MovementGround, d.Learning);
        var catalog = new DefaultAbilityAiCatalog(definition, setup.Catalog.PrimaryByJob);
        var state = OrdinaryMagicBattle.Apply(definition.Start(), new OrdinaryMagicCommand.Advance()).State;
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var passes = result.Outcome!.Passes;
        Assert.Equal(new[] { Guard }, passes[0].Pass.Potential.UsefulTargets);
        Assert.Equal(new[] { Other }, passes[1].Pass.Potential.UsefulTargets);
        Assert.Equal(new[] { Other }, passes[2].Pass.Potential.UsefulTargets);
        Assert.Equal(new ulong[] { 13, 21, 29 }, passes.Select(p => p.EntryRng.DrawCount)); // Both unit centres cover both peers:8 draws each.
        var moving = Assert.Single(Assert.Single(passes[0].Traces).Evaluations).Candidate.Outcome!;
        Assert.Equal(new[] { Guard, Other }, moving.Accounting.IncludedTargets);
        var still = Assert.Single(Assert.Single(passes[1].Traces).Evaluations).Candidate.Outcome!;
        Assert.Equal(new[] { Guard, Other }, still.Accounting.IncludedTargets);
        Assert.Equal((short)-401, still.Score!.Priority);
        Assert.Contains(passes[2].Pass.Potential.Aims, a => a.Position == new TilePosition(2, 2, 0));
        Assert.DoesNotContain(passes[2].Pass.Potential.Aims, a => a.Position == new TilePosition(4, 2, 0));
        Assert.Contains(passes[2].Traces.SelectMany(t => t.Evaluations).SelectMany(e => e.Candidate.Outcome!.Frames), f => f.TargetId == Guard);
        Assert.Equal(passes[0].RngState, passes[1].EntryRng);
        Assert.Equal(passes[1].RngState, passes[2].EntryRng);
    }

    [Fact]
    public void Forced_consumption_uses_one_unit_group_then_rebuilds_all_tile_aims()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2, targetX: 2, force: true);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(new[] { DefaultAbilityAiAimPassKind.Units, DefaultAbilityAiAimPassKind.Tiles },
            outcome.Passes.Select(p => p.Pass.Potential.Kind));
        Assert.Single(outcome.Passes[0].Traces.SelectMany(t => t.ConsumedAims));
        Assert.Equal(12, outcome.Passes[1].Traces.SelectMany(t => t.ConsumedAims).Count());
        Assert.All(outcome.Passes, p => Assert.Empty(p.RemainingAims));
        Assert.Equal(78UL, result.RngState.DrawCount); // Prefix13 + one unit/following twelve tile attempts at5 draws each.
        Assert.Equal(DefaultAbilityAiAimPassKind.Units, outcome.Best!.Pass);
    }

    [Fact]
    public void Nonfollowing_spell_has_only_tiles_and_does_not_xor_remove_the_enemy_panel()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2, targetX: 2, follow: false);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var pass = Assert.Single(result.Outcome!.Passes);
        Assert.Equal(DefaultAbilityAiAimPassKind.Tiles, pass.Pass.Potential.Kind);
        Assert.Contains(pass.Pass.Potential.Aims, a => a.Position == new TilePosition(2, 2, 0));
        Assert.Equal(13, pass.Traces.SelectMany(t => t.ConsumedAims).Count());
        Assert.Equal(132UL, result.RngState.DrawCount); // The followed tile vector127 plus one extra five-draw centre aim.
    }

    [Fact]
    public void Zero_area_following_spell_returns_after_the_single_unit_group()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 0);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var pass = Assert.Single(result.Outcome!.Passes);
        Assert.Equal(DefaultAbilityAiAimPassKind.Units, pass.Pass.Potential.Kind);
        Assert.Equal(20UL, result.RngState.DrawCount);
        Assert.True(result.Outcome.PassesBaseline);
    }

    [Fact]
    public void Unresolved_origins_continue_across_both_deadline_groups_and_a_fresh_tile_mask()
    {
        var setup = DefaultAbilityAiSearchTests.Congested(healing: false);
        var d = setup.State.Definition;
        var spell = d.Spells.Values.Single() with { RawChargeTime = 99 };
        var units = d.Units.Select(u => u.Occupant.SourceOrder == 15 ? u with {
            BaseAttributes = u.BaseAttributes with { Speed = 1 }, RawStats = u.RawStats with { Speed = 16384 } } : u);
        var definition = new OrdinaryMagicBattleDefinition(d.Map, units, [spell], d.Jobs, d.JobThresholds,
            d.InitialRng, d.MovementGround, d.Learning);
        var catalog = WithSpell(setup.Catalog, definition, spell);
        var start = definition.Start();
        var state = start.With(turns: new(start.Turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed,
            u.Id == Mage || u.Id == new BattleUnitId("peer15") ? 0 : 99, u.TieKey, u.Id == Mage)),
            new(57, 0), 10, TurnCyclePhase.UnitTurn), phase: OrdinaryMagicClockPhase.UnitTurn);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(new ulong[] { 7, 1537, 3067 }, outcome.Passes.Select(p => p.EntryRng.DrawCount));
        Assert.Equal(new[] { 14, 1, 4 }, outcome.Passes.Select(p => p.RemainingAims.Length));
        Assert.All(outcome.Passes, p => {
            Assert.Equal(6, p.Traces.Length);
            Assert.All(p.Traces, t => {
                Assert.True(t.OriginStoppedUnresolved);
                Assert.Empty(t.ConsumedAims);
                var candidate = Assert.Single(t.Evaluations).Candidate;
                Assert.Equal(DefaultAbilityAiCandidateDisposition.ScoredUnresolved, candidate.Disposition);
                Assert.Equal((byte)255, candidate.Outcome!.Score!.HitPercent);
            });
        });
        Assert.Equal(DefaultAbilityAiAimPassKind.UnitsMovingByDeadline, outcome.Best!.Pass);
        Assert.Same(outcome.Passes[0].BestAtExit, outcome.Passes[2].BestAtExit);
        Assert.True(outcome.PassesBaseline);
        Assert.Equal(4597UL, result.RngState.DrawCount); // One prefix7 + three groups of six255-event origins.
    }

    [Fact]
    public void Late_tile_overflow_rejects_the_whole_entry_after_a_scored_unit_pass()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(charge: 0, area: 2, targetX: 2);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var entry = new RandomState(57, ulong.MaxValue - 18);
        var result = Query(catalog, state, entry); // Prefix13 + unit5 fit exactly; the first tile attempt cannot finish.
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.Equal(entry, result.RngState);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Final_equal_baseline_rank_is_not_admitted_even_with_a_closer_destination()
    {
        var setup = DefaultAbilityAiAimTests.Setup(charge: 0);
        var d = setup.State.Definition;
        var spell = d.Spells.Values.Single();
        spell = spell with { MpCost = 0, Spell = spell.Spell with { Power = 0 } };
        var definition = new OrdinaryMagicBattleDefinition(d.Map, d.Units, [spell], d.Jobs, d.JobThresholds,
            d.InitialRng, d.MovementGround, d.Learning);
        var catalog = WithSpell(setup.Catalog, definition, spell);
        var state = OrdinaryMagicBattle.Apply(definition.Start(), new OrdinaryMagicCommand.Advance()).State;
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal((short)0, outcome.Best!.Candidate.Evaluation.Candidate.Outcome!.Score!.Priority);
        Assert.Equal((short)0, outcome.Origins.Movement.BaselineRank.Priority);
        Assert.True(outcome.Best.Candidate.DestinationProximity < outcome.Origins.Movement.Cells
            .Single(c => c.Tile == outcome.Origins.Movement.BaselineDestination).Proximity);
        Assert.False(outcome.PassesBaseline);
    }

    [Fact]
    public void Empty_complement_origins_keep_each_fresh_mask_and_spend_only_the_movement_prefix()
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(targetX: 6);
        var result = Query(catalog, state, scenario: DefaultAbilityAiOriginScenario.Complement);
        Assert.True(result.Accepted, result.Rejection);
        var outcome = result.Outcome!;
        Assert.Equal(3, outcome.Passes.Length);
        Assert.All(outcome.Passes, p => {
            Assert.Empty(p.Traces);
            Assert.Equal(p.Pass.Potential.Aims.ToArray(), p.RemainingAims.ToArray());
            Assert.Equal(p.EntryRng, p.RngState);
        });
        Assert.Equal(6UL, result.RngState.DrawCount);
        Assert.Null(outcome.Best);
        Assert.False(outcome.PassesBaseline);
    }

    [Theory]
    [InlineData(DefaultAbilityAiOriginScenario.Main)]
    [InlineData(DefaultAbilityAiOriginScenario.Complement)]
    public void Unknown_priority_rejects_with_no_partial_passes_or_rng(DefaultAbilityAiOriginScenario scenario)
    {
        var (catalog, state) = DefaultAbilityAiAimTests.Setup(targetX: 6, range: 6);
        var result = Query(catalog, state, scenario: scenario);
        Assert.False(result.Accepted);
        Assert.Contains("Unwritten cast-origin priority", result.Rejection);
        Assert.Null(result.Outcome);
        Assert.Equal(new RandomState(57, 0), result.RngState);
    }

    private static DefaultAbilityAiCatalog WithSpell(DefaultAbilityAiCatalog catalog, OrdinaryMagicBattleDefinition definition,
        MagicCastDefinition spell) => new(definition, catalog.PrimaryByJob.Select(p => KeyValuePair.Create(p.Key,
            p.Value with { Slots = p.Value.Slots.Select(s => s with { Spell = spell }).ToImmutableArray() })));
}
