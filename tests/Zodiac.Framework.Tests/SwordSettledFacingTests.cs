using Zodiac.Framework.Tests.Fixtures;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class SwordSettledFacingTests
{
    private static readonly BattleUnitId A = new("sample:unit/attacker"), B = new("sample:unit/defender");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");

    [Theory] [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void Both_actor_IDs_and_combatant_orders_preserve_incoming_facing_without_mutating_probes(bool secondActor,bool reverse)
    {
        var state=State(actorSecond:secondActor,reverse:reverse,incoming:BattleFacing.North);
        var actor=secondActor?B:A; var target=secondActor?A:B;
        string before=Snapshot(state); var first=Attack(state,actor,target);
        for(int i=0;i<5;i++)
        {
            var again=Attack(state,actor,target);
            Assert.True(again.Accepted); Assert.Equal(Snapshot(first.State),Snapshot(again.State));
            Assert.Equal(first.Events.ToArray(),again.Events.ToArray()); Assert.Equal(before,Snapshot(state));
        }
        Assert.Equal(BattleFacing.North,Unit(first.State,actor).Facing);
    }

    [Fact]
    public void Both_action_orders_and_Wait_preserve_attack_entry_facing_and_existing_event_order()
    {
        var state=State(actorPosition:new(1,0,0));
        var moved=AttackBattleSimulator.Move(state,new(A,new(1,1,0))); Assert.True(moved.Accepted);
        var afterMove=Attack(moved.State,A,B); Assert.True(afterMove.Accepted);
        Assert.Equal(BattleFacing.South,Unit(afterMove.State,A).Facing);
        Assert.Null(afterMove.State.TurnBattle.Turns.ActiveUnitId);
        Assert.IsType<TurnEndedEvent>(Assert.IsType<TurnBattleClockEvent>(Assert.IsType<AttackTurnBattleEvent>(afterMove.Events[1]).Event).Event);
        Assert.IsType<AttackResolvedEvent>(afterMove.Events[2]);
        var acted=Attack(State(),A,B);
        var moveAfter=AttackBattleSimulator.Move(acted.State,new(A,new(1,0,0))); Assert.True(moveAfter.Accepted);
        Assert.Equal(BattleFacing.South,Unit(moveAfter.State,A).Facing); Assert.Null(moveAfter.State.TurnBattle.Turns.ActiveUnitId);
        var waited=AttackBattleSimulator.Wait(acted.State,A); Assert.True(waited.Accepted);
        Assert.Equal(20,waited.State.TurnBattle.Turns.Units.Single(u=>u.Id==A).Ct);
        Assert.Equal(BattleFacing.South,Unit(waited.State,A).Facing);
        Assert.Equal(acted.State.TurnBattle.Turns.RngState,waited.State.TurnBattle.Turns.RngState);
    }

    [Fact]
    public void Restored_facing_changes_subsequent_evasion_and_every_journal_prefix_replays_exactly()
    {
        TwoUnitBattleCommand[] commands=[new TwoUnitBattleCommand.Attack(A,B),new TwoUnitBattleCommand.Advance(),
            new TwoUnitBattleCommand.Attack(A,B),new TwoUnitBattleCommand.Wait(A),new TwoUnitBattleCommand.Advance(),
            new TwoUnitBattleCommand.Attack(B,A),new TwoUnitBattleCommand.Attack(A,B)];
        var start=State(twoTiles:true,incoming:BattleFacing.West,actorEvade:100,evade:0);
        var state=start; var results=new List<AttackBattleResult>();
        foreach(var command in commands) { var result=Apply(state,command); results.Add(result); state=result.State; }
        Assert.Equal(4,results.Count(r=>r.Accepted)); Assert.Equal(3,results.Count(r=>!r.Accepted));
        Assert.Equal(25,Unit(state,A).Hp); Assert.Equal(25,Unit(state,B).Hp);
        Assert.Equal(BattleFacing.West,Unit(state,A).Facing); Assert.Equal(BattleFacing.West,Unit(state,B).Facing);
        Assert.Equal(new FftRandomState(595337866,13),state.TurnBattle.Turns.RngState);
        Assert.Equal(state.TurnBattle.Turns.RngState,state.TurnBattle.Battle.RngState);
        Assert.Equal(new FftRandomState(3655513600,7),results[4].State.TurnBattle.Turns.RngState);
        Assert.Equal(23010u,Assert.IsType<TurnReadyEvent>(Assert.IsType<TurnBattleClockEvent>(Assert.IsType<AttackTurnBattleEvent>(Assert.Single(results[4].Events)).Event).Event).UpkeepRoll);
        for(int prefix=0;prefix<=commands.Length;prefix++)
        {
            var replay=State(twoTiles:true,incoming:BattleFacing.West,actorEvade:100,evade:0);
            for(int i=0;i<prefix;i++) replay=Apply(replay,commands[i]).State;
            for(int i=prefix;i<commands.Length;i++)
            {
                var result=Apply(replay,commands[i]); Assert.Equal(results[i].Accepted,result.Accepted);
                Assert.Equal(results[i].AttackRejection,result.AttackRejection); Assert.Equal(results[i].TurnRejection,result.TurnRejection);
                Assert.Equal(results[i].MovementRejection,result.MovementRejection);
                Assert.Equal(results[i].Events.ToArray(),result.Events.ToArray()); Assert.Equal(Snapshot(results[i].State),Snapshot(result.State));
                replay=result.State;
            }
            Assert.Equal(Snapshot(state),Snapshot(replay));
        }
        Assert.Equal(new FftRandomState(1,0),start.TurnBattle.Turns.RngState);
    }

    private static AttackBattleResult Apply(AttackBattleSnapshot state,TwoUnitBattleCommand command)=>command switch
    {
        TwoUnitBattleCommand.Advance=>AttackBattleSimulator.Advance(state),
        TwoUnitBattleCommand.Wait wait=>AttackBattleSimulator.Wait(state,wait.UnitId),
        TwoUnitBattleCommand.Attack attack=>Attack(state,attack.ActorId,attack.TargetId),
        _=>throw new InvalidOperationException(),
    };
    private static AttackUnitState Unit(AttackBattleSnapshot state,BattleUnitId id)=>state.Combatants.Single(u=>u.UnitId==id);
    private static string Snapshot(AttackBattleSnapshot state)=>AttackFixtureRunner.Snapshot(state,"sample:map/arena").ToJsonString();
    private static AttackBattleResult Attack(AttackBattleSnapshot state,BattleUnitId actor,BattleUnitId target)=>
        AttackBattleSimulator.Attack(state,new(actor,target),new(Weapon,5));
    private static AttackBattleSnapshot State(BattleFacing incoming=BattleFacing.South,int evade=10,int actorEvade=10,
        TilePosition? actorPosition=null,bool actorSecond=false,bool reverse=false,bool twoTiles=false)
    {
        var actor=actorSecond?B:A; var target=actorSecond?A:B;
        var map=new BattleMapDefinition(twoTiles?2:3,twoTiles?1:3,1,Enumerable.Repeat(new BattleTile(10),twoTiles?2:9).ToArray());
        var rng=new FftRandomState(1,0);
        var bodies=new[]{new BattleUnitState(actor,actorPosition??(twoTiles?new(0,0,0):new(1,1,0)),1),new BattleUnitState(target,twoTiles?new(1,0,0):new(2,1,0),1)};
        var turns=new TurnCycleState([new(actor,10,0,0,true),new(target,10,100,1)],rng,phase:TurnCyclePhase.UnitTurn);
        var combatants=new[]{new AttackUnitState(actor,50,50,0,0,5,actorEvade,0,0,0,70,0,incoming,AttackUnitStatus.None,0,Weapon),
            new AttackUnitState(target,50,50,0,0,5,evade,0,0,0,70,0,BattleFacing.West,AttackUnitStatus.None,0,Weapon)};
        return new(new(new(map,bodies,rng),turns),reverse?combatants.Reverse():combatants);
    }
}
