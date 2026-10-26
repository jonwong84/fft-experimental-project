using System.Text.Json;
using Zodiac.Content.Saves;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests.Saves;

public sealed class SwordFacingSaveTests
{
    private static readonly BattleUnitId A=new("sample:unit/facing-a"),B=new("sample:unit/facing-b");
    private static readonly AssetId Weapon=new("sample:item/facing-sword");
    private static readonly TwoUnitBattleCommand[] Commands=[new TwoUnitBattleCommand.Advance(),
        new TwoUnitBattleCommand.Attack(A,B),new TwoUnitBattleCommand.Wait(A),
        new TwoUnitBattleCommand.Advance(),new TwoUnitBattleCommand.Attack(B,A)];

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Every_reachable_misaligned_facing_boundary_restores_full_state_and_corrected_continuation(int boundary)
    {
        var profile=Profile(); var source=profile.Definition.Start();
        for(int i=0;i<boundary;i++) source=Apply(source,Commands[i]).State;
        string json=TwoUnitBattleSave.ToJson(profile,source);
        var selected=Profile(); var loaded=TwoUnitBattleSave.ReadJson(selected,json);
        Assert.Equal(2,loaded.SourceSchemaVersion); Assert.False(loaded.WasMigrated);
        var resumed=loaded.State;
        Assert.Equal(Snapshot(source),Snapshot(resumed)); Assert.Equal(json,TwoUnitBattleSave.ToJson(selected,resumed));
        Assert.All(resumed.Battle.Combatants,unit=>Assert.Equal(BattleFacing.West,unit.Facing));
        Assert.DoesNotContain("attack_facing",json); Assert.DoesNotContain("savedFacing",json);
        for(int i=boundary;i<Commands.Length;i++)
        {
            var expected=Apply(source,Commands[i]); var actual=Apply(resumed,Commands[i]);
            Assert.Equal(expected.Events.ToArray(),actual.Events.ToArray()); Assert.Equal(Snapshot(expected.State),Snapshot(actual.State));
            source=expected.State; resumed=actual.State;
        }
        Assert.Equal(new[]{25,25},resumed.Battle.Combatants.Select(unit=>unit.Hp).ToArray());
        Assert.Equal(new FftRandomState(2937909499,14),resumed.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(resumed.Battle.TurnBattle.Turns.RngState,resumed.Battle.TurnBattle.Battle.RngState);
        Assert.Equal(1ul,resumed.Battle.TurnBattle.Turns.Ticks);
        Assert.Equal(20,resumed.Battle.TurnBattle.Turns.Units.Single(unit=>unit.Id==A).Ct);
    }

    [Fact]
    public void Save_restores_authoritative_facing_instead_of_inferring_attack_history()
    {
        var profile=Profile(); var state=Apply(profile.Definition.Start(),Commands[0]).State;
        state=Apply(state,Commands[1]).State;
        var units=state.Battle.Combatants.Select(unit=>new AttackUnitState(unit.UnitId,unit.Hp,unit.MaxHp,unit.Mp,unit.MaxMp,
            unit.PhysicalAttack,unit.ClassPhysicalEvade,unit.RightHandEvade,unit.LeftHandEvade,unit.AccessoryEvade,unit.Brave,
            unit.ZodiacSign,unit.UnitId==A?BattleFacing.East:unit.Facing,unit.Status,unit.KoCount,unit.RightHandWeaponId));
        state=profile.Definition.Restore(new(state.Battle.TurnBattle,units));
        string json=TwoUnitBattleSave.ToJson(profile,state);
        var fresh=Profile(); var restored=TwoUnitBattleSave.ReadJson(fresh,json).State;
        Assert.Equal(BattleFacing.East,restored.Battle.Combatants.Single(unit=>unit.UnitId==A).Facing);
        restored=Apply(restored,Commands[2]).State;restored=Apply(restored,Commands[3]).State;
        var attack=Apply(restored,Commands[4]);
        Assert.False(Assert.Single(attack.Events.OfType<TwoUnitBattleTransitionEvent>().Select(ev=>ev.Event).OfType<AttackResolvedEvent>()).Hit);
        Assert.Equal(50,attack.State.Battle.Combatants.Single(unit=>unit.UnitId==A).Hp);
        Assert.Equal(new FftRandomState(836760821,12),attack.State.Battle.TurnBattle.Turns.RngState);
    }

    private static TwoUnitBattleResult Apply(TwoUnitBattleState state,TwoUnitBattleCommand command)
    { var result=TwoUnitBattleScenario.Apply(state,command);Assert.True(result.Accepted);return result; }
    private static string Snapshot(TwoUnitBattleState state)=>JsonSerializer.Serialize(new
    {
        state.Outcome,state.WinnerId,state.Battle.Combatants,state.Battle.TurnBattle.Turns,
        units=state.Battle.TurnBattle.Battle.Units.Select(unit=>new{unit.Id,unit.Position,unit.HasMoved}),state.Battle.TurnBattle.Battle.RngState,
    });
    private static TwoUnitBattleSaveProfile Profile()
    {
        var ground=new TerrainSurfaceId("sample:terrain/facing-ground");
        var map=new BattleMapDefinition(2,1,1,[new(ground,10),new(ground,10)]);
        var movement=new UnitMovementProfile(1,2,6,UnitMovementMode.Normal,UnitMovementTraits.None,[new(ground,new TerrainMovementCost(1))]);
        var rng=FftBiosRandom.Seed(1);
        var battle=new BattleState(map,[new(A,new(0,0,0),movement),new(B,new(1,0,0),movement)],rng);
        var turns=new TurnCycleState([new(A,10,90,0),new(B,10,90,1)],rng);
        var snapshot=new AttackBattleSnapshot(new(battle,turns),[Unit(A,100),Unit(B,0)]);
        return new("sample:profile/sword-facing",1,1,new(new("sample:scenario/sword-facing"),A,B,snapshot,[new(Weapon,5)]));
        static AttackUnitState Unit(BattleUnitId id,int evade)=>new(id,50,50,0,0,5,evade,0,0,0,70,0,BattleFacing.West,AttackUnitStatus.None,0,Weapon);
    }
}
