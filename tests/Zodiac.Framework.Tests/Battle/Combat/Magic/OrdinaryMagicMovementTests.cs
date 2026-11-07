using System.Collections.Immutable;
using Zodiac.Framework.Random;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class OrdinaryMagicMovementTests
{
    private static readonly TerrainSurfaceId Ground = new("test:terrain/ordinary");
    private static BattleUnitId Mage => OrdinaryMagicBattleTestData.Mage;
    private static BattleUnitId Target => OrdinaryMagicBattleTestData.Target;
    private static OrdinaryMagicBattleDefinition Definition() => Copy(OrdinaryMagicBattleTestData.Definition());
    private static OrdinaryMagicBattleDefinition Copy(OrdinaryMagicBattleDefinition d,
        ImmutableArray<OrdinaryMagicUnitDefinition>? units = null, BattleMapDefinition? map = null) =>
        new(map ?? new(7, 5, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 35).ToArray()),
            units ?? d.Units, d.Spells.Values, d.Jobs, d.JobThresholds, d.InitialRng, Ground);
    private static OrdinaryMagicBattleState Active(OrdinaryMagicBattleDefinition? definition = null) =>
        OrdinaryMagicBattleTestData.Apply((definition ?? Definition()).Start(), new OrdinaryMagicCommand.Advance());

    [Fact]
    public void Fresh_query_and_confirmed_path_share_source_backtrace_and_publish_final_heading()
    {
        var before = Active(); var destination = new TilePosition(2, 1, 0);
        var query = OrdinaryMagicMovement.Query(before, Mage, destination);
        Assert.True(query.Accepted); var path = query.Outcome!.Path!;
        Assert.Equal(new[] { new TilePosition(0, 2, 0), new(1, 2, 0), new(2, 2, 0), destination }, path.Tiles.ToArray());
        Assert.Equal(new byte[] { 0, 0, 128 }, path.PackedSteps.ToArray()); Assert.Equal(BattleFacing.South, path.FinalHeading);
        Assert.Equal(before.Turns.RngState, query.RngState);
        var result = OrdinaryMagicBattle.Apply(before, new OrdinaryMagicCommand.Move(Mage, destination));
        Assert.True(result.Accepted, result.Rejection); var after = result.State;
        Assert.Equal(destination, after.Units[0].Position); Assert.Equal(BattleFacing.South, after.Units[0].Facing);
        Assert.Equal(before.Units[0].Current, after.Units[0].Current); Assert.Equal(before.Units[0].Vitals, after.Units[0].Vitals);
        Assert.Same(before.Units[1], after.Units[1]); Assert.Equal(before.Turns.RngState, after.Turns.RngState);
        Assert.Equal(before.Turns.Ticks, after.Turns.Ticks); Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(Mage, after.Turns.ActiveUnitId); Assert.True(after.Turns.Units[0].MovementTaken); Assert.False(after.Turns.Units[0].ActionTaken);
        var ev = Assert.IsType<OrdinaryMagicEvent.Moved>(Assert.Single(result.Events));
        Assert.Equal(BattleFacing.East, ev.PreviousFacing); Assert.Equal(path.Tiles.ToArray(), ev.Path.Tiles.ToArray());
        Assert.Equal(new TilePosition(0, 2, 0), before.Units[0].Position);
    }

    [Fact]
    public void Act_then_walk_preserves_magic_charge_and_ends_the_turn_once_without_payment_or_ticks()
    {
        var before = Active(); before = OrdinaryMagicBattleTestData.Apply(before,
            new OrdinaryMagicCommand.Cast(Mage, OrdinaryMagicBattleTestData.Fire, new(4, 2, 0)));
        var plan = before.Pending[Mage];
        var result = OrdinaryMagicBattle.Apply(before, new OrdinaryMagicCommand.Move(Mage, new(2, 1, 0)));
        Assert.True(result.Accepted, result.Rejection); var state = result.State;
        Assert.Null(state.Turns.ActiveUnitId); Assert.Equal(0, state.Turns.Units[0].Ct);
        Assert.True(state.Units[0].Current.Statuses.Charging); Assert.Same(plan, state.Pending[Mage]);
        Assert.Equal((ushort)75, state.Units[0].Current.Mp); Assert.Equal(before.Turns.RngState, state.Turns.RngState);
        Assert.IsType<OrdinaryMagicEvent.Moved>(result.Events[0]); Assert.IsType<OrdinaryMagicEvent.TurnEnded>(result.Events[1]);
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Advance());
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Wait(Target));
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Advance());
        Assert.Equal((ushort)146, state.Units[1].Vitals.Hp); Assert.Equal((ushort)69, state.Units[0].Current.Mp);
        Assert.Equal(BattleFacing.South, state.Units[0].Facing); Assert.Equal(13UL, state.Turns.Ticks);
    }

    [Fact]
    public void Move_then_cast_rebuilds_range_and_consumes_both_choices_without_refunding_ct()
    {
        var before = Active(); var state = OrdinaryMagicBattleTestData.Apply(before, new OrdinaryMagicCommand.Move(Mage, new(2, 1, 0)));
        state = OrdinaryMagicBattleTestData.Apply(state, new OrdinaryMagicCommand.Cast(Mage, OrdinaryMagicBattleTestData.Fire, new(4, 2, 0)));
        Assert.Null(state.Turns.ActiveUnitId); Assert.Equal(0, state.Turns.Units[0].Ct); Assert.Equal((byte)4, state.Pending[Mage].Counter);
        Assert.Equal((ushort)75, state.Units[0].Current.Mp);
        var away = OrdinaryMagicBattleTestData.Apply(before, new OrdinaryMagicCommand.Move(Mage, new(0, 0, 0)));
        var rejected = OrdinaryMagicBattle.Apply(away, new OrdinaryMagicCommand.Cast(Mage, OrdinaryMagicBattleTestData.Fire, new(4, 2, 0)));
        Assert.False(rejected.Accepted); Assert.Same(away, rejected.State); Assert.False(away.Turns.Units[0].ActionTaken);
    }

    [Theory] [InlineData(0,2,0)] [InlineData(4,2,0)] [InlineData(6,4,0)] [InlineData(-1,2,0)] [InlineData(1,2,1)]
    public void Invalid_endpoints_publish_no_path_choice_position_or_event(int x, int y, int layer)
    {
        var before = Active(); var result = OrdinaryMagicBattle.Apply(before, new OrdinaryMagicCommand.Move(Mage, new(x, y, layer)));
        Assert.False(result.Accepted); Assert.Same(before, result.State); Assert.Empty(result.Events);
    }

    [Fact]
    public void Allies_allow_transit_but_never_occupied_endpoints_and_enemy_blockers_require_detours()
    {
        var d = Definition(); var allyId = new BattleUnitId("ally"); var target = d.Units[1];
        var ally = target with { Occupant = target.Occupant with { UnitId = allyId, SourceOrder = 2, Position = new(1, 2, 0),
            InitialTeam = SpellTargetTeam.Blue, CurrentTeam = SpellTargetTeam.Blue }, Current = target.Current with { UnitId = allyId } };
        var state = Active(Copy(d, d.Units.Add(ally)));
        var query = OrdinaryMagicMovement.Query(state, Mage, new(3, 2, 0)); Assert.True(query.Accepted);
        Assert.Contains(new TilePosition(1, 2, 0), query.Outcome!.Path!.Tiles);
        Assert.DoesNotContain(new TilePosition(1, 2, 0), query.Outcome.PhysicalDestinations);
        Assert.False(OrdinaryMagicBattle.Apply(state, new OrdinaryMagicCommand.Move(Mage, new(1, 2, 0))).Accepted);
        var units = d.Units.SetItem(0, d.Units[0] with { Move = 4 })
            .SetItem(1, target with { Occupant = target.Occupant with { Position = new(1, 2, 0) } });
        query = OrdinaryMagicMovement.Query(Active(Copy(d, units)), Mage, new(2, 2, 0)); Assert.True(query.Accepted);
        Assert.Equal(new byte[] { 192, 0, 0, 128 }, query.Outcome!.Path!.PackedSteps.ToArray());
    }

    [Fact]
    public void Movement_draws_nothing_at_rng_limit_and_publication_counter_failures_are_atomic()
    {
        var state = Active(); state = state.With(turns: new(state.Turns.Units, new(57, ulong.MaxValue), state.Turns.Ticks, state.Turns.Phase));
        var command = new OrdinaryMagicCommand.Move(Mage, new(2, 1, 0));
        var result = OrdinaryMagicBattle.Apply(state, command); Assert.True(result.Accepted); Assert.Equal(ulong.MaxValue, result.State.Turns.RngState.DrawCount);
        var exhausted = state.With(revision: ulong.MaxValue); result = OrdinaryMagicBattle.Apply(exhausted, command);
        Assert.False(result.Accepted); Assert.Same(exhausted, result.State); Assert.Empty(result.Events);
        result = OrdinaryMagicBattle.Apply(state.With(revision: ulong.MaxValue - 1), command);
        Assert.True(result.Accepted); Assert.Equal(ulong.MaxValue, result.State.Revision);
    }

    [Fact]
    public void Explicit_ground_source_bounds_and_living_profile_are_required_without_fallback()
    {
        var old = OrdinaryMagicBattleTestData.Definition();
        Assert.False(OrdinaryMagicMovement.Query(old.Start(), Mage).Accepted);
        Assert.Throws<NotSupportedException>(() => new OrdinaryMagicBattleDefinition(old.Map, old.Units, old.Spells.Values,
            old.Jobs, old.JobThresholds, old.InitialRng, Ground));
        var wide = Copy(old, map: new(32, 8, 1, Enumerable.Repeat(new BattleTile(Ground, 10), 256).ToArray()));
        Assert.Equal(FlatMovementRejection.UnsupportedProfile, OrdinaryMagicMovement.Query(wide.Start(), Mage).Rejection);
        var lethal = Copy(OrdinaryMagicBattleTestData.Definition(targetHp: 20, charge: 0));
        var state = OrdinaryMagicBattleTestData.Apply(Active(lethal), new OrdinaryMagicCommand.Cast(Mage, OrdinaryMagicBattleTestData.Fire, new(4, 2, 0)));
        Assert.True(state.Finished); Assert.Equal(FlatMovementRejection.UnsupportedProfile, OrdinaryMagicMovement.Query(state, Mage).Rejection);
    }

    [Fact]
    public void Copied_byte_move_jump_caps_and_explicit_initial_facing_are_preserved()
    {
        var d = Definition(); d = Copy(d, d.Units.SetItem(0, d.Units[0] with { Move = 255, Jump = 255, InitialFacing = BattleFacing.North }));
        var state = d.Start(); Assert.Equal(BattleFacing.North, state.Units[0].Facing);
        var query = OrdinaryMagicMovement.Query(state, Mage); Assert.True(query.Accepted);
        Assert.Equal(124, query.Outcome!.EffectiveMove); Assert.Equal(3, query.Outcome.JumpHalf);
        Assert.Throws<NotSupportedException>(() => Copy(d, d.Units.SetItem(0, d.Units[0] with { InitialFacing = (BattleFacing)999 })));
    }
}
