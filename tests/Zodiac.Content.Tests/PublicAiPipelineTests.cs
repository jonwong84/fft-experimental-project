using System.Collections.Immutable;
using Xunit;
using Zodiac.Framework;
using Zodiac.Framework.Ai;
using Zodiac.Framework.Random;

namespace Zodiac.Content.Tests;

/// <summary>Exercises the supported public AI path from an assembly without AI's internal test access.</summary>
public sealed class PublicAiPipelineTests
{
    [Fact]
    public void Public_decision_command_and_turn_share_configuration_and_preserve_actual_authority()
    {
        var actor = new BattleUnitId("actor"); var enemy = new BattleUnitId("enemy"); var ally = new BattleUnitId("ally");
        var ground = new TerrainSurfaceId("test:ground"); var sword = new BoundedSwordWeapon(new("test:sword"), 2);
        var ids = new[] { actor, enemy, ally }; var positions = new[] { new TilePosition(1,1,0), new(2,1,0), new(1,2,0) };
        var movement = new UnitMovementProfile(1,0,6,UnitMovementMode.Normal,UnitMovementTraits.None,[KeyValuePair.Create(ground,new TerrainMovementCost(1))]);
        var map = new BattleMapDefinition(8,4,1,Enumerable.Repeat(new BattleTile(ground,10),32).ToArray());
        var rng = new RandomState(unchecked((1u - 12345u) * 4005161829u), 6);
        var bodies = ids.Select((id,i) => new BattleUnitState(id, positions[i], movement));
        var turns = ids.Select((id,i) => new TurnUnitState(id,5,i == 0 ? 110 : i == 1 ? 90 : 0,i));
        var combat = ids.Select(id => new AttackUnitState(id,50,50,0,0,5,0,0,0,0,70,0,
            id == enemy ? BattleFacing.West : BattleFacing.South,AttackUnitStatus.None,0,id == actor ? sword.Id : null));
        var roster = new FlatRosterBattleProfile(ground,ids.Select((id,i) => new FlatRosterUnit(id,i == 1 ? 0 : 1,i)));
        var entered = RosterFacingTurnBattle.Advance(RosterFacingTurnBattle.Begin(new(new(new(map,bodies,rng),new(turns,rng)),combat),roster));
        Assert.True(entered.Accepted); var ready = entered.State;
        var lists = AiHumanAbilityLists.Build(ids.Select(id => new AiHumanUnitAbilitySource(id,new(new("test:skill"),[]))),new(true,true));
        var configuration = new AiDecisionConfiguration(new(roster,ids.Select(id => new AiUnitBaseline(id,255)),true),
            new(true,true,true,true),lists,new(true,true),new(true,true,true,true,true),
            new(Weapon: sword,OrdinaryAttackTilePolicy: true,NoDefendSupport: true,FreshOrdinarySelectionDefaults: true));
        var request = new RosterAiRefreshRequest(actor,ids.Select(id => new AiUnitPolicy(id,false,false)).ToImmutableArray());
        var decision = AiDecisionMaker.Decide(ready.Battle,request,configuration,ready.Battle.TurnBattle.Turns.RngState);
        Assert.True(decision.Accepted); Assert.Equal(AiHumanAbilityKind.Attack,decision.Outcome!.Plan.Action!.Kind);
        Assert.Equal(50,ready.Battle.Combatants.Single(u => u.UnitId == enemy).Hp);
        var dispatch = new AiDispatchProfile(true,true,true,true);
        var command = AiCommandExecutor.Execute(ready,request,configuration,dispatch,BattleFacing.North);
        Assert.True(command.Accepted); Assert.Equal(AiCommandKind.Attack,command.Outcome!.Kind);
        Assert.Equal(decision.RngState,command.Outcome.DecisionRng);
        Assert.True(command.State.Battle.TurnBattle.Turns.RngState.DrawCount > decision.RngState.DrawCount);
        Assert.True(command.State.Battle.Combatants.Single(u => u.UnitId == enemy).Hp < 50);
        var turn = AiTurnController.Execute(ready,request,new(configuration,dispatch,BattleFacing.North));
        Assert.True(turn.Completed); Assert.Null(turn.Recovery); Assert.Null(turn.State.PendingFacing);
        Assert.Null(turn.State.Battle.TurnBattle.Turns.ActiveUnitId);
        Assert.Equal(ready.Battle.TurnBattle.Turns.Ticks,turn.State.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(BattleFacing.North,turn.State.Battle.Combatants.Single(u => u.UnitId == actor).Facing);
        Assert.Equal(new RandomState(1,7),ready.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(50,ready.Battle.Combatants.Single(u => u.UnitId == enemy).Hp);
    }
}
