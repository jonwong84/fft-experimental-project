using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterDecisionTests
{
    private static readonly AssetId Self = new("test:roster-self");
    internal static ImmediateAbilityBattleProfile SelfProfile(AttackBattleSnapshot state,bool granted=true) => new([new(Self)],state.Combatants.Select(u=>new ImmediateAbilityUnitGrant(u.UnitId,u.UnitId==A && granted ? [Self] : [],0)));
    internal static RosterFacingTurnState WithRng(RosterFacingTurnState s,RandomState rng)
    {
        var old=s.Battle.TurnBattle;var b=new AttackBattleSnapshot(new(new(old.Battle.Map,old.Battle.Units,rng),new(old.Turns.Units,rng,old.Turns.Ticks,old.Turns.Phase)),s.Battle.Combatants);
        return new(b,s.Profile,s.Revision,s.PendingFacing,s.Lineage);
    }

    [Fact]
    public void Seven_checkpoint_contracts_validate_full_word_count_without_enumerating_large_deltas()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-authority-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray().Where(r=>r.GetProperty("kind").GetString()=="managed-rng-checkpoint"))
        {
            var d=row.GetProperty("input");var beforeRng=new RandomState(d.GetProperty("beforeWord").GetUInt32(),d.GetProperty("beforeCount").GetUInt64());
            var proposed=new RandomState(d.GetProperty("afterWord").GetUInt32(),d.GetProperty("afterCount").GetUInt64());var state=WithRng(Ready(),beforeRng);var before=Snapshot(state.Battle);
            var result=RosterDecisionBattle.Execute(state,A,c=>c.PrepareImmediateAbility(new(A,Self),SelfProfile(c.Battle),proposed,"self"));
            Assert.Equal(row.GetProperty("expected").GetBoolean(),result.Accepted);
            if(result.Accepted)
            {
                Assert.Equal(proposed,result.State.Battle.TurnBattle.Turns.RngState);Assert.Equal(proposed,result.State.Battle.TurnBattle.Battle.RngState);
                Assert.Equal(state.Revision+1,result.State.Revision);Assert.Equal(6,result.State.Battle.Combatants.Single(u=>u.UnitId==A).PhysicalAttack);Assert.Equal("self",result.Diagnostic);
            }
            else {Atomic(state,result);Assert.Equal(FacingDecisionRejection.InvalidRandomProgress,result.Rejection);}
            Assert.Equal(before,Snapshot(state.Battle));count++;
        }
        Assert.Equal(7,count);
    }

    [Fact]
    public void Contexts_close_on_return_or_throw_and_foreign_cached_or_null_proposals_reject()
    {
        var state=Ready();RosterDecisionContext? leaked=null;RosterDecision<int>? cached=null;
        var first=RosterDecisionBattle.Execute(state,A,c=>{leaked=c;cached=c.PrepareImmediateAbility(new(A,Self),SelfProfile(c.Battle),c.RngState,1);return cached;});Assert.True(first.Accepted);
        Assert.Throws<InvalidOperationException>(()=>leaked!.PrepareWait(null,leaked.RngState,1));
        Atomic(state,RosterDecisionBattle.Execute(state,A,_=>cached!));Atomic(state,RosterDecisionBattle.Execute<int>(state,A,_=>null!));
        Assert.Throws<InvalidOperationException>(()=>RosterDecisionBattle.Execute<int>(state,A,c=>{leaked=c;throw new InvalidOperationException("planner");}));
        Assert.Throws<InvalidOperationException>(()=>leaked!.Reject<int>());
        Atomic(state,RosterDecisionBattle.Execute<int>(state,A,c=>c.Reject<int>()));
    }

    [Fact]
    public void Actual_command_failure_rolls_back_adopted_rng_and_actor_authority_is_checked()
    {
        var state=Ready();var next=new RandomStateGenerator().Next(state.Battle.TurnBattle.Turns.RngState).State;
        var badGrant=RosterDecisionBattle.Execute(state,A,c=>c.PrepareImmediateAbility(new(A,Self),SelfProfile(c.Battle,false),next,"bad"));Atomic(state,badGrant);Assert.Equal(AttackCommandRejection.AbilityNotGranted,badGrant.Transition.AttackRejection);
        Atomic(state,RosterDecisionBattle.Execute(state,A,c=>c.PrepareImmediateAbility(new(B,Self),SelfProfile(c.Battle),next,"wrong")));
        bool called=false;var wrong=RosterDecisionBattle.Execute<int>(state,B,c=>{called=true;return c.Reject<int>();});Assert.False(called);Atomic(state,wrong);
        var near=WithRng(state,new(57,ulong.MaxValue-7));var rng=new RandomStateGenerator().Next(near.Battle.TurnBattle.Turns.RngState).State;
        var late=RosterDecisionBattle.Execute(near,A,c=>c.PrepareTargetedAbility(new(A,RosterThrowStoneTests.Stone,B),RosterThrowStoneTests.Ability(c.Battle),RosterThrowStoneTests.Heights(c.Battle),rng,1));
        Atomic(near,late);Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted,late.Transition.AttackRejection);
    }

    [Fact]
    public void Real_ACT_and_Wait_delegate_existing_rules_without_adoption_revision_or_clock_work()
    {
        var state=Ready();var direct=RosterFacingTurnBattle.Attack(state,new(A,B),new(Sword,2));
        var result=RosterDecisionBattle.Execute(state,A,c=>c.PrepareAttack(new(A,B),new(Sword,2),c.RngState,"attack"));Assert.True(result.Accepted);
        Assert.Equal(Snapshot(direct.State.Battle),Snapshot(result.State.Battle));Assert.Equal(SerializeEvents(direct.Events),SerializeEvents(result.Events));
        var waited=RosterDecisionBattle.Execute(state,A,c=>c.PrepareWait(BattleFacing.North,c.RngState,7));Assert.True(waited.Accepted);Assert.Equal(55,waited.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
        Assert.Equal(BattleFacing.North,waited.State.Battle.Combatants.Single(u=>u.UnitId==A).Facing);Assert.Equal(state.Battle.TurnBattle.Turns.Ticks,waited.State.Battle.TurnBattle.Turns.Ticks);
    }

    [Fact]
    public void Post_ACT_path_and_actual_receipt_facing_commit_together_or_keep_prior_ACT()
    {
        var state=Ready();var act=RosterFacingTurnBattle.UseImmediateAbility(state,new(A,Self),SelfProfile(state.Battle));Assert.True(act.Accepted);
        var moved=RosterDecisionBattle.Execute(act.State,A,c=>c.PrepareTurnEndingMove(new(A,new(0,1,0)),BattleFacing.South,c.RngState,1));Assert.True(moved.Accepted);Assert.NotNull(moved.Path);
        Assert.Null(moved.State.PendingFacing);Assert.Equal(act.State.Revision+2,moved.State.Revision);Assert.Equal(6,moved.State.Battle.Combatants.Single(u=>u.UnitId==A).PhysicalAttack);
        var near=new RosterFacingTurnState(act.State.Battle,act.State.Profile,ulong.MaxValue-1,null,act.State.Lineage);
        var fail=RosterDecisionBattle.Execute(near,A,c=>c.PrepareTurnEndingMove(new(A,new(0,1,0)),null,c.RngState,1));Atomic(near,fail);
        Assert.Equal(FacingTurnRejection.RevisionExhausted,fail.Transition.FacingRejection);Assert.Equal(new TilePosition(1,1,0),Position(fail.State.Battle,A));Assert.Equal(6,fail.State.Battle.Combatants.Single(u=>u.UnitId==A).PhysicalAttack);
        Atomic(state,RosterDecisionBattle.Execute(state,A,c=>c.PrepareTurnEndingMove(new(A,new(0,1,0)),null,c.RngState,1)));
    }

    internal static void Atomic<T>(RosterFacingTurnState state,RosterDecisionResult<T> result)
    {Assert.False(result.Accepted);Assert.Same(state,result.State);Assert.Empty(result.Events);Assert.Null(result.Path);Assert.Equal(default(T),result.Diagnostic);}
}
