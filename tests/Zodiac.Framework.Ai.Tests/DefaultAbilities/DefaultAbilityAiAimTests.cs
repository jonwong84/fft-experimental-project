using System.Collections.Immutable;
using TestSupport;
using Xunit;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiAimTests
{
    private static readonly BattleUnitId Mage = new("mage"), Guard = new("guard"), Other = new("other");

    internal static (DefaultAbilityAiCatalog Catalog, DefaultAbilityBattleState State) Setup(
        byte charge = 2, byte area = 1, byte range = 4, bool follow = true, bool noFollow = false,
        bool force = false, bool healing = false, int width = 7, int actorX = 0, int targetX = 4, int targetY = 2, bool alliesOnly = false,
        (int X, int Y, SpellTargetTeam Team)? other = null, AiAbilityBehavior? behavior = null)
    {
        var d = AuthoredDefaultAbility.Document() with { SchemaVersion = 3, MetadataPackageId = "test:package/aims", Width = width };
        d.Spells[0] = d.Spells[0] with { ChargeTime = charge,
            Targeting = d.Spells[0].Targeting with { Area = area, Range = range, NoFollowTarget = noFollow, CanHitEnemies = force, CanHitAllies = alliesOnly },
            Formula = healing ? new(FaithMagicAmountKind.Healing, 14, ElementToggles.None, false) : d.Spells[0].Formula };
        d.Units[0] = d.Units[0] with { X = actorX, LearnedPrimarySlots = [0] };
        d.Units[1] = d.Units[1] with { X = targetX, Y = targetY, LearnedPrimarySlots = [],
            Team = healing ? SpellTargetTeam.Blue : SpellTargetTeam.Red, Hp = healing ? (ushort)30 : (ushort)200 };
        if (other is { } o)
            d = d with { Units = [.. d.Units, d.Units[1] with { Id = Other.Value, SourceOrder = 2, X = o.X, Y = o.Y, Team = o.Team }] };
        behavior ??= healing
            ? new(TargetsAllies: true, AffectsHp: true, AffectedByFaith: true, FollowTarget: follow, Reflectable: true)
            : DefaultAbilityAiListTests.FireBehavior with { FollowTarget = follow };
        var metadata = new ContentPackage(d.MetadataPackageId!, "test:profile/aims", ContentPackageKind.Baseline, null, [],
            [new(new("test:skillset/magic"), [new(0, new(AuthoredDefaultAbility.Fire))])],
            [new(new(AuthoredDefaultAbility.Fire), 50, 100, 1, behavior, true)]);
        var content = PreparedDefaultAbilityBattle.Create(d, metadata);
        var spell = content.Definition.Spells[new(AuthoredDefaultAbility.Fire)];
        var catalog = new DefaultAbilityAiCatalog(content.Definition,
            new Dictionary<AssetId, DefaultAbilityAiSkillset> { [new(AuthoredDefaultAbility.Wizard)] =
                new(11, new("test:skillset/magic"), [new(0, spell, behavior, true)]) });
        return (catalog, DefaultAbilityBattle.Apply(content.Definition.Start(), new DefaultAbilityCommand.Advance()).State);
    }

    private static DefaultAbilityAiAimOutcome Query(DefaultAbilityAiCatalog catalog, DefaultAbilityBattleState state)
    {
        var result = DefaultAbilityAiAimQuery.Evaluate(state, catalog, new(Mage, 0));
        Assert.True(result.Accepted, result.Rejection);
        return result.Outcome!;
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(2, false, false)]
    [InlineData(3, true, false)]
    [InlineData(10, true, true)]
    [InlineData(131, true, true)]
    public void Source_deadline_boundary_and_completed_usage_choose_the_passes(byte charge, bool moves, bool consume)
    {
        // Actor CT0/Speed10 => delay10. Guard CT80/Speed8 => delay3. Considered CT131 stays131, not3.
        var (catalog, state) = Setup(charge: charge);
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = Query(catalog, state);
        Assert.Equal(charge, result.Ability.ChargeTime);
        Assert.Equal(consume, result.Ability.Usage.ConsumeAim);
        Assert.Equal(3, result.Deadlines.Single(d => d.UnitId == Guard).TurnDelay);
        Assert.Equal(moves, result.Deadlines.Single(d => d.UnitId == Guard).CanMoveByChargeDeadline);
        if (consume)
        {
            var pass = Assert.Single(result.Passes);
            Assert.Equal(DefaultAbilityAiAimPassKind.Units, pass.Kind);
            Assert.True(pass.ConsumeEvaluatedAims);
        }
        else
        {
            Assert.Equal(DefaultAbilityAiAimPassKind.UnitsMovingByDeadline, result.Passes[0].Kind);
            Assert.Equal(DefaultAbilityAiAimPassKind.UnitsStillAtDeadline, result.Passes[1].Kind);
            Assert.True(result.Passes[0].ConsumeEvaluatedAims);
            Assert.False(result.Passes[1].ConsumeEvaluatedAims);
            Assert.Equal(moves, !result.Passes[0].Aims.IsEmpty);
            Assert.Equal(!moves, !result.Passes[1].Aims.IsEmpty);
            Assert.Equal(moves ? 2 : 3, result.Passes.Length);
        }
        var unitAim = Assert.Single(result.Passes.SelectMany(p => p.Aims), a => a.UnitId is not null);
        Assert.Equal(new TilePosition(4, 2, 0), unitAim.Position);
        Assert.Equal(Guard, unitAim.UnitId);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
        var replay = Query(catalog, state);
        Assert.Equal(result.Passes.SelectMany(p => p.Aims), replay.Passes.SelectMany(p => p.Aims));
    }

    [Fact]
    public void Source_mask_union_xor_and_range_intersection_scan_panels_instead_of_roster_order()
    {
        var (catalog, state) = Setup(charge: 0, other: (2, 1, SpellTargetTeam.Red));
        var result = Query(catalog, state);
        var units = result.Passes[1];
        Assert.Equal(new[] { Guard, Other }, units.UsefulTargets);
        Assert.Equal(new BattleUnitId?[] { Other, Guard }, units.Aims.Select(a => a.UnitId));
        var tiles = result.Passes[2];
        Assert.Equal(new[] { new TilePosition(2, 0, 0), new(1, 1, 0), new(3, 1, 0), new(2, 2, 0), new(3, 2, 0) },
            tiles.Aims.Select(a => a.Position));
        Assert.All(tiles.Aims, a => Assert.Null(a.UnitId));
        var reversed = Query(catalog, state.With(units: state.Units.Reverse().ToImmutableArray()));
        Assert.Equal(result.Passes.SelectMany(p => p.Aims), reversed.Passes.SelectMany(p => p.Aims));
    }

    [Fact]
    public void Occupied_aim_outside_move_range_can_cover_a_target_outside_cast_range()
    {
        var (catalog, state) = Setup(charge: 0, targetX: 5, other: (4, 2, SpellTargetTeam.Blue));
        var result = Query(catalog, state);
        Assert.Empty(result.Passes[0].Aims);
        Assert.Empty(result.Passes[1].Aims);
        var tile = Assert.Single(result.Passes[2].Aims);
        Assert.Equal(new TilePosition(4, 2, 0), tile.Position);
        Assert.Contains(state.Units, u => u.Id == Other && u.Position == tile.Position);
        Assert.Equal(3, state.Units[0].Definition.Move);
    }

    [Fact]
    public void Tile_xor_removes_both_valuable_centers_even_when_their_masks_overlap()
    {
        var (catalog, state) = Setup(charge: 0, other: (3, 2, SpellTargetTeam.Red));
        var result = Query(catalog, state);
        Assert.Equal(new[] { new TilePosition(3, 1, 0), new(2, 2, 0), new(3, 3, 0) },
            result.Passes[2].Aims.Select(a => a.Position));
    }

    [Fact]
    public void No_follow_ai_uses_tiles_and_retains_the_unit_center_even_when_ui_also_offers_units()
    {
        var (catalog, state) = Setup(charge: 0, follow: false);
        var pass = Assert.Single(Query(catalog, state).Passes);
        Assert.Equal(DefaultAbilityAiAimPassKind.Tiles, pass.Kind);
        Assert.Equal(new[] { new TilePosition(3, 2, 0), new(4, 2, 0) }, pass.Aims.Select(a => a.Position));
        var tileOnly = Setup(charge: 0, follow: false, noFollow: true);
        Assert.Equal(pass.Aims.ToArray(), Assert.Single(Query(tileOnly.Catalog, tileOnly.State).Passes).Aims.ToArray());
    }

    [Fact]
    public void Allied_healing_keeps_moving_ally_tiles_and_self_none_completes_consume_policy()
    {
        var (catalog, state) = Setup(charge: 4, healing: true);
        var result = Query(catalog, state);
        Assert.True(result.Ability.Usage.ConsumeAim);
        Assert.True(result.Deadlines.Single(d => d.UnitId == Guard).CanMoveByChargeDeadline);
        Assert.Equal(new[] { DefaultAbilityAiAimPassKind.Units, DefaultAbilityAiAimPassKind.Tiles }, result.Passes.Select(p => p.Kind));
        Assert.Equal(new TilePosition(3, 2, 0), Assert.Single(result.Passes[1].Aims).Position);
        var full = state.With(units: state.Units.SetItem(1, state.Units[1] with { Vitals = state.Units[1].Vitals with { Hp = 200, Critical = false } }));
        Assert.Empty(Query(catalog, full).Passes);
    }

    [Fact]
    public void Forced_consumption_zero_area_and_mask_edges_preserve_source_policy()
    {
        var forced = Setup(force: true);
        var r = Query(forced.Catalog, forced.State);
        Assert.True(r.Ability.Usage.ForceConsumeAim);
        Assert.Equal(DefaultAbilityAiAimPassKind.Units, r.Passes[0].Kind);
        Assert.True(r.Passes[1].ConsumeEvaluatedAims);
        var zero = Setup(area: 0, width: 16, actorX: 11, targetX: 15);
        var pass = Assert.Single(Query(zero.Catalog, zero.State).Passes);
        Assert.Equal(new TilePosition(15, 2, 0), Assert.Single(pass.Aims).Position);
        var edge = Setup(charge: 0, area: 2, targetX: 0, targetY: 0);
        Assert.Equal(new[] { new TilePosition(1, 0, 0), new(2, 0, 0), new(0, 1, 0), new(1, 1, 0), new(0, 2, 0) },
            Query(edge.Catalog, edge.State).Passes.Last().Aims.Select(a => a.Position));
    }

    [Fact]
    public void Deadline_ignores_already_used_movement_and_instant_ct_is_never_a_move_opportunity()
    {
        var (catalog, state) = Setup(charge: 3);
        var turns = state.Turns;
        state = state.With(turns: new(turns.Units.Select(t => t.Id == Guard ? new(t.Id, t.Speed, t.Ct, t.TieKey, movementTaken: true) : t),
            turns.RngState, turns.Ticks, turns.Phase));
        Assert.True(Query(catalog, state).Deadlines.Single(d => d.UnitId == Guard).CanMoveByChargeDeadline);
        var instant = Setup(charge: 0);
        turns = instant.State.Turns;
        state = instant.State.With(turns: new(turns.Units.Select(t => t.Id == Guard ? new(t.Id, t.Speed, 100, t.TieKey) : t),
            turns.RngState, turns.Ticks, turns.Phase));
        var deadline = Query(instant.Catalog, state).Deadlines.Single(d => d.UnitId == Guard);
        Assert.Equal(0, deadline.TurnDelay);
        Assert.False(deadline.CanMoveByChargeDeadline);
    }

    [Fact]
    public void No_follow_moving_enemy_produces_no_tile_pass_and_corpse_rosters_stay_gated()
    {
        var (catalog, state) = Setup(charge: 3, follow: false);
        Assert.Empty(Query(catalog, state).Passes);
        var guard = state.Units[1];
        state = state.With(units: state.Units.SetItem(1, guard with {
            Vitals = guard.Vitals with { Hp = 0, Critical = false }, Current = guard.Current with { Statuses = new(Dead: true) } }));
        var dead = DefaultAbilityAiAimQuery.Evaluate(state, catalog, new(Mage, 0));
        Assert.False(dead.Accepted);
        Assert.Null(dead.Outcome);
    }

    [Fact]
    public void Safe_radius_seven_reaches_the_source_bit_boundary()
    {
        var (catalog, state) = Setup(charge: 0, area: 0, range: 7, width: 16, actorX: 8, targetX: 15);
        var result = Query(catalog, state);
        Assert.Equal(new TilePosition(15, 2, 0), Assert.Single(Assert.Single(result.Passes).Aims).Position);
    }

    [Fact]
    public void Short_evadeable_reach_keeps_exhaustive_policy_instead_of_forcing_zero_area_consumption()
    {
        var (catalog, state) = Setup(charge: 10, area: 0, range: 1, targetX: 1);
        var result = Query(catalog, state);
        Assert.True(result.Ability.Usage.ExhaustiveOrigins);
        Assert.False(result.Ability.Usage.ForceConsumeAim);
        Assert.False(result.Ability.Usage.ConsumeAim);
        Assert.All(result.Passes, p => Assert.False(p.ConsumeEvaluatedAims));
        Assert.Equal(new[] { DefaultAbilityAiAimPassKind.UnitsMovingByDeadline, DefaultAbilityAiAimPassKind.UnitsStillAtDeadline },
            result.Passes.Select(p => p.Kind));
        Assert.Equal(Guard, Assert.Single(result.Passes[0].Aims).UnitId);
    }

    [Theory]
    [InlineData("actor")] [InlineData("slot")] [InlineData("foreign")] [InlineData("mp")]
    [InlineData("pending")] [InlineData("learned")] [InlineData("self")] [InlineData("status")]
    [InlineData("ground")] [InlineData("choice")] [InlineData("radius")] [InlineData("width")]
    [InlineData("zero-range")] [InlineData("trajectory")] [InlineData("shape")]
    public void Unsupported_facts_reject_without_partial_aims_or_live_changes(string change)
    {
        var x = Setup(noFollow: change == "choice", range: change == "radius" ? (byte)7 : change == "zero-range" ? (byte)0 : (byte)4,
            width: change == "width" ? 17 : 7, healing: change == "self",
            behavior: change == "trajectory" ? DefaultAbilityAiListTests.FireBehavior with { DirectTrajectory = true } :
                change == "shape" ? DefaultAbilityAiListTests.FireBehavior with { AreaShape = AiAbilityAreaShape.Linear } : null);
        var state = x.State;
        var catalog = x.Catalog;
        var request = new DefaultAbilityAiAimRequest(change == "actor" ? new("missing") : Mage, change == "slot" ? 1 : 0);
        if (change == "foreign") catalog = Setup().Catalog;
        var mage = state.Units[0];
        if (change == "mp") state = state.With(units: state.Units.SetItem(0, mage with { Current = mage.Current with { Mp = 0 } }));
        if (change == "learned") state = state.With(units: state.Units.SetItem(0, mage with {
            LearnedAbilitySlots = mage.LearnedAbilitySlots.SetItem(mage.Definition.RewardJobId, []) }));
        if (change == "self") state = state.With(units: state.Units.SetItem(0, mage with { Vitals = mage.Vitals with { Hp = 30 } }));
        if (change == "status") state = state.With(units: state.Units.SetItem(1, state.Units[1] with {
            Current = state.Units[1].Current with { Statuses = new(Sleep: true) } }));
        if (change == "pending") state = DefaultAbilityBattle.Apply(state,
            new DefaultAbilityCommand.Cast(Mage, new(AuthoredDefaultAbility.Fire), new(4, 2, 0))).State;
        if (change == "ground")
        {
            var d = state.Definition;
            var unbound = new DefaultAbilityBattleDefinition(d.Map, d.Units, d.Spells.Values, d.Jobs, d.JobThresholds, d.InitialRng, learning: d.Learning);
            catalog = new(unbound, catalog.PrimaryByJob);
            state = DefaultAbilityBattle.Apply(unbound.Start(), new DefaultAbilityCommand.Advance()).State;
        }
        string before = DefaultAbilityAiReadyTests.Receipt(state);
        var result = DefaultAbilityAiAimQuery.Evaluate(state, catalog, request);
        Assert.False(result.Accepted);
        Assert.Null(result.Outcome);
        Assert.NotNull(result.Rejection);
        Assert.Equal(before, DefaultAbilityAiReadyTests.Receipt(state));
    }
}
