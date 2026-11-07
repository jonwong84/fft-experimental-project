using System.Collections.Immutable;
using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class OrdinarySpellAiSearchTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard"), Other = new("other");

    private static OrdinarySpellAiSearchResult Query(OrdinarySpellAiCatalog catalog, OrdinaryMagicBattleState state,
        RandomState? rng = null) => OrdinarySpellAiSearchQuery.Evaluate(state, catalog, new(Mage, 0), rng ?? new(57, 0));

    [Theory]
    [InlineData(0, 10)] [InlineData(2, 8)] [InlineData(3, 4)] [InlineData(4, 5)] [InlineData(131, 4)]
    public void Source_order_uses_one_rng_and_exact_ties_keep_the_first_unit_aim(byte charge, ulong draws)
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: charge);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var best = result.Outcome!.Best!;
        Assert.Equal(new TilePosition(4, 2, 0), best.Aim.Position);
        Assert.Equal(Guard, best.Aim.UnitId);
        Assert.Equal((short)49, best.Candidate.Outcome!.Score!.Priority);
        Assert.Equal((byte)100, best.Candidate.Outcome.Score.HitPercent);
        Assert.Equal(draws, result.RngState.DrawCount);
        var evaluations = result.Outcome.Passes.SelectMany(p => p.Evaluations).ToArray();
        Assert.Single(evaluations, e => e.ImprovedBest);
        Assert.Equal(new RandomState(57, 0), evaluations[0].EntryRng);
        for (int i = 1; i < evaluations.Length; i++)
            Assert.Equal(evaluations[i - 1].Candidate.RngState, evaluations[i].EntryRng);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Query(catalog, state)));
    }

    [Fact]
    public void Better_area_rank_replaces_the_unit_aim_and_equal_later_area_keeps_the_first_best()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 0, area: 2, other: (2, 1, SpellTargetTeam.Red));
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var best = result.Outcome!.Best!;
        Assert.Equal(new TilePosition(3, 1, 0), best.Aim.Position);
        Assert.Null(best.Aim.UnitId);
        Assert.Equal((short)-401, best.Candidate.Outcome!.Score!.Priority);
        Assert.Equal((byte)100, best.Candidate.Outcome.Score.HitPercent);
        Assert.Equal(88UL, result.RngState.DrawCount);
        Assert.Equal(3396639393U, result.RngState.Value); // Existing BIOS-v3 LCG contract, advanced88 draws from57.
        Assert.Equal(12, result.Outcome.Passes.Sum(p => p.Evaluations.Length));
        Assert.Equal(2, result.Outcome.Passes.SelectMany(p => p.Evaluations).Count(e => e.ImprovedBest));
        var sameRank = Assert.Single(result.Outcome.Passes.SelectMany(p => p.Evaluations), e => e.Aim.Position == new TilePosition(3, 2, 0));
        Assert.Equal(best.Candidate.Outcome.Score.Priority, sameRank.Candidate.Outcome!.Score!.Priority);
        Assert.False(sameRank.ImprovedBest);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Pass_membership_changes_value_without_removing_collateral_formula_recipients_or_scores()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 3, other: (3, 2, SpellTargetTeam.Red));
        var turns = state.Turns;
        state = state.With(turns: new(turns.Units.Select(t => t.Id == Other ? new(t.Id, t.Speed, 0, t.TieKey) : t),
            turns.RngState, turns.Ticks, turns.Phase));
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        var first = Assert.Single(result.Outcome!.Passes[0].Evaluations).Candidate.Outcome!;
        Assert.Equal(new[] { Guard }, first.Usefulness.Where(u => u.Effect == OrdinarySpellAiEffect.Useful).Select(u => u.UnitId));
        Assert.Equal(2, first.Frames.Length);
        Assert.Equal((ushort)146, first.Units.Single(u => u.Id == Guard).Vitals.Hp);
        Assert.Equal((ushort)146, first.Units.Single(u => u.Id == Other).Vitals.Hp);
        Assert.Equal((short)-401, first.Score!.Priority);
        var second = Assert.Single(result.Outcome.Passes[1].Evaluations).Candidate.Outcome!;
        Assert.Equal(new[] { Other }, second.Usefulness.Where(u => u.Effect == OrdinarySpellAiEffect.Useful).Select(u => u.UnitId));
        Assert.Equal(2, second.Frames.Length);
        Assert.Equal(Guard, result.Outcome.Best!.Aim.UnitId);
        Assert.All(result.Outcome.Passes[2].Evaluations, e => Assert.Equal(new[] { Other },
            e.Candidate.Outcome!.Usefulness.Where(u => u.Effect == OrdinarySpellAiEffect.Useful).Select(u => u.UnitId)));
    }

    [Fact]
    public void Nonvaluable_formula_draws_continue_to_later_aims_without_partial_unit_or_mp_adoption()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 0, range: 3, area: 3, targetX: 2, alliesOnly: true);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        Assert.Null(result.Outcome!.Best);
        var evaluations = result.Outcome.Passes.SelectMany(p => p.Evaluations).ToArray();
        Assert.True(evaluations.Length > 1);
        Assert.Equal(OrdinarySpellAiCandidateDisposition.NotValuable, evaluations[0].Candidate.Disposition);
        Assert.Equal(4UL, evaluations[0].Candidate.RngState.DrawCount);
        Assert.Equal(evaluations[0].Candidate.RngState, evaluations[1].EntryRng);
        Assert.True(result.RngState.DrawCount > 4);
        Assert.All(evaluations, e => Assert.Equal(OrdinarySpellAiCandidateDisposition.NotValuable, e.Candidate.Disposition));
        Assert.All(result.Outcome.Passes, p => Assert.False(p.OriginStoppedUnresolved));
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Fact]
    public void Healing_keeps_the_moving_ally_tile_pass_and_draws_only_clock_upkeep()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 4, healing: true);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(2UL, result.RngState.DrawCount);
        Assert.Equal(2, result.Outcome!.Passes.Sum(p => p.Evaluations.Length));
        Assert.Equal((short)909, result.Outcome.Best!.Candidate.Outcome!.Score!.Priority);
        Assert.Equal(Guard, result.Outcome.Best.Aim.UnitId);
        Assert.Equal((ushort)84, result.Outcome.Best.Candidate.Outcome.Units.Single(u => u.Id == Guard).Vitals.Hp);
    }

    private static (OrdinarySpellAiCatalog Catalog, OrdinaryMagicBattleState State) Congested(bool healing)
    {
        var x = OrdinarySpellAiReadyTests.Setup(charge: 127, heal: healing);
        var d = x.Content.Definition;
        var mage = d.Units[0] with { BaseAttributes = d.Units[0].BaseAttributes with { Speed = 1 },
            RawStats = d.Units[0].RawStats with { Speed = 16384 } };
        var panels = Enumerable.Range(0, 35).Select(i => new TilePosition(i % 7, i / 7, 0))
            .Where(p => p != mage.Occupant.Position).Take(15).ToArray();
        var peers = Enumerable.Range(1, 15).Select(i => d.Units[1] with {
            Occupant = d.Units[1].Occupant with { UnitId = new("peer" + i), SourceOrder = i, Position = panels[i - 1] },
            Current = d.Units[1].Current with { UnitId = new("peer" + i) },
            BaseAttributes = d.Units[1].BaseAttributes with { Speed = 50 }, RawStats = d.Units[1].RawStats with { Speed = 819200 } });
        var definition = new OrdinaryMagicBattleDefinition(d.Map, new[] { mage }.Concat(peers), d.Spells.Values, d.Jobs,
            d.JobThresholds, d.InitialRng, d.MovementGround, d.Learning);
        var catalog = new OrdinarySpellAiCatalog(definition, x.Catalog.PrimaryByJob);
        var start = definition.Start();
        return (catalog, start.With(turns: new(start.Turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed,
            u.Id == Mage ? 0 : 99, u.TieKey, u.Id == Mage)), new(57, 0), 10, TurnCyclePhase.UnitTurn), phase: OrdinaryMagicClockPhase.UnitTurn));
    }

    [Theory]
    [InlineData(false, 1, 255, -6774)] [InlineData(true, 2, 510, 5661)]
    public void Unresolved_hit255_is_ranked_before_stopping_only_that_origin_pass(bool healing, int passCount, ulong draws, short priority)
    {
        var (catalog, state) = Congested(healing);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(passCount, result.Outcome!.Passes.Length);
        Assert.All(result.Outcome.Passes, pass => {
            Assert.True(pass.Potential.Aims.Length > 1);
            Assert.True(pass.OriginStoppedUnresolved);
            Assert.Equal(OrdinarySpellAiCandidateDisposition.ScoredUnresolved, Assert.Single(pass.Evaluations).Candidate.Disposition);
        });
        Assert.Equal(draws, result.RngState.DrawCount);
        var best = result.Outcome.Best!;
        Assert.Equal((byte)255, best.Candidate.Outcome!.Score!.HitPercent);
        Assert.Equal(priority, best.Candidate.Outcome.Score.Priority);
        Assert.Equal((ushort)75, best.Candidate.Outcome.Units.Single(u => u.Id == Mage).Current.Mp);
        Assert.Same(result.Outcome.Passes[0].Evaluations[0], best);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Later_budget_or_unsupported_caster_ko_discards_prior_best_and_restores_whole_entry(bool casterKo)
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(charge: 0, area: casterKo ? (byte)2 : (byte)1,
            other: casterKo ? (2, 1, SpellTargetTeam.Red) : null);
        if (casterKo)
            state = state.With(units: state.Units.SetItem(0, state.Units[0] with { Vitals = state.Units[0].Vitals with { Hp = 30 } }));
        var rng = casterKo ? new RandomState(57, 0) : new RandomState(57, ulong.MaxValue - 9);
        string before = OrdinarySpellAiReadyTests.Receipt(state);
        var first = OrdinarySpellAiCandidateQuery.Evaluate(state, catalog, new(Mage, 0,
            casterKo ? new(2, 1, 0) : new(4, 2, 0)), rng);
        Assert.Equal(OrdinarySpellAiCandidateDisposition.Scored, first.Disposition);
        var result = Query(catalog, state, rng);
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.Equal(rng, result.RngState);
        Assert.Equal(before, OrdinarySpellAiReadyTests.Receipt(state));
    }

    [Theory]
    [InlineData("foreign")] [InlineData("slot")] [InlineData("actor")] [InlineData("pending")]
    public void Fresh_boundary_rejects_foreign_catalog_unowned_slot_wrong_actor_and_pending_action(string change)
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup();
        var request = new OrdinarySpellAiSearchRequest(change == "actor" ? new("missing") : Mage, change == "slot" ? 1 : 0);
        if (change == "foreign") catalog = OrdinarySpellAiAimTests.Setup().Catalog;
        if (change == "pending") state = OrdinaryMagicBattle.Apply(state,
            new OrdinaryMagicCommand.Cast(Mage, new("test:spell/fire"), new(4, 2, 0))).State;
        var rng = new RandomState(57, 9);
        var result = OrdinarySpellAiSearchQuery.Evaluate(state, catalog, request, rng);
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.Equal(rng, result.RngState);
    }

    [Fact]
    public void No_useful_targets_return_an_empty_search_without_rng_or_a_best()
    {
        var (catalog, state) = OrdinarySpellAiAimTests.Setup(healing: true);
        state = state.With(units: state.Units.SetItem(1, state.Units[1] with { Vitals = state.Units[1].Vitals with { Hp = 200, Critical = false } }));
        var rng = new RandomState(57, 9);
        var result = Query(catalog, state, rng);
        Assert.True(result.Accepted, result.Rejection);
        Assert.Empty(result.Outcome!.Passes);
        Assert.Null(result.Outcome.Best);
        Assert.Equal(rng, result.RngState);
    }
}
