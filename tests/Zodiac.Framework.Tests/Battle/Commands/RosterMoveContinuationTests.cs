using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterMoveContinuationTests
{
    private static readonly AssetId Self = new("test:roster-self");
    private static RosterDecisionResult<string> Plan(RosterFacingTurnState state,string kind,BoundedSwordWeapon? weapon=null) => RosterDecisionBattle.Execute(state,A,c=>kind switch {
        "self"=>c.PrepareMoveThenImmediateAbility(new(A,new(1,1,0)),new(A,Self),RosterDecisionTests.SelfProfile(c.Battle),c.RngState,kind),
        "stone"=>c.PrepareMoveThenTargetedAbility(new(A,new(1,1,0)),new(A,RosterThrowStoneTests.Stone,B),RosterThrowStoneTests.Ability(c.Battle),RosterThrowStoneTests.Heights(c.Battle),c.RngState,kind),
        "wait"=>c.PrepareMoveThenWait(new(A,new(1,1,0)),BattleFacing.South,c.RngState,kind),
        _=>c.PrepareMoveThenAttack(new(A,new(1,1,0)),new(A,B),weapon ?? new(Sword,2),c.RngState,kind)});

    [Fact]
    public void Frozen_attack_self_stone_and_wait_use_existing_actual_rules_without_new_planning()
    {
        foreach(string kind in new[]{"attack","self","stone","wait"})
        {
            var planned=Plan(Ready(true),kind);Assert.True(planned.Accepted);var state=planned.State;var before=Snapshot(state.Battle);
            var direct=kind switch {
                "attack"=>RosterFacingTurnBattle.Attack(state,new(A,B),new(Sword,2)),
                "self"=>RosterFacingTurnBattle.UseImmediateAbility(state,new(A,Self),RosterDecisionTests.SelfProfile(state.Battle)),
                "stone"=>RosterFacingTurnBattle.UseTargetedAbility(state,new(A,RosterThrowStoneTests.Stone,B),RosterThrowStoneTests.Ability(state.Battle),RosterThrowStoneTests.Heights(state.Battle)),
                _=>RosterFacingTurnBattle.WaitWithFacing(state,A,BattleFacing.South)};
            var continued=RosterMoveDecisionBattle.Continue(state,A,planned.Continuation);Assert.True(continued.Accepted);
            Assert.Equal(Snapshot(direct.State.Battle),Snapshot(continued.State.Battle));Assert.Equal(SerializeEvents(direct.Events),SerializeEvents(continued.Events));
            Assert.Equal(before,Snapshot(state.Battle));Assert.Equal(kind=="wait",continued.State.PendingFacing is null);
        }
    }

    [Fact]
    public void Null_foreign_reconstructed_newer_or_wrong_actor_tokens_cannot_authorize_a_followup()
    {
        var a=Plan(Ready(true),"stone");var b=Plan(Ready(true),"stone");var state=a.State;
        Assert.Equal(FacingMoveRejection.MissingContinuation,RosterMoveDecisionBattle.Continue(state,A,null).Rejection);
        Atomic(state,RosterMoveDecisionBattle.Continue(state,A,b.Continuation));
        var reconstructed=new RosterFacingTurnState(state.Battle,state.Profile,state.Revision,state.PendingFacing,state.Lineage);
        Atomic(reconstructed,RosterMoveDecisionBattle.Continue(reconstructed,A,a.Continuation));
        Assert.Equal(FacingMoveRejection.WrongActor,RosterMoveDecisionBattle.Continue(state,B,a.Continuation).Rejection);
        var ended=RosterFacingTurnBattle.WaitWithFacing(state,A);Assert.True(ended.Accepted);Atomic(ended.State,RosterMoveDecisionBattle.Continue(ended.State,A,a.Continuation));
    }

    [Fact]
    public void Late_actual_rng_failure_preserves_Move_allows_deterministic_retry_and_explicit_END()
    {
        var ready=RosterDecisionTests.WithRng(Ready(true),new(57,ulong.MaxValue-6));var moved=Plan(ready,"stone");Assert.True(moved.Accepted);var before=Snapshot(moved.State.Battle);
        var failed=RosterMoveDecisionBattle.Continue(moved.State,A,moved.Continuation);Atomic(moved.State,failed);Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted,failed.Transition.AttackRejection);
        Atomic(moved.State,RosterMoveDecisionBattle.Continue(moved.State,A,moved.Continuation));Assert.Equal(before,Snapshot(moved.State.Battle));
        Assert.Equal(new TilePosition(1,1,0),Position(failed.State.Battle,A));Assert.True(failed.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).MovementTaken);
        var cancel=RosterFacingTurnBattle.WaitWithFacing(moved.State,A);Assert.True(cancel.Accepted);Assert.Equal(35,cancel.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
        Atomic(cancel.State,RosterMoveDecisionBattle.Continue(cancel.State,A,moved.Continuation));
    }

    [Fact]
    public void Same_original_state_token_replay_matches_values_but_completion_receipts_cannot_cross_replays()
    {
        var moved=Plan(Ready(true),"stone");var first=RosterMoveDecisionBattle.Continue(moved.State,A,moved.Continuation);var second=RosterMoveDecisionBattle.Continue(moved.State,A,moved.Continuation);
        Assert.True(first.Accepted);Assert.True(second.Accepted);Assert.Equal(Snapshot(first.State.Battle),Snapshot(second.State.Battle));Assert.Equal(SerializeEvents(first.Events),SerializeEvents(second.Events));
        Assert.Equal(new RandomState(3448561432,14),first.State.Battle.TurnBattle.Turns.RngState);
        var mixed=RosterFacingTurnBattle.FinishFacing(first.State,A,second.State.PendingFacing);Assert.False(mixed.Accepted);Assert.Same(first.State,mixed.State);
        Assert.True(RosterFacingTurnBattle.FinishFacing(first.State,A,first.State.PendingFacing).Accepted);
    }

    [Fact]
    public void Final_revision_Move_can_commit_but_followup_rejects_without_rewinding_it()
    {
        var ready=Ready(true);var near=new RosterFacingTurnState(ready.Battle,ready.Profile,ulong.MaxValue-1,null,ready.Lineage);var moved=Plan(near,"self");Assert.True(moved.Accepted);
        Assert.Equal(ulong.MaxValue,moved.State.Revision);var fail=RosterMoveDecisionBattle.Continue(moved.State,A,moved.Continuation);Atomic(moved.State,fail);
        Assert.Equal(FacingTurnRejection.RevisionExhausted,fail.Transition.FacingRejection);Assert.Equal(5,fail.State.Battle.Combatants.Single(u=>u.UnitId==A).PhysicalAttack);
    }

    [Fact]
    public void Actual_weapon_and_post_Move_ray_are_revalidated_using_the_frozen_intent()
    {
        var bad=Plan(Ready(true),"attack",new(new("test:wrong"),2));Assert.True(bad.Accepted);var fail=RosterMoveDecisionBattle.Continue(bad.State,A,bad.Continuation);Atomic(bad.State,fail);
        Assert.Equal(AttackCommandRejection.UnsupportedSetup,fail.Transition.AttackRejection);
        var setup=Make([new("a",new(0,1,0),Ct:110),new("b",new(5,1,0),1),new("c",new(3,1,0))],active:false,count:6,ticks:0);
        var ready=RosterFacingTurnBattle.Advance(RosterFacingTurnBattle.Begin(setup.State,setup.Profile)).State;
        var moved=RosterDecisionBattle.Execute(ready,A,c=>c.PrepareMoveThenTargetedAbility(new(A,new(2,1,0)),new(A,RosterThrowStoneTests.Stone,B),RosterThrowStoneTests.Ability(c.Battle),RosterThrowStoneTests.Heights(c.Battle),c.RngState,1));Assert.True(moved.Accepted);
        var actual=RosterMoveDecisionBattle.Continue(moved.State,A,moved.Continuation);Assert.True(actual.Accepted);Assert.Equal(C,Assert.Single(actual.Events.OfType<ThrowStoneResolvedEvent>()).TargetId);
        Assert.Equal(50,actual.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);
    }

    private static void Atomic(RosterFacingTurnState state,RosterMoveContinueResult result)
    {Assert.False(result.Accepted);Assert.Same(state,result.State);Assert.Empty(result.Events);Assert.Null(result.Transition.Path);}
}
