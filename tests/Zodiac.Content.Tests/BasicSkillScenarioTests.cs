using System.Text.Json;
using Zodiac.Content.Samples;
using Zodiac.Content.Saves;
using Zodiac.Framework;
using Xunit;

namespace Zodiac.Content.Tests;

public sealed class BasicSkillScenarioTests
{
    private static BattleUnitId A=>SyntheticBasicSkillBattle.First;
    private static BattleUnitId B=>SyntheticBasicSkillBattle.Second;

    [Fact]
    public void Combined_journal_uses_boosted_PA_knockback_first_KO_and_all_terminal_guards()
    {
        var definition=SyntheticBasicSkillBattle.Create(); var state=definition.Start();
        string initial=Snapshot(state); int hits=0,pushes=0,rejections=0; bool terminal=false;
        foreach(var command in SyntheticBasicSkillBattle.Journal())
        {
            var before=state; var result=TwoUnitBattleScenario.Apply(state,command); state=result.State;
            if(!result.Accepted)
            {
                rejections++; Assert.Same(before,state); Assert.Empty(result.Events);
                if(terminal) Assert.Equal(TwoUnitBattleRejection.AlreadyCompleted,result.ScenarioRejection);
                continue;
            }
            var effects=result.Events.OfType<TwoUnitBattleTransitionEvent>().Select(ev=>ev.Event).ToArray();
            var stone=effects.OfType<ThrowStoneResolvedEvent>().SingleOrDefault();
            if(stone is not null)
            {
                hits++; Assert.True(stone.Hit); Assert.Contains(stone.Damage,new[]{6,12});
                Assert.Equal(before.Battle.TurnBattle.Turns.RngState.DrawCount+7,state.Battle.TurnBattle.Turns.RngState.DrawCount);
                Assert.Equal(6,state.Battle.Combatants[0].PhysicalAttack);
                Assert.Equal(BattleFacing.East,state.Battle.Combatants[0].Facing);
            }
            pushes+=effects.OfType<ThrowStoneKnockbackEvent>().Count();
            if(state.Outcome!=TwoUnitBattleOutcome.InProgress)
            {
                terminal=true; Assert.IsType<TwoUnitBattleCompletedEvent>(result.Events[^1]);
                Assert.Equal(A,state.WinnerId); Assert.Equal(0,state.Battle.Combatants[1].Hp);
            }
            Assert.Equal(state.Battle.TurnBattle.Turns.RngState,state.Battle.TurnBattle.Battle.RngState);
        }
        Assert.True(terminal); Assert.True(hits>1); Assert.True(pushes>0); Assert.Equal(8,rejections);
        Assert.Equal(initial,Snapshot(definition.Start()));
    }

    [Fact]
    public void Every_prefix_recomposes_definitions_and_replays_all_remaining_state_and_events()
    {
        var journal=SyntheticBasicSkillBattle.Journal();
        for(int prefix=0;prefix<=journal.Length;prefix++)
        {
            var left=SyntheticBasicSkillBattle.Create().Start(); var right=SyntheticBasicSkillBattle.Create().Start();
            foreach(var command in journal.Take(prefix))
            { left=TwoUnitBattleScenario.Apply(left,command).State; right=TwoUnitBattleScenario.Apply(right,command).State; }
            Assert.Equal(Snapshot(left),Snapshot(right));
            foreach(var command in journal.Skip(prefix))
            {
                var a=TwoUnitBattleScenario.Apply(left,command); var b=TwoUnitBattleScenario.Apply(right,command);
                Assert.Equal(a.Accepted,b.Accepted); Assert.Equal(a.ScenarioRejection,b.ScenarioRejection);
                Assert.Equal(a.AttackRejection,b.AttackRejection); Assert.Equal(a.TurnRejection,b.TurnRejection);
                Assert.Equal(a.MovementRejection,b.MovementRejection); Assert.Equal(a.Events.ToArray(),b.Events.ToArray());
                Assert.Equal(Snapshot(a.State),Snapshot(b.State)); left=a.State; right=b.State;
            }
        }
    }

