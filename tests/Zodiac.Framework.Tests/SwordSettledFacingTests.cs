using System.Text.Json;
using Zodiac.Framework.Tests.Fixtures;
using Xunit;

namespace Zodiac.Framework.Tests;

public sealed class SwordSettledFacingTests
{
    private static readonly BattleUnitId A = new("sample:unit/attacker"), B = new("sample:unit/defender");
    private static readonly AssetId Weapon = new("sample:item/plain-sword");

    public static IEnumerable<object[]> CardinalVectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sword-settled-facing-contract-v1.json")));
        foreach (var row in document.RootElement.GetProperty("cardinalVectors").EnumerateArray())
        {
            var target = row.GetProperty("targetPosition"); var expected = row.GetProperty("expected");
            yield return [row.GetProperty("id").GetString()!, target[0].GetInt32(), target[1].GetInt32(),
                Enum.Parse<BattleFacing>(row.GetProperty("actorFacing").GetString()!),
                Enum.Parse<BattleFacing>(row.GetProperty("targetFacing").GetString()!), row.GetProperty("targetClassEvade").GetInt32(),
                expected.GetProperty("hit").GetBoolean(), expected.GetProperty("damage").GetInt32(),
                expected.GetProperty("rngWord").GetUInt32(), expected.GetProperty("draws").GetUInt64()];
        }
    }

    [Theory] [MemberData(nameof(CardinalVectors))]
    public void Source_contract_cardinals_preserve_both_facings_with_exact_damage_and_RNG(string id, int x, int y,
        BattleFacing incoming, BattleFacing targetFacing, int evade, bool hit, int damage, uint word, ulong draws)
    {
        Assert.NotEmpty(id);
        var state = State(incoming: incoming, targetFacing: targetFacing, evade: evade, targetPosition: new(x,y,0));
        string before = Snapshot(state);
        var actual = Attack(state, A, B);
        Assert.True(actual.Accepted); Assert.NotSame(state, actual.State);
        Assert.Equal(incoming, Unit(actual.State,A).Facing); Assert.Equal(targetFacing, Unit(actual.State,B).Facing);
        Assert.Equal(50-damage, Unit(actual.State,B).Hp);
        Assert.Equal(new AttackResolvedEvent(A,B,hit,false,damage), Assert.Single(actual.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(new FftRandomState(word,draws), actual.State.TurnBattle.Turns.RngState);
        Assert.Equal(actual.State.TurnBattle.Turns.RngState, actual.State.TurnBattle.Battle.RngState);
        Assert.True(actual.State.TurnBattle.Turns.Units.Single(u=>u.Id==A).ActionTaken);
        Assert.False(actual.State.TurnBattle.Turns.Units.Single(u=>u.Id==A).MovementTaken);
        Assert.Equal(before, Snapshot(state));
    }

    [Theory]
    [InlineData(1u,5,25,0,25,false,AttackUnitStatus.Dead,6ul,2516284547u)]
    [InlineData(57u,5,50,5,45,true,AttackUnitStatus.Critical,7ul,3448561432u)]
    [InlineData(1u,0,50,50,0,false,AttackUnitStatus.None,6ul,2516284547u)]
    [InlineData(1u,64,50,0,999,false,AttackUnitStatus.Dead,6ul,2516284547u)]
    public void KO_critical_zero_and_cap_preserve_actor_and_target_facing(uint seed, int power, int hp, int finalHp,
        int damage, bool critical, AttackUnitStatus status, ulong draws, uint word)
    {
        var state=State(seed:seed,targetHp:hp,pa:power==64?255:5);
        var result=Attack(state,A,B,power);
        Assert.True(result.Accepted);
        Assert.Equal(BattleFacing.South,Unit(result.State,A).Facing); Assert.Equal(BattleFacing.West,Unit(result.State,B).Facing);
        Assert.Equal(finalHp,Unit(result.State,B).Hp); Assert.Equal(status,Unit(result.State,B).Status);
        Assert.Equal(new AttackResolvedEvent(A,B,true,critical,damage),Assert.Single(result.Events.OfType<AttackResolvedEvent>()));
        Assert.Equal(new FftRandomState(word,draws),result.State.TurnBattle.Turns.RngState);
        if(status==AttackUnitStatus.Dead) Assert.Single(result.Events.OfType<AttackKnockedOutEvent>());
        if(damage==0) Assert.Empty(result.Events.OfType<AttackHpChangedEvent>());
    }

    public static IEnumerable<object[]> DrawBudgets()
    {
        foreach(var (seed,required) in new[]{(19u,4),(1u,6),(57u,7)})
            for(int remaining=0;remaining<=required;remaining++) yield return [seed,required,remaining];
    }
    [Theory] [MemberData(nameof(DrawBudgets))]
    public void Every_draw_exhaustion_boundary_preserves_state_and_facing(uint seed,int required,int remaining)
    {
        var state=State(seed:seed,count:ulong.MaxValue-(ulong)remaining);
        string before=Snapshot(state); var result=Attack(state,A,B);
        Assert.Equal(remaining==required,result.Accepted);
        if(!result.Accepted)
        { Assert.Same(state,result.State); Assert.Empty(result.Events); Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted,result.AttackRejection); }
        else
        { Assert.Equal(ulong.MaxValue,result.State.TurnBattle.Turns.RngState.DrawCount); Assert.Equal(BattleFacing.South,Unit(result.State,A).Facing); }
        Assert.Equal(before,Snapshot(state));
    }

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
    private static AttackBattleResult Attack(AttackBattleSnapshot state,BattleUnitId actor,BattleUnitId target,int power=5)=>
        AttackBattleSimulator.Attack(state,new(actor,target),new(Weapon,power));
    private static AttackBattleSnapshot State(uint seed=1,ulong count=0,BattleFacing incoming=BattleFacing.South,
        BattleFacing targetFacing=BattleFacing.West,int evade=10,int actorEvade=10,int targetHp=50,int pa=5,
        TilePosition? actorPosition=null,TilePosition? targetPosition=null,bool actorSecond=false,bool reverse=false,bool twoTiles=false)
    {
        var actor=actorSecond?B:A; var target=actorSecond?A:B;
        var map=new BattleMapDefinition(twoTiles?2:3,twoTiles?1:3,1,Enumerable.Repeat(new BattleTile(10),twoTiles?2:9).ToArray());
        var rng=new FftRandomState(seed,count);
        var bodies=new[]{new BattleUnitState(actor,actorPosition??(twoTiles?new(0,0,0):new(1,1,0)),1),new BattleUnitState(target,targetPosition??(twoTiles?new(1,0,0):new(2,1,0)),1)};
        var turns=new TurnCycleState([new(actor,10,0,0,true),new(target,10,100,1)],rng,phase:TurnCyclePhase.UnitTurn);
        var combatants=new[]{new AttackUnitState(actor,50,50,0,0,pa,actorEvade,0,0,0,70,0,incoming,AttackUnitStatus.None,0,Weapon),
            new AttackUnitState(target,targetHp,50,0,0,5,evade,0,0,0,70,0,targetFacing,AttackUnitStatus.None,0,Weapon)};
        return new(new(new(map,bodies,rng),turns),reverse?combatants.Reverse():combatants);
    }
}
