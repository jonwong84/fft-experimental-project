using System.Text.Json;
using Xunit;
using Zodiac.Framework.CombatRules;
using Zodiac.Framework.Formulas;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

public sealed class PhysicalFormulaAssignmentTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","physical-formula-assignment-v2.json")));
    public static IEnumerable<object[]> Cases()
    {
        using var fixture=Fixture();return fixture.RootElement.GetProperty("cases").EnumerateArray().Select(r=>new object[]{r.GetProperty("id").GetString()!}).ToArray();
    }

    [Theory] [MemberData(nameof(Cases))]
    public void Source_vectors_preserve_selected_arithmetic_critical_evasion_modes_and_exact_rng(string id)
    {
        using var fixture=Fixture();var row=fixture.RootElement.GetProperty("cases").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id);
        var d=row.GetProperty("input");var e=row.GetProperty("expected");
        var formula=new AbilityFormula((AbilityFormulaId)d.GetProperty("formula").GetInt32(),new(d.GetProperty("x").GetInt32(),d.GetProperty("y").GetInt32()));
        var ability=new PhysicalAbilityDefinition(new("test:unchanged-identity"),1,d.GetProperty("evadeable").GetBoolean(),false,formula);
        var entry=new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());
        var result=PhysicalDamageRules.Calculate(ability,d.GetProperty("pa").GetInt32(),d.GetProperty("attackerBrave").GetInt32(),d.GetProperty("targetBrave").GetInt32(),
            d.GetProperty("classEvade").GetInt32(),1,0,BattleFacing.West,Enum.Parse<AbilityFormulaMode>(d.GetProperty("mode").GetString()!),entry);
        Assert.Equal(e.GetProperty("accepted").GetBoolean(),result.Accepted);
        Assert.Equal(new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64()),result.RngState);
        if(!result.Accepted){Assert.Null(result.Outcome);return;}
        var actual=result.Outcome!;Assert.Equal(e.GetProperty("hit").GetBoolean(),actual.Hit);
        Assert.Equal(e.GetProperty("accuracy").GetInt32(),actual.Accuracy);Assert.Equal(e.GetProperty("damage").GetInt32(),actual.Damage);
        Assert.Equal(e.GetProperty("critical").GetBoolean(),actual.Critical);Assert.Equal(e.GetProperty("attempted").GetBoolean(),actual.KnockbackAttempted);
        Assert.Same(formula,ability.Formula);
    }

    private static readonly AssetId Dash=new("test:dash");
    private static DashAbilityBattleProfile Profile(AttackBattleSnapshot state)=>DashAbilityBattleProfile.FromDefinitions(Ground,
        [new(Dash,1,false,false,new(AbilityFormulaId.RandomPaPlusHalfPa,new(9,0)))],
        state.Combatants.Select(u=>new TargetedAbilityUnitGrant(u.UnitId,[Dash],0)));

    [Theory] [InlineData(0u,30,3,true)] [InlineData(1u,14,2,false)]
    public void Same_Dash_identity_executes_assigned_formula_with_its_own_critical_and_push_path(uint word,int damage,ulong count,bool pushed)
    {
        var s=Make([new("a",new(1,1,0)),new("b",new(2,1,0),1),new("c",new(4,3,0))],seed:word,evade:100);
        var before=Snapshot(s.State);var profile=Profile(s.State);
        var result=DashAbilityBattleSimulator.ApplyRoster(s.State,new(A,Dash,B),profile,s.Profile);
        Assert.True(result.Accepted);var hit=Assert.Single(result.Events.OfType<DashResolvedEvent>());
        Assert.Equal(Dash,hit.AbilityId);Assert.Equal(damage,hit.Damage);Assert.Equal(pushed,hit.KnockbackAttempted);
        Assert.Equal(50-damage,result.State.Combatants.Single(u=>u.UnitId==B).Hp);
        Assert.Equal(new TilePosition(pushed?3:2,1,0),Position(result.State,B));
        Assert.Equal(count,result.State.TurnBattle.Turns.RngState.DrawCount);Assert.Equal(before,Snapshot(s.State));
        Assert.Equal(Snapshot(result.State),Snapshot(DashAbilityBattleSimulator.ApplyRoster(s.State,new(A,Dash,B),profile,s.Profile).State));
    }

    [Fact]
    public void Critical_lethal_hit_keeps_draws_and_suppresses_displacement()
    {
        var s=Make([new("a",new(1,1,0)),new("b",new(2,1,0),1,30)],seed:0);
        var result=DashAbilityBattleSimulator.ApplyRoster(s.State,new(A,Dash,B),Profile(s.State),s.Profile);
        Assert.True(result.Accepted);Assert.Equal(0,result.State.Combatants.Single(u=>u.UnitId==B).Hp);
        Assert.Equal(1,result.State.Combatants.Single(u=>u.UnitId==B).KoCount);Assert.Equal(new TilePosition(2,1,0),Position(result.State,B));
        Assert.Equal(3UL,result.State.TurnBattle.Turns.RngState.DrawCount);Assert.Empty(result.Events.OfType<DashKnockbackEvent>());
    }

    [Fact]
    public void Frozen_Move_continuation_uses_same_assigned_formula_and_failed_formula_rolls_back_actual_work()
    {
        var ready=Ready(true);var profile=Profile(ready.Battle);
        var move=RosterDecisionBattle.Execute(ready,A,c=>c.PrepareMoveThenDashAbility(new(A,new(1,1,0)),new(A,Dash,B),profile,c.RngState,1));
        Assert.True(move.Accepted);Assert.NotNull(move.Continuation);
        var continued=RosterMoveDecisionBattle.Continue(move.State,A,move.Continuation);Assert.True(continued.Accepted);
        var calculation=PhysicalDamageRules.Calculate(profile.Abilities[Dash].Definition,5,70,70,0,1,0,BattleFacing.West,
            AbilityFormulaMode.Execution,move.State.Battle.TurnBattle.Turns.RngState);
        Assert.Equal(calculation.Outcome!.Damage,Assert.Single(continued.Events.OfType<DashResolvedEvent>()).Damage);
        Assert.Equal(calculation.RngState,continued.State.Battle.TurnBattle.Turns.RngState);
        var limited=Make(seed:0,count:ulong.MaxValue-2);var before=Snapshot(limited.State);
        var rejected=DashAbilityBattleSimulator.ApplyRoster(limited.State,new(A,Dash,B),Profile(limited.State),limited.Profile);
        Assert.False(rejected.Accepted);Assert.Same(limited.State,rejected.State);Assert.Empty(rejected.Events);Assert.Equal(before,Snapshot(limited.State));
    }

    [Fact]
    public void Unsupported_formula_and_signed16_overflow_contexts_fail_at_definition_boundary()
    {
        Assert.Throws<NotSupportedException>(()=>new AbilityFormula((AbilityFormulaId)1,new(0,0)));
        Assert.Throws<NotSupportedException>(()=>new AbilityFormula(AbilityFormulaId.RandomPaPlusHalfPa,new(255,255)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new AbilityFormula(AbilityFormulaId.RandomPa,new(0,256)));
        Assert.Throws<ArgumentException>(()=>new AbilityFormula(AbilityFormulaId.RandomPa,new(0,4)).CalculateDamage(5,0,0));
        Assert.Equal(32538,new AbilityFormula(AbilityFormulaId.RandomPaPlusHalfPa,new(87,255)).CalculateDamage(99,32767,32767));
        var original=new AbilityFormula(AbilityFormulaId.RandomPa,new(0,4));
        Assert.Throws<NotSupportedException>(()=>DashAbilityBattleProfile.FromDefinitions(Ground,[new(Dash,1,true,false,original)],[]));
        Assert.Throws<NotSupportedException>(()=>TargetedAbilityBattleProfile.FromDefinitions(Ground,[new(Dash,4,false,true,original)],[]));
    }
}
