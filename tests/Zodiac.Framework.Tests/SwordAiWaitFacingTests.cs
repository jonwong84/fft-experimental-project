using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiWaitFacingTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly FftRandomState Rng = new(19, ulong.MaxValue), Live = new(57, 100);
    private static readonly FlatOrdinaryMovementProfile Movement = new(Ground, A, B);
    private static readonly SwordAiWaitFacingProfile Profile = new(true, true);

    [Theory]
    [InlineData(7, 5, BattleFacing.South)] [InlineData(5, 7, BattleFacing.West)]
    [InlineData(7, 9, BattleFacing.North)] [InlineData(9, 7, BattleFacing.East)]
    [InlineData(6, 6, BattleFacing.South)] [InlineData(6, 8, BattleFacing.West)]
    [InlineData(8, 8, BattleFacing.North)] [InlineData(8, 6, BattleFacing.East)]
    [InlineData(9, 8, BattleFacing.East)] [InlineData(8, 9, BattleFacing.North)]
    public void Source_cardinals_diagonal_ties_and_unequal_axes_map_to_semantic_facings(int x, int y, BattleFacing facing)
    {
        var state = State(16, 16, new(7, 7, 0), new(x, y, 0)); var result = Evaluate(state); var o = Success(result);
        Assert.Equal(facing, o.Facing); Assert.Equal(new TilePosition(7, 7, 0), o.ActorPosition);
        Assert.Equal(new TilePosition(x, y, 0), o.TargetPosition); Assert.Equal(Rng, result.RngState);
        Assert.Equal(BattleFacing.West, state.Combatants.Single(u => u.UnitId == A).Facing);
    }

    [Theory]
    [InlineData(2, 1, 0, 0, 1, 0, BattleFacing.East)] [InlineData(2, 1, 1, 0, 0, 0, BattleFacing.West)]
    [InlineData(1, 2, 0, 0, 0, 1, BattleFacing.North)] [InlineData(1, 2, 0, 1, 0, 0, BattleFacing.South)]
    public void Narrow_map_edges_face_the_occupied_adjacent_enemy(int width, int depth, int ax, int ay, int tx, int ty, BattleFacing facing)
    {
        var state = State(width, depth, new(ax, ay, 0), new(tx, ty, 0));
        Assert.Equal(facing, Success(Evaluate(state)).Facing);
        Assert.DoesNotContain(new TilePosition(tx, ty, 0), FlatOrdinaryMovement.Evaluate(state, A, Movement).Outcome!.PhysicalDestinations);
    }

    [Fact]
    public void Uses_positions_after_an_actual_move_instead_of_a_prior_hypothetical_destination()
    {
        var initial = State(4, 3, new(1, 1, 0), new(2, 0, 0));
        Assert.Equal(BattleFacing.East, Success(Evaluate(initial)).Facing);
        var moved = AttackBattleSimulator.Move(initial, new(A, new(2, 1, 0)), BattleMovementPolicy.OrdinaryAdjacentStepPrototype);
        Assert.True(moved.Accepted);
        var result = Evaluate(moved.State); var o = Success(result);
        Assert.Equal(new TilePosition(2, 1, 0), o.ActorPosition); Assert.Equal(BattleFacing.South, o.Facing);
        Assert.True(moved.State.TurnBattle.Turns.Units.Single(u => u.Id == A).MovementTaken);
        Assert.Equal(BattleFacing.West, moved.State.Combatants.Single(u => u.UnitId == A).Facing);
    }

    [Theory] [InlineData(0)] [InlineData(100)]
    public void Already_used_ACT_and_different_target_delays_need_no_counter_or_tick_reservation(int targetCt)
    {
        var state = State(acted: true, targetCt: targetCt, ticks: ulong.MaxValue); var before = Snapshot(state);
        var result = Evaluate(state); Assert.Equal(BattleFacing.East, Success(result).Facing);
        Assert.Equal(Rng, result.RngState); Assert.Equal(before, Snapshot(state));
        Assert.True(state.TurnBattle.Turns.Units.Single(u => u.Id == A).ActionTaken);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Both_unrepresented_Wait_context_facts_are_required()
    {
        foreach (var profile in new[] { Profile with { OriginalAiChooseAtWaitContext = false }, Profile with { EligibleOpponent = false } })
            Rejected(State(), A, B, profile, SwordAiWaitFacingRejection.UnsupportedContext);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Wrong_turn_unknown_or_ineligible_target_and_unsupported_geometry_reject_without_partial_facing()
    {
        Rejected(State(), new("missing:actor"), B, Profile, SwordAiWaitFacingRejection.UnknownActor);
        Rejected(State(), A, new("missing:target"), Profile, SwordAiWaitFacingRejection.UnknownTarget);
        Rejected(State(), A, A, Profile, SwordAiWaitFacingRejection.InvalidTarget);
        Rejected(State(), B, A, Profile, SwordAiWaitFacingRejection.UnavailableTurn);
        Rejected(State(active: false), A, B, Profile, SwordAiWaitFacingRejection.UnavailableTurn);
        Rejected(State(targetHp: 0), A, B, Profile, SwordAiWaitFacingRejection.UnsupportedProfile);
        Rejected(State(height: 11), A, B, Profile, SwordAiWaitFacingRejection.UnsupportedProfile);
        Rejected(State(origin: new(1, 1, 0), target: new(1, 1, 0)), A, B, Profile, SwordAiWaitFacingRejection.OverlappingPositions);
    }

    [Theory] [InlineData(false)] [InlineData(true)] [Trait("Contract", "QueryIsolation")]
    public void Replay_collection_order_and_actor_identity_preserve_the_settled_snapshot(bool second)
    {
        var state = State(second: second); var before = Snapshot(state); var actor = second ? B : A; var target = second ? A : B;
        var result = Evaluate(state, actor, target); Success(result);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Evaluate(state, actor, target)));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Evaluate(State(second: second, reverse: true), actor, target)));
        Assert.Equal(before, Snapshot(state)); Assert.Equal(Live, state.TurnBattle.Turns.RngState);
    }

    private static SwordAiWaitFacingResult Evaluate(AttackBattleSnapshot state, BattleUnitId? actor = null, BattleUnitId? target = null) =>
        SwordAiWaitFacing.Evaluate(state, actor ?? A, target ?? B, Movement, Profile, Rng);
    private static SwordAiWaitFacingOutcome Success(SwordAiWaitFacingResult result)
    { Assert.True(result.Accepted, result.Rejection.ToString()); Assert.Equal(SwordAiWaitFacingRejection.None, result.Rejection); return Assert.IsType<SwordAiWaitFacingOutcome>(result.Outcome); }
    private static void Rejected(AttackBattleSnapshot state, BattleUnitId actor, BattleUnitId target,
        SwordAiWaitFacingProfile profile, SwordAiWaitFacingRejection reason)
    {
        var before = Snapshot(state); var result = SwordAiWaitFacing.Evaluate(state, actor, target, Movement, profile, Rng);
        Assert.False(result.Accepted); Assert.Null(result.Outcome); Assert.Equal(reason, result.Rejection);
        Assert.Equal(Rng, result.RngState); Assert.Equal(before, Snapshot(state));
    }
    private static string Snapshot(AttackBattleSnapshot state) => JsonSerializer.Serialize(new { state.Combatants, state.TurnBattle.Turns,
        Units = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved }) });
    private static AttackBattleSnapshot State(int width = 4, int depth = 3, TilePosition? origin = null, TilePosition? target = null,
        bool acted = false, int targetCt = 95, ulong ticks = 20, bool active = true, int targetHp = 50, int height = 10,
        bool second = false, bool reverse = false)
    {
        var actor = second ? B : A; var enemy = second ? A : B;
        var movement = new UnitMovementProfile(4, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, origin ?? new(1, 1, 0), movement), new(enemy, target ?? new(2, 1, 0), movement) };
        var turns = new TurnUnitState[] { new(actor, 5, 15, 2, active, false, acted), new(enemy, 5, targetCt, 17) };
        AttackUnitState Combat(BattleUnitId id, int hp) => new(id, hp, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0,
            BattleFacing.West, hp == 0 ? AttackUnitStatus.Dead : AttackUnitStatus.None, 0, null);
        var combat = new[] { Combat(actor, 50), Combat(enemy, targetHp) };
        return new(new(new(new(width, depth, 1, Enumerable.Repeat(new BattleTile(Ground, height), width * depth).ToArray()),
            reverse ? bodies.Reverse() : bodies, Live), new(reverse ? turns.Reverse() : turns, Live, ticks,
                active ? TurnCyclePhase.UnitTurn : TurnCyclePhase.PickUnit)), reverse ? combat.Reverse() : combat);
    }
}
