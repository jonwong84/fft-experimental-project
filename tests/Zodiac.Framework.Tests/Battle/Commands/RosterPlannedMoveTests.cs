using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterPlannedMoveTests
{
    [Fact]
    public void Planned_Move_adopts_one_valid_checkpoint_and_publishes_actual_path_heading_and_bound_token()
    {
        var state=Ready(true);int calls=0;var rng=new RandomStateGenerator().Next(state.Battle.TurnBattle.Turns.RngState).State;
        var result=RosterDecisionBattle.Execute(state,A,c=>{calls++;return c.PrepareMoveThenAttack(new(A,new(1,1,0)),new(A,B),new(Sword,2),rng,"move");});
        Assert.True(result.Accepted);Assert.Equal(1,calls);Assert.NotNull(result.Path);Assert.NotNull(result.Continuation);Assert.Null(result.State.PendingFacing);
        Assert.Equal(new TilePosition(1,1,0),Position(result.State.Battle,A));Assert.Equal(BattleFacing.East,result.State.Battle.Combatants.Single(u=>u.UnitId==A).Facing);
        Assert.Equal(50,result.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);Assert.Equal(rng,result.State.Battle.TurnBattle.Turns.RngState);Assert.Equal(state.Revision+1,result.State.Revision);
        Assert.Equal("move",result.Diagnostic);
    }

    [Fact]
    public void Stone_setup_before_Move_does_not_require_original_range_or_ray_clearance()
    {
        var specs=new[]{new Spec("a",new(0,1,0),Ct:110),new Spec("b",new(5,1,0),1),new Spec("c",new(3,1,0))};
        var setup=Make(specs,active:false,count:6,ticks:0);var state=RosterFacingTurnBattle.Advance(RosterFacingTurnBattle.Begin(setup.State,setup.Profile)).State;
        var command=new UseTargetedAbilityCommand(A,RosterThrowStoneTests.Stone,B);var profile=RosterThrowStoneTests.Ability(state.Battle);var heights=RosterThrowStoneTests.Heights(state.Battle);
        Assert.Equal(AttackCommandRejection.OutOfRange,TargetedAbilityBattleSimulator.ValidateRosterCommand(state.Battle,command,profile,state.Profile,heights));
        var moved=RosterDecisionBattle.Execute(state,A,c=>c.PrepareMoveThenTargetedAbility(new(A,new(2,1,0)),command,profile,heights,c.RngState,1));
        Assert.True(moved.Accepted);Assert.NotNull(moved.Continuation);Assert.Equal(new TilePosition(2,1,0),Position(moved.State.Battle,A));
        Assert.Equal(50,moved.State.Battle.Combatants.Single(u=>u.UnitId==C).Hp);
    }

    [Fact]
    public void Invalid_grants_metadata_targets_or_followup_actor_reject_before_Move()
    {
        var state=Ready(true);var move=new MoveUnitCommand(A,new(1,1,0));var command=new UseTargetedAbilityCommand(A,RosterThrowStoneTests.Stone,B);
        var profile=RosterThrowStoneTests.Ability(state.Battle);var heights=RosterThrowStoneTests.Heights(state.Battle);
        foreach(var result in new[]{
            RosterDecisionBattle.Execute(state,A,c=>c.PrepareMoveThenTargetedAbility(move,command,RosterThrowStoneTests.Ability(c.Battle,false),heights,c.RngState,1)),
            RosterDecisionBattle.Execute(state,A,c=>c.PrepareMoveThenTargetedAbility(move,command,profile,new([new(A,30),new(B,30)]),c.RngState,1)),
            RosterDecisionBattle.Execute(state,A,c=>c.PrepareMoveThenAttack(move,new(A,new("absent")),new(Sword,2),c.RngState,1)),
            RosterDecisionBattle.Execute(state,A,c=>c.PrepareMoveThenAttack(move,new(B,A),new(Sword,2),c.RngState,1))})
        {RosterDecisionTests.Atomic(state,result);Assert.Null(result.Continuation);}
    }

    [Fact]
    public void Failed_actual_path_rolls_back_checkpoint_and_publishes_no_followup_authority()
    {
        var state=Ready(true);var rng=new RandomStateGenerator().Next(state.Battle.TurnBattle.Turns.RngState).State;
        var fail=RosterDecisionBattle.Execute(state,A,c=>c.PrepareMoveThenAttack(new(A,new(2,1,0)),new(A,B),new(Sword,2),rng,1));
        RosterDecisionTests.Atomic(state,fail);Assert.Null(fail.Continuation);
        var moved=RosterFacingTurnBattle.Move(state,new(A,new(1,1,0)));var second=RosterDecisionBattle.Execute(moved.State,A,c=>c.PrepareMoveThenWait(new(A,new(0,1,0)),null,c.RngState,1));
        RosterDecisionTests.Atomic(moved.State,second);Assert.Equal(FacingDecisionRejection.UnavailableChoices,second.Rejection);
    }

    [Fact]
    public void New_factories_share_closed_context_and_foreign_proposal_rejection()
    {
        var state=Ready(true);RosterDecisionContext? leaked=null;RosterDecision<int>? saved=null;
        var result=RosterDecisionBattle.Execute(state,A,c=>{leaked=c;saved=c.PrepareMoveThenWait(new(A,new(1,1,0)),null,c.RngState,1);return saved;});Assert.True(result.Accepted);
        Assert.Throws<InvalidOperationException>(()=>leaked!.PrepareMoveThenWait(new(A,new(1,1,0)),null,leaked.RngState,1));
        var stale=RosterDecisionBattle.Execute(state,A,_=>saved!);RosterDecisionTests.Atomic(state,stale);Assert.Null(stale.Continuation);
    }
}
