using System.Text.Json;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class SwordAiWaitSettlementTests
{
    private static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");
    private static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    private static readonly FlatOrdinaryMovementProfile Movement = new(Ground, A, B);
    private static readonly SwordAiWaitFacingProfile WaitProfile = new(true, true);
    private static readonly SwordAiWaitSettlementProfile EndProfile = new(true);

    [Theory] [InlineData("none", 55, BattleFacing.East)]
    [InlineData("move", 35, BattleFacing.North)] [InlineData("act", 35, BattleFacing.East)]
    public void Active_Wait_uses_settled_positions_and_refunds_each_unused_choice_once(string used, int ct, BattleFacing facing)
    {
        var state = Active();
        if (used == "move") state = Move(state, new(2, 0, 0));
        if (used == "act") state = Attack(state);
        var before = Snapshot(state);
        var result = Wait(state); var after = Success(result);
        Assert.Equal(ct, Turn(after, A).Ct);
        Assert.Equal(used == "move", Turn(after, A).MovementTaken);
        Assert.Equal(used == "act", Turn(after, A).ActionTaken);
        Assert.Equal(facing, Unit(after, A).Facing);
        Assert.Equal(Position(state, A), result.Facing!.ActorPosition);
        Assert.Single(Ended(result)); Assert.IsType<FacingSettledEvent>(result.Events[^1]);
        Assert.Same(Unit(state, B), Unit(after, B)); Assert.Equal(Turn(state, B), Turn(after, B));
        Assert.Equal(state.Revision + 1, after.Revision); Assert.Null(after.PendingFacing);
        AssertClockUnchanged(state, after); Assert.Equal(before, Snapshot(state));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Both_choice_orders_finish_real_receipts_without_refund_damage_or_clock_replay(bool moveFirst)
    {
        var state = Pending(moveFirst); var receipt = state.PendingFacing; var before = Snapshot(state);
        Assert.Equal(25, Unit(state, B).Hp); Assert.Equal(15, Turn(state, A).Ct);
        // A public raw-snapshot query cannot turn an ended battle into command authority.
        Assert.Equal(SwordAiWaitFacingRejection.UnavailableTurn,
            SwordAiWaitFacing.Evaluate(state.Battle, A, B, Movement, WaitProfile, state.Battle.TurnBattle.Turns.RngState).Rejection);
        var result = Finish(state); var after = Success(result);
        Assert.Equal(BattleFacing.North, Unit(after, A).Facing);
        Assert.Single(result.Events); Assert.Empty(Ended(result));
        Assert.Same(state.Battle.TurnBattle, after.Battle.TurnBattle);
        Assert.Same(Unit(state, B), Unit(after, B));
        Assert.Equal(Unit(state, A).WithResult(50, BattleFacing.North, Unit(state, A).KoCount), Unit(after, A));
        Assert.Equal(state.Revision + 1, after.Revision); Assert.Null(after.PendingFacing);
        Assert.Same(receipt, state.PendingFacing); Assert.Equal(before, Snapshot(state));
        var replay = Finish(state); Success(replay);
        Assert.Equal(Snapshot(after), Snapshot(replay.State)); Assert.Equal(result.Events.ToArray(), replay.Events.ToArray());
        Atomic(after, Finish(after, receipt), SwordAiWaitSettlementRejection.AuthorityRejected, FacingTurnRejection.MissingCompletion);
    }

    [Fact]
    public void Actual_critical_knockback_is_settled_before_receipt_facing_resolution()
    {
        var state = Active(seed: 601690112, wide: true);
        state = Attack(state);
        Assert.Equal(new TilePosition(4, 2, 0), Position(state, B));
        state = Move(state, new(2, 4, 0));
        Assert.NotNull(state.PendingFacing);
        var result = Finish(state); var after = Success(result);
        // The stale pre-attack enemy at (3,2) would choose South from (2,4).
        Assert.Equal(BattleFacing.East, result.Facing!.Facing);
        Assert.Equal(new TilePosition(4, 2, 0), result.Facing.TargetPosition);
        Assert.Equal(5, Unit(after, B).Hp); Assert.Same(Unit(state, B), Unit(after, B));
        Assert.Same(state.Battle.TurnBattle, after.Battle.TurnBattle);
    }

    [Fact]
    public void High_CT_caps_once_and_same_facing_still_completes_the_session()
    {
        var active = Active(speed: 80, facing: BattleFacing.East);
        Assert.Equal(75, Turn(active, A).Ct);
        var wait = Wait(active); var waited = Success(wait);
        Assert.Equal(60, Turn(waited, A).Ct);
        Assert.Equal(new FacingSettledEvent(A, BattleFacing.East, BattleFacing.East), wait.Events[^1]);
        var pending = Pending(true, speed: 80); Assert.Equal(60, Turn(pending, A).Ct);
        var ended = Success(Finish(pending)); Assert.Equal(60, Turn(ended, A).Ct);
        Assert.Same(pending.Battle.TurnBattle, ended.Battle.TurnBattle);
    }

    [Fact]
    public void Missing_foreign_stale_wrong_actor_and_pending_Wait_reject_atomically()
    {
        var active = Active(); var pending = Pending(true); var foreign = Pending(true); var receipt = pending.PendingFacing;
        Atomic(active, Finish(active), SwordAiWaitSettlementRejection.AuthorityRejected, FacingTurnRejection.MissingCompletion);
        Atomic(pending, Finish(pending, foreign.PendingFacing), SwordAiWaitSettlementRejection.AuthorityRejected, FacingTurnRejection.StaleCompletion);
        Atomic(pending, SwordAiWaitSettlement.Finish(pending, A, B, null, Movement, WaitProfile, EndProfile),
            SwordAiWaitSettlementRejection.AuthorityRejected, FacingTurnRejection.StaleCompletion);
        Atomic(pending, SwordAiWaitSettlement.Finish(pending, B, A, receipt, Movement, WaitProfile, EndProfile),
            SwordAiWaitSettlementRejection.AuthorityRejected, FacingTurnRejection.WrongActor);
        Atomic(pending, Wait(pending), SwordAiWaitSettlementRejection.AuthorityRejected, FacingTurnRejection.FacingPending);
        Atomic(active, SwordAiWaitSettlement.Wait(active, B, A, Movement, WaitProfile, EndProfile),
            SwordAiWaitSettlementRejection.AuthorityRejected, FacingTurnRejection.InactiveActor);
        var newer = Success(Finish(pending)); newer = NextPending(newer);
        Atomic(newer, Finish(newer, receipt), SwordAiWaitSettlementRejection.AuthorityRejected, FacingTurnRejection.StaleCompletion);
        Assert.Same(receipt, pending.PendingFacing); Success(Finish(pending));
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Default_source_contexts_and_bad_targets_preserve_active_and_pending_authority()
    {
        foreach (var state in new[] { Active(), Pending(true) })
        {
            SwordAiWaitSettlementResult Apply(SwordAiWaitFacingProfile wait, SwordAiWaitSettlementProfile end, BattleUnitId target) =>
                state.PendingFacing is null ? SwordAiWaitSettlement.Wait(state, A, target, Movement, wait, end)
                    : SwordAiWaitSettlement.Finish(state, A, target, state.PendingFacing, Movement, wait, end);
            Atomic(state, Apply(WaitProfile, new(), B), SwordAiWaitSettlementRejection.UnsupportedContext);
            foreach (var wait in new[] { new SwordAiWaitFacingProfile(), WaitProfile with { OriginalAiChooseAtWaitContext = false }, WaitProfile with { EligibleOpponent = false } })
                Atomic(state, Apply(wait, EndProfile, B), SwordAiWaitSettlementRejection.FacingRejected,
                    facing: SwordAiWaitFacingRejection.UnsupportedContext);
            Atomic(state, Apply(WaitProfile, EndProfile, A), SwordAiWaitSettlementRejection.FacingRejected,
                facing: SwordAiWaitFacingRejection.InvalidTarget);
            Atomic(state, Apply(WaitProfile, EndProfile, new("sample:unit/missing")), SwordAiWaitSettlementRejection.FacingRejected,
                facing: SwordAiWaitFacingRejection.UnknownTarget);
        }
    }

    [Fact]
    public void Dead_opponent_rejects_without_guessing_facing_or_consuming_completion()
    {
        var pending = Attack(Move(Active(), new(2, 0, 0)), power: 40);
        Assert.Equal(0, Unit(pending, B).Hp); var receipt = pending.PendingFacing;
        Atomic(pending, Finish(pending), SwordAiWaitSettlementRejection.FacingRejected,
            facing: SwordAiWaitFacingRejection.UnsupportedProfile);
        Assert.Same(receipt, pending.PendingFacing);
        Assert.True(FacingTurnBattle.FinishFacing(pending, A, receipt, new FacingChoice.KeepCurrent()).Accepted);
    }

    [Fact]
    public void Zero_draw_Wait_and_completion_succeed_at_exhausted_RNG_and_keep_both_copies()
    {
        var state = Active(draws: ulong.MaxValue - 1);
        Assert.Equal(ulong.MaxValue, state.Battle.TurnBattle.Turns.RngState.DrawCount);
        var after = Success(Wait(state)); AssertClockUnchanged(state, after);
        Assert.Equal(55, Turn(after, A).Ct);
        var pending = Attack(Move(Active(draws: ulong.MaxValue - 7), new(2, 0, 0)));
        Assert.Equal(ulong.MaxValue, pending.Battle.TurnBattle.Turns.RngState.DrawCount);
        var finished = Success(Finish(pending)); Assert.Same(pending.Battle.TurnBattle, finished.Battle.TurnBattle);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Identity_collection_order_and_immutable_replay_preserve_results(bool second)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var state = Active(second: second); var reversed = Active(second: second, reverse: true);
        var before = Snapshot(state);
        var result = SwordAiWaitSettlement.Wait(state, actor, target, Movement, WaitProfile, EndProfile); Success(result);
        var replay = SwordAiWaitSettlement.Wait(state, actor, target, Movement, WaitProfile, EndProfile); Success(replay);
        var reordered = SwordAiWaitSettlement.Wait(reversed, actor, target, Movement, WaitProfile, EndProfile); Success(reordered);
        Assert.Equal(Snapshot(result.State), Snapshot(replay.State)); Assert.Equal(Snapshot(result.State), Snapshot(reordered.State));
        Assert.Equal(result.Facing, reordered.Facing); Assert.Equal(result.Events.ToArray(), reordered.Events.ToArray());
        Assert.Equal(before, Snapshot(state));
    }

    private static SwordAiWaitSettlementResult Wait(FacingTurnState state) => SwordAiWaitSettlement.Wait(state, A, B, Movement, WaitProfile, EndProfile);
    private static SwordAiWaitSettlementResult Finish(FacingTurnState state, FacingCompletionReceipt? receipt = null) =>
        SwordAiWaitSettlement.Finish(state, A, B, receipt ?? state.PendingFacing, Movement, WaitProfile, EndProfile);
    private static FacingTurnState Success(SwordAiWaitSettlementResult result)
    {
        Assert.True(result.Accepted, $"{result.Rejection}/{result.FacingRejection}/{result.Transition.FacingRejection}");
        Assert.True(result.Transition.Accepted); Assert.NotNull(result.Facing);
        Assert.Equal(SwordAiWaitSettlementRejection.None, result.Rejection);
        Assert.Equal(SwordAiWaitFacingRejection.None, result.FacingRejection); return result.State;
    }
    private static FacingTurnState Accepted(FacingTurnResult result)
    { Assert.True(result.Accepted, $"{result.FacingRejection}/{result.AttackRejection}/{result.TurnRejection}/{result.MovementRejection}"); return result.State; }
    private static void Atomic(FacingTurnState state, SwordAiWaitSettlementResult result, SwordAiWaitSettlementRejection reason,
        FacingTurnRejection authority = FacingTurnRejection.None, SwordAiWaitFacingRejection facing = SwordAiWaitFacingRejection.None)
    {
        Assert.False(result.Accepted); Assert.False(result.Transition.Accepted); Assert.Equal(reason, result.Rejection);
        Assert.Equal(authority, result.Transition.FacingRejection); Assert.Equal(facing, result.FacingRejection);
        Assert.Same(state, result.State); Assert.Empty(result.Events); Assert.Null(result.Facing);
    }
    private static void AssertClockUnchanged(FacingTurnState before, FacingTurnState after)
    {
        Assert.Equal(before.Battle.TurnBattle.Turns.RngState, after.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(before.Battle.TurnBattle.Battle.RngState, after.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(before.Battle.TurnBattle.Turns.Ticks, after.Battle.TurnBattle.Turns.Ticks);
    }
    private static AttackUnitState Unit(FacingTurnState state, BattleUnitId id) => state.Battle.Combatants.Single(u => u.UnitId == id);
    private static TurnUnitState Turn(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Turns.Units.Single(u => u.Id == id);
    private static TilePosition Position(FacingTurnState state, BattleUnitId id) => state.Battle.TurnBattle.Battle.Units.Single(u => u.Id == id).Position;
    private static IEnumerable<TurnEndedEvent> Ended(SwordAiWaitSettlementResult result) => result.Events.OfType<AttackTurnBattleEvent>()
        .Select(e => e.Event).OfType<TurnBattleClockEvent>().Select(e => e.Event).OfType<TurnEndedEvent>();
    private static string Snapshot(FacingTurnState state) => JsonSerializer.Serialize(new { state.Revision,
        pending = state.PendingFacing is { } p ? new { p.ActorId, p.Revision } : null,
        combat = state.Battle.Combatants.OrderBy(u => u.UnitId.Value),
        bodies = state.Battle.TurnBattle.Battle.Units.OrderBy(u => u.Id.Value).Select(u => new { u.Id, u.Position, u.HasMoved }),
        turns = state.Battle.TurnBattle.Turns.Units.OrderBy(u => u.Id.Value), state.Battle.TurnBattle.Turns.Phase,
        state.Battle.TurnBattle.Turns.Ticks, state.Battle.TurnBattle.Turns.RngState, bodyRng = state.Battle.TurnBattle.Battle.RngState });
    private static FacingTurnState Move(FacingTurnState state, TilePosition destination) => Accepted(FacingTurnBattle.Move(state, new(A, destination)));
    private static FacingTurnState Attack(FacingTurnState state, int power = 5) => Accepted(FacingTurnBattle.Attack(state, new(A, B), new(Weapon, power)));
    private static FacingTurnState Pending(bool moveFirst, int speed = 20)
    {
        var state = Active(speed: speed);
        return moveFirst ? Attack(Move(state, new(2, 0, 0))) : Move(Attack(state), new(1, 0, 0));
    }
    private static FacingTurnState NextPending(FacingTurnState state)
    {
        // The faster actor becomes ready again; no state/receipt fabrication.
        state = Accepted(FacingTurnBattle.Advance(state)); Assert.Equal(A, state.Battle.TurnBattle.Turns.ActiveUnitId);
        return Attack(Move(state, new(1, 1, 0)), power: 0);
    }
    private static FacingTurnState Active(int speed = 20, uint seed = 1, ulong draws = 7,
        BattleFacing facing = BattleFacing.West, bool second = false, bool reverse = false, bool wide = false)
    {
        var actor = second ? B : A; var target = second ? A : B; var rng = new FftRandomState(seed, draws);
        int width = wide ? 6 : 4, depth = wide ? 5 : 3;
        var map = new BattleMapDefinition(width, depth, 1, Enumerable.Repeat(new BattleTile(Ground, 10), width * depth).ToArray());
        var movement = new UnitMovementProfile(3, 2, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        BattleUnitState[] bodies = [new(actor, wide ? new(2, 2, 0) : new(1, 1, 0), movement), new(target, wide ? new(3, 2, 0) : new(2, 1, 0), movement)];
        TurnUnitState[] turns = [new(actor, speed, 95, 2), new(target, 1, 0, 17)];
        AttackUnitState[] combat = [new(actor, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0, facing, AttackUnitStatus.None, 255, Weapon),
            new(target, 50, 50, 0, 0, 5, 0, 0, 0, 0, 70, 0, BattleFacing.West, AttackUnitStatus.None, 255, Weapon)];
        var battle = new AttackBattleSnapshot(new(new(map, reverse ? bodies.Reverse() : bodies, rng),
            new(reverse ? turns.Reverse() : turns, rng)), reverse ? combat.Reverse() : combat);
        return Accepted(FacingTurnBattle.Advance(FacingTurnBattle.Begin(battle, new(Ground))));
    }
}
