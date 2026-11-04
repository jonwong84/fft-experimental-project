using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiTurnExecutionTests
{
    [Fact]
    public void Complete_turns_route_actual_choices_and_receipts_with_established_literal_streams()
    {
        // FinalRng is internal word/cumulative draw count after the entire settled turn; Ready enters at (1,7) unless specified.
        var cases = new[] {
            new { Name="Stone Move-first", State=Ready(), Power=1, FinalRng=new RandomState(2102736524,50), Decisions=1, Ct=15 },
            new { Name="Attack Move-first", State=Ready(left:true), Power=5, FinalRng=new RandomState(1343201072,30), Decisions=1, Ct=15 },
            new { Name="Empty Move-first", State=Ready(left:true,evade:100), Power=1, FinalRng=new RandomState(2473755763,29), Decisions=1, Ct=35 },
            new { Name="Attack ACT-first", State=Ready(), Power=2, FinalRng=new RandomState(1223573862,72), Decisions=2, Ct=15 },
            new { Name="Stone push changes reach", State=Ready(move:0,entryWord:20), Power=1, FinalRng=new RandomState(713082951,38), Decisions=2, Ct=35 },
            new { Name="Same-tile END", State=Ready(move:0,target:new(6,1,0)), Power=2, FinalRng=new RandomState(180171308,18), Decisions=1, Ct=55 } };
        foreach (var example in cases)
        {
            string before=Snapshot(example.State.Battle); var result=Run(example.State,example.Power);
            Assert.True(result.Completed,Serialize(result.Failure)); Assert.Null(result.Recovery);
            Assert.Equal(example.Decisions,result.Decisions.Length); Assert.Null(result.State.PendingFacing);
            Assert.Null(result.State.Battle.TurnBattle.Turns.ActiveUnitId);
            Assert.Equal(example.FinalRng,result.State.Battle.TurnBattle.Turns.RngState);
            Assert.Equal(example.FinalRng,result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(example.Ct,result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
            Assert.Single(result.Events.OfType<FacingSettledEvent>());
            Assert.Equal(example.State.Battle.TurnBattle.Turns.Ticks,result.State.Battle.TurnBattle.Turns.Ticks);
            Assert.Equal(before,Snapshot(example.State.Battle));
        }
    }

    [Fact]
    public void Late_failures_keep_published_progress_and_exact_recovery_does_not_repeat_events_or_decisions()
    {
        foreach (bool actFirst in new[]{false,true})
        {
            var input=Ready(count:ulong.MaxValue-(actFirst ? 64ul:42ul));
            var result=Run(input,actFirst ? 2:1); Assert.False(result.Completed);
            Assert.Equal(actFirst ? AttackThrowStoneAiTurnStage.FreshCommand:AttackThrowStoneAiTurnStage.RetainedCommand,result.Failure!.Stage);
            Assert.Single(result.Decisions); Assert.NotEmpty(result.Events); Assert.NotSame(input,result.State);
            Assert.Equal(actFirst ? 40:50,result.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);
            Assert.Equal(actFirst,result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).ActionTaken);
            Assert.Equal(!actFirst,result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).MovementTaken);
            string before=Snapshot(result.State.Battle);
            var retry=AttackThrowStoneAiTurnExecution.Resume(result.State,result.Recovery!);
            Assert.False(retry.Completed); Assert.Same(result.State,retry.State); Assert.Empty(retry.Events); Assert.Empty(retry.Decisions);
            Assert.Equal(result.Failure.Stage,retry.Failure!.Stage); Assert.Equal(before,Snapshot(result.State.Battle));
            var replay=Run(input,actFirst ? 2:1); Assert.Equal(before,Snapshot(replay.State.Battle));
            var foreign=AttackThrowStoneAiTurnExecution.Resume(replay.State,result.Recovery!);
            Assert.Equal(AttackThrowStoneAiTurnStage.RecoveryBinding,foreign.Failure!.Stage);
            Assert.Same(replay.State,foreign.State); Assert.Empty(foreign.Events); Assert.Null(foreign.Recovery);
        }
        var unknown=Ready(move:1,target:new(4,1,0)); var rejected=Run(unknown);
        Assert.False(rejected.Completed); Assert.Same(unknown,rejected.State); Assert.Empty(rejected.Events); Assert.Empty(rejected.Decisions);
        Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnwrittenCastPriority,rejected.Failure!.CommandFailure!.BeforeActFailure!.Reason);
    }

    [Fact]
    public void Lethal_ACT_survives_unsupported_post_ACT_or_facing_without_inventing_terminal_policy()
    {
        foreach (bool actFirst in new[]{false,true})
        {
            var input=actFirst ? Ready(move:0,hp:10) : Ready(left:true,hp:25);
            var result=Run(input,actFirst ? 1:5);
            Assert.False(result.Completed); Assert.Equal(0,result.State.Battle.Combatants.Single(u=>u.UnitId==B).Hp);
            Assert.Single(result.Events.Where(e=>e is ThrowStoneResolvedEvent or AttackResolvedEvent)); Assert.Empty(result.Events.OfType<FacingSettledEvent>());
            Assert.Equal(actFirst ? AttackThrowStoneAiTurnStage.FreshCommand:AttackThrowStoneAiTurnStage.FinishFacing,result.Failure!.Stage);
            if(actFirst) Assert.Equal(SwordAiRefreshRejection.TerminalInput,result.Failure.CommandFailure!.AfterActFailure!.ScenarioFailure!.PreparationFailure!.RefreshRejection);
            else { Assert.Equal(FacingTurnRejection.NoLivingEnemy,result.Failure.FacingFailure!.FacingRejection); Assert.NotNull(result.State.PendingFacing); }
            var retry=AttackThrowStoneAiTurnExecution.Resume(result.State,result.Recovery!);
            Assert.False(retry.Completed); Assert.Same(result.State,retry.State); Assert.Empty(retry.Events);
        }
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Both_actor_identities_and_collection_orders_replay_the_complete_turn()
    {
        foreach(bool second in new[]{false,true})
        {
            var request=FreshRequest(second);var input=Ready(move:1,target:new(6,1,0),second:second);string before=Snapshot(input.Battle);
            var first=Run(input,1,request);Assert.True(first.Completed);
            var replay=Run(input,1,request);Assert.True(replay.Completed);
            var reordered=Run(Ready(move:1,target:new(6,1,0),second:second,reverse:true),1,request with{Policies=request.Policies.Reverse().ToImmutableArray()});
            Assert.True(reordered.Completed);Assert.Equal(Snapshot(first.State.Battle),Snapshot(replay.State.Battle));
            Assert.Equal(Snapshot(first.State.Battle),Snapshot(reordered.State.Battle));Assert.Equal(Serialize(first.Events),Serialize(reordered.Events));
            Assert.Equal(Serialize(first.Decisions),Serialize(reordered.Decisions));Assert.Equal(before,Snapshot(input.Battle));
        }
    }

    internal static AttackThrowStoneAiTurnResult Run(FacingTurnState state,int power=2,SwordAiRefreshRequest? request=null,
        AttackThrowStoneAiSelectionConfiguration? configuration=null)=>AttackThrowStoneAiTurnExecution.Execute(state,
            new(request??FreshRequest(),Stone,new(Sword,power)),configuration??SelectionConfiguration(Selection),Dispatch);
}
