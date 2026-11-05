using System.Text.Json;
using Xunit;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterImmediateAbilityTests
{
    private static readonly AssetId Self = new("test:roster-self");
    private static ImmediateAbilityBattleProfile Ability(RosterFacingTurnState s,bool granted=true) => new([new(Self)],
        s.Battle.Combatants.Select(u=>new ImmediateAbilityUnitGrant(u.UnitId,u.UnitId==A && granted ? [Self] : [],0)));

    [Fact]
    public void Eight_actual_source_vectors_preserve_peers_rng_and_choice_receipt_settlement()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-execution-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var ready=Ready();var old=ready.Battle.TurnBattle;bool moved=d.GetProperty("moved").GetBoolean();
            var bodies=old.Battle.Units.Select(u=>new BattleUnitState(u.Id,u.Position,u.MovementProfile,u.Id==A && moved));
            var turns=old.Turns.Units.Select(u=>u.Id==A ? new TurnUnitState(u.Id,u.Speed,d.GetProperty("ct").GetInt32(),u.TieKey,true,moved,false) : u);
            var battle=new AttackBattleSnapshot(new(new(old.Battle.Map,bodies,old.Battle.RngState),new(turns,old.Turns.RngState,old.Turns.Ticks,old.Turns.Phase)),
                ready.Battle.Combatants.Select(u=>u.UnitId==A ? u.WithPhysicalAttack(d.GetProperty("pa").GetInt32()) : u));
            var state=new RosterFacingTurnState(battle,ready.Profile,ready.Revision,null,ready.Lineage);var before=Snapshot(state.Battle);
            var result=RosterFacingTurnBattle.UseImmediateAbility(state,new(A,Self),Ability(state));Assert.True(result.Accepted);Assert.Equal(state.Revision+1,result.State.Revision);
            Assert.Equal(e.GetProperty("pa").GetInt32(),result.State.Battle.Combatants.Single(u=>u.UnitId==A).PhysicalAttack);
            Assert.Equal(e.GetProperty("actCt").GetInt32(),result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
            Assert.True(result.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).ActionTaken);
            Assert.Equal(e.GetProperty("pendingAfterAct").GetBoolean(),result.State.PendingFacing is not null);
            Assert.Equal(e.GetProperty("paChanged").GetBoolean(),result.Events.OfType<AttackPhysicalAttackChangedEvent>().Any());
            Assert.Single(result.Events.OfType<ImmediateAbilityResolvedEvent>());
            foreach(var peer in state.Battle.Combatants.Where(u=>u.UnitId!=A))Assert.Equal(peer,result.State.Battle.Combatants.Single(u=>u.UnitId==peer.UnitId));
            Assert.Equal(state.Battle.TurnBattle.Turns.RngState,result.State.Battle.TurnBattle.Turns.RngState);
            var finished=moved ? RosterFacingTurnBattle.FinishFacing(result.State,A,result.State.PendingFacing) : RosterFacingTurnBattle.WaitWithFacing(result.State,A);
            Assert.True(finished.Accepted);Assert.Equal(e.GetProperty("endCt").GetInt32(),finished.State.Battle.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
            Assert.Null(finished.State.PendingFacing);Assert.Equal(before,Snapshot(state.Battle));count++;
        }
        Assert.Equal(8,count);
    }

    [Fact]
    public void Both_actual_orders_share_command_kernel_and_bind_foreign_receipts()
    {
        foreach(bool first in new[]{true,false})
        {
            var ready=Ready(first);var initial=first ? RosterFacingTurnBattle.Move(ready,new(A,new(1,1,0)))
                : RosterFacingTurnBattle.UseImmediateAbility(ready,new(A,Self),Ability(ready));Assert.True(initial.Accepted);Assert.Null(initial.State.PendingFacing);
            var final=first ? RosterFacingTurnBattle.UseImmediateAbility(initial.State,new(A,Self),Ability(initial.State))
                : RosterFacingTurnBattle.Move(initial.State,new(A,new(0,1,0)));Assert.True(final.Accepted);Assert.NotNull(final.State.PendingFacing);
            Assert.Equal(6,final.State.Battle.Combatants.Single(u=>u.UnitId==A).PhysicalAttack);Assert.Equal(ready.Battle.TurnBattle.Turns.RngState,final.State.Battle.TurnBattle.Turns.RngState);
            var otherMove=RosterFacingTurnBattle.Move(Ready(true),new(A,new(1,1,0)));
            var other=RosterFacingTurnBattle.UseImmediateAbility(otherMove.State,new(A,Self),Ability(otherMove.State));
            var wrong=RosterFacingTurnBattle.FinishFacing(final.State,A,other.State.PendingFacing);
            Assert.False(wrong.Accepted);Assert.Same(final.State,wrong.State);Assert.Empty(wrong.Events);
            var settled=RosterFacingTurnBattle.FinishFacing(final.State,A,final.State.PendingFacing,BattleFacing.South);Assert.True(settled.Accepted);
            Assert.Equal(BattleFacing.South,settled.State.Battle.Combatants.Single(u=>u.UnitId==A).Facing);
        }
    }

    [Fact]
    public void Rejected_grants_profiles_pending_and_revision_guards_leave_accepted_progress_intact()
    {
        var moved=RosterFacingTurnBattle.Move(Ready(true),new(A,new(1,1,0)));var state=moved.State;var before=Snapshot(state.Battle);
        var noGrant=RosterFacingTurnBattle.UseImmediateAbility(state,new(A,Self),Ability(state,false));Atomic(state,noGrant);Assert.Equal(AttackCommandRejection.AbilityNotGranted,noGrant.AttackRejection);
        Atomic(state,RosterFacingTurnBattle.UseImmediateAbility(state,new(A,Self),new([new(Self)],[new(A,[Self],0)])));
        var pending=RosterFacingTurnBattle.UseImmediateAbility(state,new(A,Self),Ability(state));Assert.True(pending.Accepted);Atomic(pending.State,RosterFacingTurnBattle.UseImmediateAbility(pending.State,new(A,Self),Ability(pending.State)));
        var exhausted=new RosterFacingTurnState(state.Battle,state.Profile,ulong.MaxValue,null,state.Lineage);var fail=RosterFacingTurnBattle.UseImmediateAbility(exhausted,new(A,Self),Ability(exhausted));Atomic(exhausted,fail);
        Assert.Equal(FacingTurnRejection.RevisionExhausted,fail.FacingRejection);Assert.Equal(new TilePosition(1,1,0),Position(noGrant.State.Battle,A));Assert.Equal(before,Snapshot(state.Battle));
    }

    [Fact]
    public void Roster_session_reuses_existing_actual_effect_without_collection_order_or_rng_differences()
    {
        var a=Ready();var b=Ready(reverse:true);var before=Snapshot(a.Battle);
        var direct=ImmediateAbilityBattleSimulator.Apply(a.Battle,new(A,Self),Ability(a));var x=RosterFacingTurnBattle.UseImmediateAbility(a,new(A,Self),Ability(a));
        var y=RosterFacingTurnBattle.UseImmediateAbility(b,new(A,Self),Ability(b));Assert.True(x.Accepted);Assert.True(y.Accepted);
        Assert.Equal(Snapshot(direct.State),Snapshot(x.State.Battle));Assert.Equal(Snapshot(x.State.Battle),Snapshot(y.State.Battle));
        Assert.Equal(SerializeEvents(direct.Events),SerializeEvents(x.Events));Assert.Equal(SerializeEvents(x.Events),SerializeEvents(y.Events));Assert.Equal(before,Snapshot(a.Battle));
    }

    private static void Atomic(RosterFacingTurnState s,RosterFacingResult r){Assert.False(r.Accepted);Assert.Same(s,r.State);Assert.Empty(r.Events);}
}
