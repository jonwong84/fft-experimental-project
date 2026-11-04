using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActPreparationTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiPostActExecutionTests
{
    internal static readonly AttackThrowStoneAiPreparedSelectionProfile Selection = new(true, true, true, true, true);
    internal static readonly SwordAiCommandDispatchProfile Dispatch = new(true, true, true, true);

    [Fact]
    public void Actual_hit_miss_and_push_settle_real_paths_or_END_using_one_fresh_decision()
    {
        // DecisionRng is the internal word/cumulative count after refresh, movement and eight empty lottery draws.
        var cases = new[] {
            new { Name = "Attack / move", State = Act(Ready()), Outside = false, Power = 2,
                Destination = new TilePosition(1,4,0), DecisionRng = new RandomState(1223573862,72) },
            new { Name = "Stone hit / END", State = Act(Ready(move:0),power:1), Outside = false, Power = 1,
                Destination = Origin, DecisionRng = new RandomState(3834121963,37) },
            new { Name = "Stone miss / END", State = Act(Ready(move:0,evade:75),power:1), Outside = false, Power = 1,
                Destination = Origin, DecisionRng = new RandomState(1448521415,41) },
            new { Name = "Stone push changes branch", State = Act(Ready(move:0,entryWord:20),power:1), Outside = true, Power = 1,
                Destination = Origin, DecisionRng = new RandomState(713082951,38) },
            new { Name = "Far push / move", State = Pushed(), Outside = true, Power = 2,
                Destination = new TilePosition(3,2,0), DecisionRng = new RandomState(1073193443,42) } };
        foreach (var example in cases)
        {
            var state = example.State; string before = Snapshot(state.Battle);
            var result = Execute(state, example.Outside, example.Power); var o = Success(result);
            Assert.Equal(example.DecisionRng, o.DecisionRng);
            Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(o.DecisionRng, result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(example.Destination, result.State.Battle.TurnBattle.Battle.Units.Single(u=>u.Id==A).Position);
            bool moved = example.Destination != Origin;
            Assert.Equal(moved ? SwordAiDispatchKind.Move : SwordAiDispatchKind.EndTurn, o.Dispatch.Kind);
            if (moved) { Assert.Equal(Origin,result.Path!.Tiles[0]); Assert.Equal(example.Destination,result.Path.Tiles[^1]); }
            else Assert.Null(result.Path);
            var actor = result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A);
            Assert.False(actor.HasTurn); Assert.True(actor.ActionTaken); Assert.Equal(moved,actor.MovementTaken);
            Assert.Equal(moved ? 15 : 35,actor.Ct); Assert.Equal(95,result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==B).Ct);
            var target = state.Battle.TurnBattle.Battle.Units.Single(u=>u.Id==B).Position;
            Assert.Equal(FacingTurnBattle.DirectionToward(example.Destination,target),result.State.Battle.Combatants.Single(u=>u.UnitId==A).Facing);
            Assert.Equal(state.Battle.TurnBattle.Turns.Ticks,result.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(state.Revision+(moved ? 2ul : 1ul),result.State.Revision); Assert.Null(result.State.PendingFacing);
            Assert.Single(result.Events.OfType<FacingSettledEvent>());
            Assert.Equal(moved ? 1 : 0,result.Events.OfType<VoluntaryPathCompletedEvent>().Count());
            Assert.Empty(result.Events.OfType<AttackResolvedEvent>()); Assert.Empty(result.Events.OfType<ThrowStoneResolvedEvent>());
            Assert.Equal(state.Battle.Combatants.Single(u=>u.UnitId==B).Hp,result.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);
            Assert.Equal(before,Snapshot(state.Battle));
        }
    }

    [Fact]
    public void Last_lottery_failure_preserves_published_ACT_and_exact_budgets_fit()
    {
        // Cost is counted from Ready: actual ACT-first transaction plus the fresh post-ACT decision.
        foreach (var example in new[] {
            new { Move=3, Power=2, Evade=0, Seed=1u, Total=65ul },
            new { Move=0, Power=1, Evade=0, Seed=1u, Total=30ul },
            new { Move=0, Power=1, Evade=75, Seed=1u, Total=34ul },
            new { Move=0, Power=1, Evade=0, Seed=20u, Total=31ul } })
        {
            bool outside=example.Seed==20;
            var exact = Act(Ready(move:example.Move,evade:example.Evade,entryWord:example.Seed,count:ulong.MaxValue-example.Total),power:example.Power);
            var fits=Execute(exact,outside,example.Power); Success(fits);
            Assert.Equal(ulong.MaxValue,fits.State.Battle.TurnBattle.Turns.RngState.DrawCount);
            var state=Act(Ready(move:example.Move,evade:example.Evade,entryWord:example.Seed,count:ulong.MaxValue-example.Total+1),power:example.Power);
            var failed=Execute(state,outside,example.Power); Atomic(state,failed,AttackThrowStoneAiPostActExecutionRejection.SelectionRejected);
            Assert.Equal(7,failed.Failure!.SelectionFailure!.FailedLotteryIndex);
            Assert.True(state.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).ActionTaken);
        }
    }

    [Fact] [Trait("Coverage","CurrentAdmission")]
    public void Choice_branch_source_context_terminal_and_stale_actor_reject_without_adoption()
    {
        var state=Act(Ready());
        Atomic(state,Execute(state,true),AttackThrowStoneAiPostActExecutionRejection.SelectionRejected);
        Atomic(state,Execute(state,dispatch:new()),AttackThrowStoneAiPostActExecutionRejection.UnsupportedDispatchContext);
        Atomic(state,Execute(state,configuration:SelectionConfiguration(Selection with {NoDefendSupport=false})),AttackThrowStoneAiPostActExecutionRejection.SelectionRejected);
        Atomic(state,Execute(state,request:FreshRequest(second:true)),AttackThrowStoneAiPostActExecutionRejection.BoundaryRejected);
        var ready=Ready(); Atomic(ready,Execute(ready),AttackThrowStoneAiPostActExecutionRejection.UnavailableChoices);
        var terminal=Act(Ready(move:0,hp:10),power:1);
        Atomic(terminal,Execute(terminal,power:1),AttackThrowStoneAiPostActExecutionRejection.SelectionRejected);
        var ended=Execute(state);Success(ended);
        Atomic(ended.State,Execute(ended.State),AttackThrowStoneAiPostActExecutionRejection.BoundaryRejected);
    }

    [Fact] [Trait("Contract","QueryIsolation")]
    public void Both_actors_and_input_orders_replay_the_actual_settled_state()
    {
        foreach(bool second in new[]{false,true})
        foreach(bool outside in new[]{false,true})
        {
            var request=FreshRequest(second);
            var state=outside ? Pushed(second) : Act(Ready(second:second),request:request);
            string before=Snapshot(state.Battle);
            var first=Execute(state,outside,request:request);Success(first);
            var replay=Execute(state,outside,request:request);Success(replay);
            var reversedRequest=request with {Policies=request.Policies.Reverse().ToImmutableArray()};
            var reversed=outside ? Pushed(second,true) : Act(Ready(second:second,reverse:true),request:reversedRequest);
            var reordered=Execute(reversed,outside,request:reversedRequest);Success(reordered);
            Assert.Equal(Snapshot(first.State.Battle),Snapshot(replay.State.Battle));
            Assert.Equal(Snapshot(first.State.Battle),Snapshot(reordered.State.Battle));
            Assert.Equal(Serialize(first.Outcome),Serialize(reordered.Outcome)); Assert.Equal(Serialize(first.Events),Serialize(reordered.Events));
            Assert.Equal(before,Snapshot(state.Battle));
        }
    }

    private static FacingTurnState Pushed(bool second=false,bool reverse=false)
    {
        var result=FacingTurnBattle.UseTargetedAbility(Ready(move:3,target:new(5,1,0),entryWord:2711922929,count:20,second:second,reverse:reverse),
            new(second ? B:A,Stone,second ? A:B),Profile().ThrowStone.Ability);
        Assert.True(result.Accepted);return result.State;
    }
    internal static AttackThrowStoneAiPostActExecutionResult Execute(FacingTurnState state,bool outside=false,int power=2,
        SwordAiRefreshRequest? request=null,AttackThrowStoneAiSelectionConfiguration? configuration=null,SwordAiCommandDispatchProfile? dispatch=null) => outside
        ? AttackThrowStoneAiPostActExecution.ExecuteOutOfReach(state,new(request??FreshRequest(),Stone,new(Sword,power)),configuration??SelectionConfiguration(Selection),dispatch??Dispatch)
        : AttackThrowStoneAiPostActExecution.ExecuteInReach(state,new(request??FreshRequest(),Stone,new(Sword,power)),configuration??SelectionConfiguration(Selection),dispatch??Dispatch);
    private static AttackThrowStoneAiPostActExecutionOutcome Success(AttackThrowStoneAiPostActExecutionResult result)
    { Assert.True(result.Accepted,Serialize(result.Failure));Assert.Null(result.Failure);return Assert.IsType<AttackThrowStoneAiPostActExecutionOutcome>(result.Outcome); }
    private static void Atomic(FacingTurnState state,AttackThrowStoneAiPostActExecutionResult result,AttackThrowStoneAiPostActExecutionRejection reason)
    { Assert.False(result.Accepted);Assert.Equal(reason,result.Failure!.Reason);Assert.Same(state,result.State);Assert.Empty(result.Events);Assert.Null(result.Path);Assert.Null(result.Outcome); }
}
