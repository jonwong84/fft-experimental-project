using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class AttackThrowStoneAiFallbackExecutionTests
{
    [Fact]
    public void Fresh_zero_accuracy_or_no_cast_same_tile_fallback_ends_with_both_choice_refunds()
    {
        foreach(bool outside in new[]{false,true})
        {
            var state=Input(outside);string before=Snapshot(state.Battle);var result=End(state,outside);var o=Success(result);
            Assert.Equal(AttackThrowStoneAiPlanKind.MoveOnly,o.Decision.Plan.Kind);Assert.Null(o.Decision.Plan.Action);
            Assert.Equal(Origin,o.Decision.Plan.Destination);Assert.Equal(8,o.Decision.Lottery.Length);
            Assert.All(o.Decision.Lottery,r=>{Assert.Equal(0,r.HitPercent);Assert.False(r.Passed);});
            Assert.Equal(AiLotteryReason.MovementFallback,o.Decision.Reason);
            Assert.Equal(o.DecisionRng,result.State.Battle.TurnBattle.Turns.RngState);Assert.Equal(o.DecisionRng,result.State.Battle.TurnBattle.Battle.RngState);
            var actor=result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A);
            Assert.False(actor.HasTurn);Assert.False(actor.ActionTaken);Assert.False(actor.MovementTaken);Assert.Equal(55,actor.Ct);
            Assert.Equal(95,result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==B).Ct);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks,result.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(state.Revision+1,result.State.Revision);Assert.Null(result.State.PendingFacing);
            Assert.Single(result.Events.OfType<FacingSettledEvent>());Assert.Empty(result.Events.OfType<VoluntaryPathCompletedEvent>());
            Assert.Empty(result.Events.OfType<AttackResolvedEvent>());Assert.Empty(result.Events.OfType<ThrowStoneResolvedEvent>());
            Assert.Null(result.Execution.Path);Assert.Equal(before,Snapshot(state.Battle));
            // The no-cast branch costs refresh1 + singleton movement1 + reach1 + empty lottery8.
            if(outside)Assert.Equal(new RandomState(180171308,18),o.DecisionRng);
        }
    }

    [Fact] [Trait("Coverage","CurrentAdmission")]
    public void Different_tile_action_unknown_score_wrong_branch_and_spent_choice_never_become_a_cheaper_END()
    {
        var action=Ready(move:0);Atomic(action,End(action),AttackThrowStoneAiFallbackRejection.SameTileFallbackRequired);
        var move=Ready(left:true,evade:100);Atomic(move,End(move),AttackThrowStoneAiFallbackRejection.SameTileFallbackRequired);
        var unknown=Ready(move:1,target:new(4,1,0));var rejected=End(unknown,true);
        Atomic(unknown,rejected,AttackThrowStoneAiFallbackRejection.SelectionRejected);
        Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnwrittenCastPriority,rejected.Failure!.SelectionFailure!.Reason);
        var state=Input(false);Atomic(state,End(state,true),AttackThrowStoneAiFallbackRejection.SelectionRejected);
        Atomic(state,End(state,dispatch:new()),AttackThrowStoneAiFallbackRejection.UnsupportedDispatchContext);
        var acted=AttackThrowStoneAiPostActPreparationTests.Act(Ready(move:0),power:1);
        Atomic(acted,End(acted),AttackThrowStoneAiFallbackRejection.UnavailableChoices);
        var ended=End(state);Success(ended);Atomic(ended.State,End(ended.State),AttackThrowStoneAiFallbackRejection.BoundaryRejected);
    }

    [Fact] [Trait("Contract","QueryIsolation")]
    public void Exact_no_cast_budget_last_roll_rollback_and_reordered_actor_replay_keep_inputs_immutable()
    {
        var exact=Input(true,count:ulong.MaxValue-11);var fits=End(exact,true);Success(fits);Assert.Equal(ulong.MaxValue,fits.Outcome!.DecisionRng.DrawCount);
        var shortState=Input(true,count:ulong.MaxValue-10);var failed=End(shortState,true);
        Atomic(shortState,failed,AttackThrowStoneAiFallbackRejection.SelectionRejected);Assert.Equal(7,failed.Failure!.SelectionFailure!.SelectionFailure!.LotteryIndex);
        foreach(bool outside in new[]{false,true})
        foreach(bool second in new[]{false,true})
        {
            var request=FreshRequest(second);var state=Input(outside,second);string before=Snapshot(state.Battle);
            var first=End(state,outside,request);Success(first);var replay=End(state,outside,request);Success(replay);
            var reordered=End(Input(outside,second,true),outside,request with{Policies=request.Policies.Reverse().ToImmutableArray()});Success(reordered);
            Assert.Equal(Serialize(first.Outcome),Serialize(replay.Outcome));Assert.Equal(Serialize(first.Outcome),Serialize(reordered.Outcome));
            Assert.Equal(Snapshot(first.State.Battle),Snapshot(reordered.State.Battle));Assert.Equal(before,Snapshot(state.Battle));
        }
    }

    internal static FacingTurnState Input(bool outside,bool second=false,bool reverse=false,ulong count=7) =>
        Ready(move:0,target:outside ? new(6,1,0):null,evade:outside ? 0:100,second:second,reverse:reverse,count:count);
    internal static AttackThrowStoneAiFallbackResult End(FacingTurnState state,bool outside=false,SwordAiRefreshRequest? request=null,SwordAiCommandDispatchProfile? dispatch=null) => outside
        ? AttackThrowStoneAiFallbackExecution.ExecuteOutOfReach(state,new(request??FreshRequest(),Stone,new(Sword,2)),SelectionConfiguration(Selection),dispatch??Dispatch)
        : AttackThrowStoneAiFallbackExecution.ExecuteInReach(state,new(request??FreshRequest(),Stone,new(Sword,2)),SelectionConfiguration(Selection),dispatch??Dispatch);
    private static AttackThrowStoneAiFallbackOutcome Success(AttackThrowStoneAiFallbackResult result)
    {Assert.True(result.Accepted,Serialize(result.Failure));Assert.Null(result.Failure);return Assert.IsType<AttackThrowStoneAiFallbackOutcome>(result.Outcome);}
    private static void Atomic(FacingTurnState state,AttackThrowStoneAiFallbackResult result,AttackThrowStoneAiFallbackRejection reason)
    {Assert.False(result.Accepted);Assert.Equal(reason,result.Failure!.Reason);Assert.Same(state,result.State);Assert.Empty(result.Events);Assert.Null(result.Outcome);Assert.Null(result.Execution.Path);}
}