    [Fact]
    public void Metadata_membership_does_not_grant_effects_and_old_profiles_do_not_admit_new_command()
    {
        var metadata=SyntheticBasicSkillBattle.Metadata();
        var definition=SyntheticBasicSkillBattle.Create(metadata,SyntheticBasicSkillBattle.SelfEffects(metadata),
            SyntheticBasicSkillBattle.TargetedEffects(metadata),grantTargeted:false);
        var state=TwoUnitBattleScenario.Apply(definition.Start(),new TwoUnitBattleCommand.Advance()).State;
        var result=TwoUnitBattleScenario.Apply(state,new TwoUnitBattleCommand.UseTargetedAbility(A,new(SyntheticBasicSkillBattle.ThrowStone.Value),B));
        Assert.Equal(AttackCommandRejection.AbilityNotGranted,result.AttackRejection); Assert.Same(state,result.State);
        foreach(var old in new[]{SyntheticTwoUnitBattle.Create(),SyntheticAccumulateBattle.Create()})
        {
            var start=old.Start();
            var unsupported=TwoUnitBattleScenario.Apply(start,new TwoUnitBattleCommand.UseTargetedAbility(A,new(SyntheticBasicSkillBattle.ThrowStone.Value),B));
            Assert.Equal(TwoUnitBattleRejection.UnsupportedCommand,unsupported.ScenarioRejection); Assert.Same(start,unsupported.State);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void Guarded_sword_and_reachable_PA_envelope_are_not_broadened(bool overflow)
    {
        if(overflow) { Assert.Throws<NotSupportedException>(()=>SyntheticBasicSkillBattle.Create(swordPower:167)); return; }
        var state=TwoUnitBattleScenario.Apply(SyntheticBasicSkillBattle.Create().Start(),new TwoUnitBattleCommand.Advance()).State;
        state=TwoUnitBattleScenario.Apply(state,new TwoUnitBattleCommand.Move(A,new(2,2,0))).State;
        var attack=TwoUnitBattleScenario.Apply(state,new TwoUnitBattleCommand.Attack(A,B));
        Assert.Equal(AttackCommandRejection.UnsupportedSetup,attack.AttackRejection); Assert.Same(state,attack.State); Assert.Empty(attack.Events);
    }

    [Fact]
    public void Self_and_targeted_profiles_cannot_give_one_ID_conflicting_meanings()
    {
        var source=SyntheticBasicSkillBattle.Create(); var id=new AssetId(SyntheticBasicSkillBattle.ThrowStone.Value);
        var self=new ImmediateAbilityBattleProfile([new(id)],[new(A,[id],0),new(B,[],0)]);
        Assert.Throws<ArgumentException>(()=>new TwoUnitBattleDefinition(source.Id,A,B,source.InitialBattle,source.Weapons.Values,self,source.TargetedAbilities));
    }

    [Theory] [InlineData(false,0)] [InlineData(false,2)] [InlineData(false,1000)] [InlineData(true,0)]
    public void Initial_boosted_terminal_and_targeted_only_saves_fail_before_file_IO(bool targetedOnly,int prefix)
    {
        var definition=SyntheticBasicSkillBattle.Create();
        if(targetedOnly) definition=new(definition.Id,A,B,definition.InitialBattle,definition.Weapons.Values,null,definition.TargetedAbilities);
        var profile=new TwoUnitBattleSaveProfile("sample:save/unsupported-basic",1,1,definition);
        var state=definition.Start();
        if(!targetedOnly) foreach(var command in SyntheticBasicSkillBattle.Journal().Take(prefix)) state=TwoUnitBattleScenario.Apply(state,command).State;
        string directory=Path.Combine(Path.GetTempPath(),"fft-basic-save-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"keep.json"); File.WriteAllText(path,"preserve-existing");
        try
        {
            Assert.Throws<NotSupportedException>(()=>definition.Restore(state.Battle));
            Assert.Throws<BattleSaveValidationException>(()=>TwoUnitBattleSave.ToJson(profile,state));
            Assert.Throws<BattleSaveValidationException>(()=>TwoUnitBattleSave.ReadJson(profile,"not-json"));
            Assert.Throws<BattleSaveValidationException>(()=>TwoUnitBattleSave.Load(profile,Path.Combine(directory,"missing")));
            Assert.Throws<BattleSaveValidationException>(()=>TwoUnitBattleSave.Save(profile,state,path));
            Assert.Equal("preserve-existing",File.ReadAllText(path)); Assert.Single(Directory.GetFiles(directory));
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    private static string Snapshot(TwoUnitBattleState state)=>JsonSerializer.Serialize(new
    {state.Outcome,state.WinnerId,state.Battle.Combatants,state.Battle.TurnBattle.Turns,
     units=state.Battle.TurnBattle.Battle.Units.Select(unit=>new{unit.Id,unit.Position,unit.HasMoved}),state.Battle.TurnBattle.Battle.RngState});
}
