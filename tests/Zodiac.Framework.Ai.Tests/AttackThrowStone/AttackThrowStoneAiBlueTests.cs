using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.ThrowStoneAiTestData;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiMoveFirstExecutionTests;
using static Zodiac.Framework.Ai.Tests.AttackThrowStoneAiPostActExecutionTests;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AttackThrowStoneAiBlueTests
{
    [Fact]
    public void Blue_preparation_preserves_retained_support_policy_and_shared_pair_summaries_before_and_after_ACT()
    {
        foreach(bool retained in new[]{false,true})
        {
            var state=Ready().Battle;string before=Snapshot(state);
            var request=FreshRequest() with{Policies=[new(A,false,retained),new(B,true,true)]};
            var blue=Prepare(state,request:request);Assert.True(blue.Accepted);var b=blue.Outcome!;
            var non=Prepare(state,request:request,configuration:SelectionConfiguration(Selection));Assert.True(non.Accepted);
            Assert.Equal(SwordAiScenarioTeam.Blue,b.Team);Assert.Equal(new RandomState(1103527590,8),blue.RngState);
            Assert.Equal(non.RngState,blue.RngState);Assert.Equal(Serialize(non.Outcome!.ActorEntries),Serialize(b.ActorEntries));
            Assert.Equal(retained,b.Common.Policies.Single(p=>p.UnitId==A).TeamSupportUnavailable);
            Assert.True(non.Outcome.Common.Policies.Single(p=>p.UnitId==A).TeamSupportUnavailable);
            Assert.True(b.Common.Policies.Single(p=>p.UnitId==B).TeamSupportUnavailable);
            Assert.Equal((2,2),(b.EnemyHarmfulAbilityCount,b.EnemyPhysicalAbilityCount));
            Assert.False(b.Common.RetreatSupportAvailable);Assert.True(b.Common.ManualAutobattle);Assert.Equal(0,b.Common.RandomUseThreshold);
            Assert.Equal(before,Snapshot(state));
        }
        var actual=ActualStone();var after=AttackThrowStoneAiPreparation.EvaluateAfterAct(actual.Battle,
            new(FreshRequest(),Stone,new(Sword,2)),Config(),actual.Battle.TurnBattle.Turns.RngState);
        Assert.True(after.Accepted);Assert.Equal(SwordAiScenarioTeam.Blue,after.Outcome!.Team);
        Assert.All(after.Outcome.ActorEntries,e=>Assert.False(e.Usable));Assert.True(after.Outcome.Units.Single(u=>u.UnitId==A).HasRangedAbility);
        Assert.False(after.Outcome.Common.Policies.Single(p=>p.UnitId==A).TeamSupportUnavailable);
        Assert.Equal(actual.Battle.TurnBattle.Turns.RngState.DrawCount+1,after.RngState.DrawCount);
    }

    [Fact] [Trait("Coverage", "CurrentAdmission")]
    public void Blue_control_and_integer_HP_threshold_are_explicit_admission_boundaries()
    {
        var input=Battle();var noControl=Config() with{Preparation=Config().Preparation with{BlueAiControlled=false}};
        Assert.Equal(AttackThrowStoneAiPreparationRejection.UnsupportedTeam,Prepare(input,configuration:noControl).Failure!.Reason);
        Assert.Equal(AttackThrowStoneAiPreparationRejection.UnsupportedTeam,
            Prepare(input,configuration:Config() with{Team=SwordAiScenarioTeam.Unknown}).Failure!.Reason);
        foreach(var example in new[]{new{Hp=44,Max=128,Accepted=false},new{Hp=45,Max=128,Accepted=true},
            new{Hp=17,Max=50,Accepted=false},new{Hp=18,Max=50,Accepted=true}})
        {
            var state=ActorHp(input,example.Hp,example.Max);string before=Snapshot(state);var result=Prepare(state);
            Assert.Equal(example.Accepted,result.Accepted);
            if(!example.Accepted){Assert.Equal(AttackThrowStoneAiPreparationRejection.UnsupportedProfile,result.Failure!.Reason);Assert.Equal(state.TurnBattle.Turns.RngState,result.RngState);Assert.Null(result.Outcome);}
            Assert.Equal(before,Snapshot(state));
        }
    }

    [Fact]
    public void Blue_movement_omits_the_post_score_reach_draw_while_NonBlue_keeps_its_literal_halving_stream()
    {
        var state=Battle(move:1,target:new(6,1,0));string before=Snapshot(state);
        var blue=Scenario(state,new(20,7),Config());Assert.True(blue.Accepted);var b=blue.Outcome!;
        var non=Scenario(state,new(20,7),SelectionConfiguration(Selection));Assert.True(non.Accepted);var n=non.Outcome!;
        Assert.Null(b.ReachAdjustment);Assert.Equal(2,b.EffectiveReach);Assert.Equal(new RandomState(143664818,9),blue.RngState);
        Assert.Equal(new AiReachAdjustment(12502,0,2,1),n.ReachAdjustment);Assert.Equal(new RandomState(2966873603,10),non.RngState);
        Assert.Equal(Serialize(n.Movement),Serialize(b.Movement));Assert.Equal(Serialize(n.ScoreMask),Serialize(b.ScoreMask));
        Assert.Null(b.Cells.Single(c=>c.Tile==Origin).WrittenPriority);
        Assert.Contains(Origin,b.Scenario1);Assert.Contains(Origin,b.Scenario2);Assert.Equal(new TilePosition(2,1,0),b.BaselineDestination);
        var near=Battle();var request=new AttackThrowStoneAiSelectionRequest(FreshRequest(),Stone,new(Sword,2));
        var inBlue=AttackThrowStoneAiPreparedSelection.Evaluate(near,request,Config(),new(1,7));
        var inNon=AttackThrowStoneAiPreparedSelection.Evaluate(near,request,SelectionConfiguration(Selection),new(1,7));
        Assert.True(inBlue.Accepted);Assert.True(inNon.Accepted);Assert.Equal(inNon.RngState,inBlue.RngState);
        Assert.Equal(Serialize(inNon.Outcome!.Selection),Serialize(inBlue.Outcome!.Selection));Assert.Equal(before,Snapshot(state));
    }

    [Fact]
    public void Blue_pre_and_post_ACT_scenarios_fit_without_an_adjustment_draw_and_short_scoring_budgets_roll_back()
    {
        var state=Battle(move:1,target:new(6,1,0));var exact=new RandomState(1,ulong.MaxValue-2);
        var fits=Scenario(state,exact,Config());Assert.True(fits.Accepted);Assert.Equal(ulong.MaxValue,fits.RngState.DrawCount);
        var needsDraw=Scenario(state,exact,SelectionConfiguration(Selection));Assert.False(needsDraw.Accepted);
        Assert.Equal(AttackThrowStoneAiScenarioStage.ReachAdjustment,needsDraw.Failure!.Stage);Assert.Equal(exact,needsDraw.RngState);
        var shortRng=exact with{DrawCount=ulong.MaxValue-1};var shortScore=Scenario(state,shortRng,Config());
        Assert.False(shortScore.Accepted);Assert.Equal(AttackThrowStoneAiScenarioStage.Movement,shortScore.Failure!.Stage);Assert.Equal(shortRng,shortScore.RngState);
        var actual=ActualStone();var request=new AttackThrowStoneAiSelectionRequest(FreshRequest(),Stone,new(Sword,2));
        var after=AttackThrowStoneAiPostActScenarios.EvaluateOutOfReach(actual.Battle,request,Config(),new(1,7));
        Assert.True(after.Accepted);Assert.Null(after.Outcome!.ReachAdjustment);Assert.Equal(after.Outcome.TargetReach,after.Outcome.EffectiveReach);
        Assert.Equal(new RandomState(2524885223,9),after.RngState);Assert.All(after.Outcome.Preparation.ActorEntries,e=>Assert.False(e.Usable));
    }

    [Fact]
    public void Blue_complete_turns_reuse_the_shared_command_driver_with_independent_literal_streams()
    {
        // Blue out-of-reach skips the NonBlue adjustment draw; real ACT continues from that changed stream.
        foreach(var example in new[]{
            new{Name="In-reach Move-first",State=Ready(),Power=1,Rng=new RandomState(2102736524,50),Decisions=1,Ct=15},
            new{Name="Out-of-reach Move-first",State=Ready(move:1,target:new(6,1,0)),Power=1,Rng=new RandomState(2111915288,22),Decisions=1,Ct=15},
            new{Name="Out-of-reach ACT-first",State=Ready(move:0,target:new(4,1,0)),Power=2,Rng=new RandomState(744964398,32),Decisions=2,Ct=35},
            new{Name="Same-tile END",State=Ready(move:0,target:new(6,1,0)),Power=2,Rng=new RandomState(267834847,17),Decisions=1,Ct=55}})
        {
            string before=Snapshot(example.State.Battle);var result=AttackThrowStoneAiTurnExecutionTests.Run(example.State,example.Power,configuration:Config());
            Assert.True(result.Completed,Serialize(result.Failure));Assert.Null(result.Recovery);Assert.Equal(example.Decisions,result.Decisions.Length);
            Assert.Equal(example.Rng,result.State.Battle.TurnBattle.Turns.RngState);Assert.Equal(example.Rng,result.State.Battle.TurnBattle.Battle.RngState);
            Assert.Equal(example.Ct,result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);Assert.Null(result.State.PendingFacing);
            Assert.Single(result.Events.OfType<FacingSettledEvent>());Assert.Equal(before,Snapshot(example.State.Battle));
        }
    }

    [Fact] [Trait("Contract", "QueryIsolation")]
    public void Blue_provenance_replay_and_partial_recovery_keep_the_existing_authority_boundaries()
    {
        foreach(bool second in new[]{false,true})
        {
            var input=Ready(move:1,target:new(6,1,0),second:second);var request=FreshRequest(second);string before=Snapshot(input.Battle);
            var first=AttackThrowStoneAiTurnExecutionTests.Run(input,1,request,Config());Assert.True(first.Completed);
            var reordered=AttackThrowStoneAiTurnExecutionTests.Run(Ready(move:1,target:new(6,1,0),second:second,reverse:true),1,
                request with{Policies=request.Policies.Reverse().ToImmutableArray()},Config());Assert.True(reordered.Completed);
            Assert.Equal(Snapshot(first.State.Battle),Snapshot(reordered.State.Battle));Assert.Equal(Serialize(first.Events),Serialize(reordered.Events));
            Assert.Equal(before,Snapshot(input.Battle));
        }
        var unknown=Ready(move:1,target:new(4,1,0));var rejected=AttackThrowStoneAiTurnExecutionTests.Run(unknown,configuration:Config());
        Assert.False(rejected.Completed);Assert.Same(unknown,rejected.State);Assert.Empty(rejected.Events);
        Assert.Equal(AttackThrowStoneAiPreparedSelectionRejection.UnwrittenCastPriority,rejected.Failure!.CommandFailure!.BeforeActFailure!.Reason);
        var exact=AttackThrowStoneAiTurnExecutionTests.Run(Ready(move:1,target:new(6,1,0),count:ulong.MaxValue-15),1,configuration:Config());
        Assert.True(exact.Completed);Assert.Equal(ulong.MaxValue,exact.State.Battle.TurnBattle.Turns.RngState.DrawCount);
        var shortTurn=AttackThrowStoneAiTurnExecutionTests.Run(Ready(move:1,target:new(6,1,0),count:ulong.MaxValue-14),1,configuration:Config());
        Assert.False(shortTurn.Completed);Assert.Equal(AttackThrowStoneAiTurnStage.RetainedCommand,shortTurn.Failure!.Stage);
        var retry=AttackThrowStoneAiTurnExecution.Resume(shortTurn.State,shortTurn.Recovery!);
        Assert.False(retry.Completed);Assert.Same(shortTurn.State,retry.State);Assert.Empty(retry.Events);Assert.Empty(retry.Decisions);
    }

    private static AttackThrowStoneAiSelectionConfiguration Config()=>SelectionConfiguration(Selection,SwordAiScenarioTeam.Blue) with{
        Preparation=new(true,true,true,BlueAiControlled:true)};
    private static AttackThrowStoneAiPreparationResult Prepare(AttackBattleSnapshot state,SwordAiRefreshRequest? request=null,
        AttackThrowStoneAiSelectionConfiguration? configuration=null)
    {
        var c=configuration??Config();return AttackThrowStoneAiPreparation.Evaluate(state,request??FreshRequest(),Stone,new(Sword,2),c.Attack,c.ThrowStone,
            c.Scoring,c.Refresh,c.Preparation,c.Team,state.TurnBattle.Turns.RngState);
    }
    private static AttackThrowStoneAiOutOfReachScenarioResult Scenario(AttackBattleSnapshot state,RandomState rng,AttackThrowStoneAiSelectionConfiguration c)=>
        AttackThrowStoneAiOutOfReachScenario.Evaluate(state,FreshRequest(),Stone,new(Sword,2),c.Attack,c.ThrowStone,c.Scoring,c.Refresh,c.Preparation,
            new(true,true,true),c.Team,rng);
    private static FacingTurnState ActualStone()
    {
        var result=FacingTurnBattle.UseTargetedAbility(Ready(move:0,target:new(4,1,0)),new(A,Stone,B),Profile().ThrowStone.Ability);
        Assert.True(result.Accepted);return result.State;
    }
    private static AttackBattleSnapshot ActorHp(AttackBattleSnapshot state,int hp,int max)=>new(state.TurnBattle,state.Combatants.Select(u=>u.UnitId!=A ? u:
        new AttackUnitState(u.UnitId,hp,max,u.Mp,u.MaxMp,u.PhysicalAttack,u.ClassPhysicalEvade,u.RightHandEvade,u.LeftHandEvade,u.AccessoryEvade,
            u.Brave,u.ZodiacSign,u.Facing,hp<=max/5 ? AttackUnitStatus.Critical:AttackUnitStatus.None,u.KoCount,u.RightHandWeaponId)));
}
