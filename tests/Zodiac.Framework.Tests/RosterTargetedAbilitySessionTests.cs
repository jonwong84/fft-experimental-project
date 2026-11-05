using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;
using static Zodiac.Framework.Tests.RosterThrowStoneTests;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterTargetedAbilitySessionTests
{
    [Fact]
    public void Both_actual_stone_orders_publish_only_real_receipts_and_support_explicit_facing()
    {
        foreach(bool moveFirst in new[]{true,false})
        {
            var state=Ready(moveFirst);var before=Snapshot(state.Battle);
            var first=moveFirst ? RosterFacingTurnBattle.Move(state,new(A,new(1,1,0))) : Act(state);Assert.True(first.Accepted);Assert.Null(first.State.PendingFacing);
            var second=moveFirst ? Act(first.State) : RosterFacingTurnBattle.Move(first.State,new(A,new(0,1,0)));Assert.True(second.Accepted);Assert.NotNull(second.State.PendingFacing);
            Assert.Equal(45,second.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);Assert.Equal(new TilePosition(3,1,0),Position(second.State.Battle,B));
            var settled=RosterFacingTurnBattle.FinishFacing(second.State,A,second.State.PendingFacing,BattleFacing.North);Assert.True(settled.Accepted);Assert.Null(settled.State.PendingFacing);
            Assert.Equal(new RandomState(3448561432,14),settled.State.Battle.TurnBattle.Turns.RngState);Assert.Equal(before,Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Foreign_receipts_pending_commands_and_late_revision_failure_preserve_actual_effect()
    {
        RosterFacingTurnState Pending(){var move=RosterFacingTurnBattle.Move(Ready(true),new(A,new(1,1,0)));return Act(move.State).State;}
        var state=Pending();var foreign=Pending();Atomic(state,RosterFacingTurnBattle.FinishFacing(state,A,foreign.PendingFacing));Atomic(state,Act(state));
        var ready=Ready(true);var near=new RosterFacingTurnState(ready.Battle,ready.Profile,ulong.MaxValue-2,null,ready.Lineage);
        var moved=RosterFacingTurnBattle.Move(near,new(A,new(1,1,0)));var act=Act(moved.State);Assert.True(act.Accepted);Assert.NotNull(act.State.PendingFacing);
        Atomic(act.State,RosterFacingTurnBattle.FinishFacing(act.State,A,act.State.PendingFacing));Assert.Equal(45,act.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);
    }

    [Fact]
    public void Failed_actual_stone_keeps_an_accepted_move_and_both_rng_copies()
    {
        var moved=RosterFacingTurnBattle.Move(Ready(true),new(A,new(1,1,0)));var state=moved.State;var old=state.Battle.TurnBattle;var rng=new RandomState(57,ulong.MaxValue-6);
        var battle=new AttackBattleSnapshot(new(new(old.Battle.Map,old.Battle.Units,rng),new(old.Turns.Units,rng,old.Turns.Ticks,old.Turns.Phase)),state.Battle.Combatants);
        var exhausted=new RosterFacingTurnState(battle,state.Profile,state.Revision,null,state.Lineage);var before=Snapshot(exhausted.Battle);var fail=Act(exhausted);
        Atomic(exhausted,fail);Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted,fail.AttackRejection);Assert.Equal(new TilePosition(1,1,0),Position(fail.State.Battle,A));Assert.Equal(before,Snapshot(exhausted.Battle));
    }

    [Fact]
    public void Session_replay_matches_direct_roster_kernel_and_cannot_authorize_pair_profiles()
    {
        var a=Ready();var b=Ready(reverse:true);var direct=TargetedAbilityBattleSimulator.ApplyRoster(a.Battle,new(A,Stone,B),Ability(a.Battle),a.Profile,Heights(a.Battle));
        var x=Act(a);var y=Act(b);Assert.True(x.Accepted);Assert.Equal(Snapshot(direct.State),Snapshot(x.State.Battle));Assert.Equal(Snapshot(x.State.Battle),Snapshot(y.State.Battle));
        Assert.Equal(SerializeEvents(direct.Events),SerializeEvents(x.Events));Assert.Equal(SerializeEvents(x.Events),SerializeEvents(y.Events));
        Atomic(a,RosterFacingTurnBattle.UseTargetedAbility(a,new(A,Stone,B),new(Ground,[new(Stone)],[new(A,[Stone],0),new(B,[],0)]),Heights(a.Battle)));
    }

    private static RosterFacingResult Act(RosterFacingTurnState state)=>RosterFacingTurnBattle.UseTargetedAbility(state,new(A,Stone,B),Ability(state.Battle),Heights(state.Battle));
    private static void Atomic(RosterFacingTurnState state,RosterFacingResult r){Assert.False(r.Accepted);Assert.Same(state,r.State);Assert.Empty(r.Events);}
}
