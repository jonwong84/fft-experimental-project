using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset","OriginalFft")]
public sealed class AttackThrowStoneAiOutOfReachActFirstTests
{
    [Fact]
    public void Fresh_zero_Move_actor_origin_executes_real_Stone_without_spending_Move_or_issuing_a_receipt()
    {
        // Decision/Actual are internal state words and cumulative counts, starting from Ready's (seed,7).
        foreach(var example in new[] {
            new { Seed=1u, Hp=50, Decision=new RandomState(3210001534,16), Actual=new RandomState(3297241969,23), Damage=5, Push=true },
            new { Seed=20u, Hp=50, Decision=new RandomState(4007910005,16), Actual=new RandomState(3660420676,23), Damage=10, Push=false },
            new { Seed=1u, Hp=10, Decision=new RandomState(267834847,17), Actual=new RandomState(3791773014,24), Damage=10, Push=false } })
        {
            var state=Ready(move:0,target:new(4,1,0),entryWord:example.Seed,hp:example.Hp);string before=Snapshot(state.Battle);
            var result=Start(state);var o=Success(result);
            Assert.Equal(example.Decision,o.DecisionRng);Assert.Equal(example.Actual,result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(example.Actual,result.State.Battle.TurnBattle.Battle.RngState);
            Assert.True(o.Decision.Selection.Plan.ActBeforeMove);Assert.Equal(Origin,o.Decision.Selection.Plan.Destination);
            Assert.Equal(AttackThrowStoneAiPlanKind.ThrowStone,o.Decision.Selection.Plan.Kind);
            Assert.Equal(new ThrowStoneResolvedEvent(A,Stone,B,true,example.Damage,example.Push),Assert.Single(result.Events.OfType<ThrowStoneResolvedEvent>()));
            Assert.Equal(example.Hp-example.Damage,result.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);
            Assert.Equal(example.Seed==20, o.Decision.MovementScenario.ReachAdjustment.Halved);
            var actor=result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A);
            Assert.True(actor.HasTurn);Assert.True(actor.ActionTaken);Assert.False(actor.MovementTaken);Assert.Equal(15,actor.Ct);
            Assert.Null(result.State.PendingFacing);Assert.Equal(state.Revision+1,result.State.Revision);
            Assert.Empty(result.Events.OfType<VoluntaryPathCompletedEvent>());Assert.Empty(result.Events.OfType<FacingSettledEvent>());
            Assert.Equal(before,Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Actual_draw_exhaustion_rolls_back_private_selection_while_exact_hit_and_miss_budgets_fit()
    {
        foreach(var example in new[] {
            new {Seed=1u,Evade=0,Hp=50,Total=16ul},new {Seed=20u,Evade=0,Hp=50,Total=16ul},
            new {Seed=1u,Evade=0,Hp=10,Total=17ul},new {Seed=20u,Evade=75,Hp=50,Total=20ul} })
        {
            var state=Ready(move:0,target:new(4,1,0),entryWord:example.Seed,evade:example.Evade,hp:example.Hp,count:ulong.MaxValue-example.Total);
            var exact=Start(state);Success(exact);Assert.Equal(ulong.MaxValue,exact.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            var shortState=Ready(move:0,target:new(4,1,0),entryWord:example.Seed,evade:example.Evade,hp:example.Hp,count:ulong.MaxValue-example.Total+1);
            var failed=Start(shortState);Atomic(shortState,failed,AttackThrowStoneAiActFirstRejection.BoundaryRejected);
            Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted,failed.Execution.Transition.AttackRejection);
        }
    }

    [Fact] [Trait("Coverage","CurrentAdmission")]
    public void Unwritten_cast_scores_wrong_branch_empty_plans_source_context_and_spent_choices_reject()
    {
        var unknown=Ready(move:1,target:new(4,1,0));var failed=Start(unknown);
        Atomic(unknown,failed,AttackThrowStoneAiActFirstRejection.SelectionRejected);
        Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnwrittenCastPriority,failed.Failure!.SelectionFailure!.Reason);
        var near=Ready();Atomic(near,Start(near),AttackThrowStoneAiActFirstRejection.SelectionRejected);
        var far=Ready(move:0,target:new(6,1,0));Atomic(far,Start(far),AttackThrowStoneAiActFirstRejection.ActFirstRequired);
        var state=Ready(move:0,target:new(4,1,0));
        Atomic(state,Start(state,dispatch:new()),AttackThrowStoneAiActFirstRejection.UnsupportedDispatchContext);
        var acted=Start(state);Success(acted);Atomic(acted.State,Start(acted.State),AttackThrowStoneAiActFirstRejection.UnavailableChoices);
    }

    [Fact] [Trait("Contract","QueryIsolation")]
    public void Both_actor_identities_and_collection_orders_replay_the_same_actual_result()
    {
        foreach(bool second in new[]{false,true})
        {
            var request=FreshRequest(second);var state=Ready(move:0,target:new(4,1,0),second:second);string before=Snapshot(state.Battle);
            var first=Start(state,request);Success(first);var replay=Start(state,request);Success(replay);
            var reversed=Start(Ready(move:0,target:new(4,1,0),second:second,reverse:true),request with{Policies=request.Policies.Reverse().ToImmutableArray()});Success(reversed);
            Assert.Equal(Serialize(first.Outcome),Serialize(replay.Outcome));Assert.Equal(Serialize(first.Outcome),Serialize(reversed.Outcome));
            Assert.Equal(Snapshot(first.State.Battle),Snapshot(reversed.State.Battle));Assert.Equal(before,Snapshot(state.Battle));
        }
    }

    internal static AttackThrowStoneAiOutOfReachActFirstResult Start(FacingTurnState state,SwordAiRefreshRequest? request=null,SwordAiCommandDispatchProfile? dispatch=null) =>
        AttackThrowStoneAiActFirstExecution.StartOutOfReach(state,new(request??FreshRequest(),Stone,new(Sword,2)),SelectionConfiguration(Selection),dispatch??Dispatch);
    private static AttackThrowStoneAiOutOfReachActFirstOutcome Success(AttackThrowStoneAiOutOfReachActFirstResult result)
    {Assert.True(result.Accepted,Serialize(result.Failure));Assert.Null(result.Failure);return Assert.IsType<AttackThrowStoneAiOutOfReachActFirstOutcome>(result.Outcome);}
    private static void Atomic(FacingTurnState state,AttackThrowStoneAiOutOfReachActFirstResult result,AttackThrowStoneAiActFirstRejection reason)
    {Assert.False(result.Accepted);Assert.Same(state,result.State);Assert.Empty(result.Events);Assert.Null(result.Outcome);Assert.Equal(reason,result.Failure!.Reason);}
}
