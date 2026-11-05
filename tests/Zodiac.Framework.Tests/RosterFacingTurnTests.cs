using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterFacingTurnTests
{
    [Fact]
    public void Move_ACT_and_ACT_Move_bind_actual_complete_results_before_explicit_facing()
    {
        foreach (bool moveFirst in new[]{false,true})
        {
            var ready=Ready(moveFirst); var before=Snapshot(ready.Battle);
            var first=moveFirst ? RosterFacingTurnBattle.Move(ready,new(A,new(1,1,0)))
                : RosterFacingTurnBattle.Attack(ready,new(A,B),new(Sword,5));
            Assert.True(first.Accepted); Assert.Null(first.State.PendingFacing);
            var second=moveFirst ? RosterFacingTurnBattle.Attack(first.State,new(A,B),new(Sword,5))
                : RosterFacingTurnBattle.Move(first.State,new(A,new(0,1,0)));
            Assert.True(second.Accepted); Assert.NotNull(second.State.PendingFacing);
            Assert.Equal(5,second.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);
            Assert.Equal(new TilePosition(3,1,0),Position(second.State.Battle,B));
            Assert.Equal(new RandomState(3448561432,14),second.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(15,second.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
            Assert.Equal(ready.Battle.TurnBattle.Turns.Ticks,second.State.Battle.TurnBattle.Turns.Ticks);
            var final=RosterFacingTurnBattle.FinishFacing(second.State,A,second.State.PendingFacing,moveFirst ? BattleFacing.South : null);
            Assert.True(final.Accepted); Assert.Null(final.State.PendingFacing); Assert.Null(final.State.Battle.TurnBattle.Turns.ActiveUnitId);
            Assert.Equal(moveFirst ? BattleFacing.South : BattleFacing.West,final.State.Battle.Combatants.Single(u=>u.UnitId==A).Facing);
            Assert.IsType<FacingSettledEvent>(Assert.Single(final.Events)); Assert.Equal(before,Snapshot(ready.Battle));
        }
    }

    [Fact]
    public void Foreign_wrong_actor_invalid_direction_and_consumed_receipts_cannot_settle_a_session()
    {
        RosterFacingTurnState Pending()
        {
            var moved=RosterFacingTurnBattle.Move(Ready(true),new(A,new(1,1,0)));
            return RosterFacingTurnBattle.Attack(moved.State,new(A,B),new(Sword,5)).State;
        }
        var state=Pending(); var foreign=Pending();
        var wrong=RosterFacingTurnBattle.FinishFacing(state,A,foreign.PendingFacing);
        Atomic(state,wrong); Assert.Equal(FacingTurnRejection.StaleCompletion,wrong.FacingRejection);
        wrong=RosterFacingTurnBattle.FinishFacing(state,B,state.PendingFacing); Atomic(state,wrong);
        Assert.Equal(FacingTurnRejection.WrongActor,wrong.FacingRejection);
        Atomic(state,RosterFacingTurnBattle.FinishFacing(state,A,state.PendingFacing,(BattleFacing)99));
        Atomic(state,RosterFacingTurnBattle.Advance(state));
        Atomic(state,RosterFacingTurnBattle.Attack(state,new(A,B),new(Sword,5)));
        Atomic(state,RosterFacingTurnBattle.Move(state,new(A,new(0,1,0))));
        var complete=RosterFacingTurnBattle.FinishFacing(state,A,state.PendingFacing);
        Assert.True(complete.Accepted); Atomic(complete.State,RosterFacingTurnBattle.FinishFacing(complete.State,A,state.PendingFacing));
    }

    [Fact]
    public void Lethal_ACT_preserves_real_KO_rejects_unported_corpse_movement_and_can_END_with_unused_Move()
    {
        var ready=Ready(hp:45); var act=RosterFacingTurnBattle.Attack(ready,new(A,B),new(Sword,5));
        Assert.True(act.Accepted); Assert.Equal(0,act.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);
        Assert.Equal(new TilePosition(2,1,0),Position(act.State.Battle,B)); Assert.Null(act.State.PendingFacing);
        var move=RosterFacingTurnBattle.Move(act.State,new(A,new(0,1,0))); Atomic(act.State,move);
        Assert.Equal(FlatMovementRejection.UnsupportedProfile,move.PathRejection);
        var end=RosterFacingTurnBattle.WaitWithFacing(act.State,A);
        Assert.True(end.Accepted); Assert.Equal(35,end.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
        Assert.Equal(new RandomState(3448561432,14),end.State.Battle.TurnBattle.Turns.RngState);
        Assert.IsType<FacingSettledEvent>(end.Events[^1]); Assert.Null(end.State.PendingFacing);
        Atomic(end.State,RosterFacingTurnBattle.Advance(end.State));
    }

    [Fact]
    public void Late_revision_failure_preserves_accepted_attack_push_and_pending_receipt()
    {
        var ready=Ready(true); var near=new RosterFacingTurnState(ready.Battle,ready.Profile,ulong.MaxValue-2,null,ready.Lineage);
        var moved=RosterFacingTurnBattle.Move(near,new(A,new(1,1,0))); Assert.True(moved.Accepted);
        var act=RosterFacingTurnBattle.Attack(moved.State,new(A,B),new(Sword,5)); Assert.True(act.Accepted);
        Assert.Equal(ulong.MaxValue,act.State.Revision); Assert.NotNull(act.State.PendingFacing);
        var fail=RosterFacingTurnBattle.FinishFacing(act.State,A,act.State.PendingFacing); Atomic(act.State,fail);
        Assert.Equal(FacingTurnRejection.RevisionExhausted,fail.FacingRejection);
        Assert.Equal(5,fail.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp); Assert.Equal(new TilePosition(3,1,0),Position(fail.State.Battle,B));
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Roster_sessions_replay_across_collection_order_and_late_ACT_failure_keeps_the_Move()
    {
        var ready=Ready(true); var reverse=Ready(true,true);
        var first=RosterFacingTurnBattle.Move(ready,new(A,new(1,1,0))); var replay=RosterFacingTurnBattle.Move(reverse,new(A,new(1,1,0)));
        Assert.True(first.Accepted); Assert.True(replay.Accepted); Assert.Equal(Snapshot(first.State.Battle),Snapshot(replay.State.Battle));
        Assert.Equal(SerializeEvents(first.Events),SerializeEvents(replay.Events));
        var t=first.State.Battle.TurnBattle;
        var exhausted=new RandomState(57,ulong.MaxValue-6);
        var battle=first.State.Battle.WithTurnBattle(new(new(t.Battle.Map,t.Battle.Units,exhausted),
            new(t.Turns.Units,exhausted,t.Turns.Ticks,t.Turns.Phase)));
        var cursor=new RosterFacingTurnState(battle,first.State.Profile,first.State.Revision,null,first.State.Lineage);
        var failed=RosterFacingTurnBattle.Attack(cursor,new(A,B),new(Sword,5)); Atomic(cursor,failed);
        Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted,failed.AttackRejection);
        Assert.Equal(new TilePosition(1,1,0),Position(failed.State.Battle,A));
        Assert.True(failed.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).MovementTaken);
    }

    [Fact]
    public void Fresh_session_admission_and_early_END_validate_choices_and_explicit_facing()
    {
        var s=Make(); Assert.Throws<NotSupportedException>(()=>RosterFacingTurnBattle.Begin(s.State,s.Profile));
        var ready=Ready(); Atomic(ready,RosterFacingTurnBattle.WaitWithFacing(ready,B));
        Atomic(ready,RosterFacingTurnBattle.WaitWithFacing(ready,A,(BattleFacing)99));
        var end=RosterFacingTurnBattle.WaitWithFacing(ready,A,BattleFacing.North);
        Assert.True(end.Accepted); Assert.Equal(55,end.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
        Assert.Equal(BattleFacing.North,end.State.Battle.Combatants.Single(u=>u.UnitId==A).Facing);
        Assert.Equal(ready.Battle.TurnBattle.Turns.RngState,end.State.Battle.TurnBattle.Turns.RngState);
    }

    private static void Atomic(RosterFacingTurnState state,RosterFacingResult result)
    { Assert.False(result.Accepted); Assert.Same(state,result.State); Assert.Empty(result.Events); Assert.Null(result.Path); }
}
