using Xunit;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiProjectionTests
{
    internal static DefaultAbilityAiUnit Caster => Unit("caster",0,100,100,75,8,SpellTargetTeam.Blue);
    internal static DefaultAbilityAiUnit Target => Unit("target",1,200,200,50,5,SpellTargetTeam.Red);
    internal static DefaultAbilityAiUnit Unit(string id, int order, ushort hp, ushort maxHp, ushort mp, byte ma, SpellTargetTeam team) =>
        new(new(new(id), order, new(order*2,0,0), team, team),
            new(new(id),ma,70,new(MagicZodiacSign.Aries, IsMale:true),new(),new(),Equipment:DefaultAbilityBattleDefinition.BareEquipment,Mp:mp),
            new(hp,maxHp,mp,70,hp<=maxHp/5));

    [Fact]
    public void Fire_ai_uses_four_real_evade_draws_no_proc_and_one_mp_cost()
    {
        var caster = Caster; var target = Target; var rng = new RandomState(57,7);
        var r = DefaultAbilityAiProjection.Calculate(caster,DefaultAbilityAiListTests.Spell(),[target],rng);
        Assert.True(r.Accepted); Assert.Equal((ushort)69,r.Outcome!.Caster.Current.Mp);
        var frame = Assert.Single(r.Outcome.Cast.Targets).Result;
        Assert.Equal((short)54,frame.HpDamage); Assert.Equal((ushort)100,frame.Accuracy);
        Assert.False(frame.Effects.AddedEffect); Assert.Equal(11UL,r.RngState.DrawCount);
        var hp = DefaultAbilityHitApplication.Project(target.Current,target.Vitals,frame);
        Assert.Equal((ushort)146,hp.Vitals.Hp); Assert.Equal((ushort)200,target.Vitals.Hp);
        Assert.Equal((ushort)75,caster.Current.Mp); Assert.Equal(new RandomState(57,7),rng);
    }

    [Fact]
    public void Healing_ai_has_no_formula_draws_and_projects_critical_clear()
    {
        var spell=DefaultAbilityAiListTests.Spell() with { Spell=new(FaithMagicAmountKind.Healing,14,MagicElement.None,false) };
        var target=Target with { Vitals=Target.Vitals with { Hp=30,Critical=true } };
        var r=DefaultAbilityAiProjection.Calculate(Caster,spell,[target],new(57,7));
        Assert.True(r.Accepted); Assert.Equal(new RandomState(57,7),r.RngState);
        var hp=DefaultAbilityHitApplication.Project(target.Current,target.Vitals,r.Outcome!.Cast.Targets[0].Result);
        Assert.Equal((ushort)84,hp.Vitals.Hp); Assert.False(hp.Vitals.Critical);
    }

    [Fact]
    public void Shared_kernel_projects_critical_ko_and_charging_without_live_bookkeeping()
    {
        var r=DefaultAbilityAiProjection.Calculate(Caster,DefaultAbilityAiListTests.Spell(),[Target],new(57,0));
        var frame=r.Outcome!.Cast.Targets[0].Result;
        var t=Target with { Current=Target.Current with { Statuses=new(Charging:true) }, Vitals=Target.Vitals with { Hp=80 } };
        var critical=DefaultAbilityHitApplication.Project(t.Current,t.Vitals,frame);
        Assert.Equal((ushort)26,critical.Vitals.Hp); Assert.True(critical.Vitals.Critical); Assert.True(critical.Current.Statuses.Charging);
        var ko=DefaultAbilityHitApplication.Project(t.Current,t.Vitals with { Hp=30,Critical=true },frame);
        Assert.True(ko.Current.Statuses.Dead); Assert.False(ko.Current.Statuses.Charging); Assert.False(ko.Vitals.Critical);
    }

    [Fact]
    public void Late_target_exhaustion_rolls_back_resource_and_all_formula_work()
    {
        var third=Unit("third",2,100,100,50,5,SpellTargetTeam.Red); var rng=new RandomState(57,ulong.MaxValue-7);
        var r=DefaultAbilityAiProjection.Calculate(Caster,DefaultAbilityAiListTests.Spell(),[Target,third],rng);
        Assert.False(r.Accepted); Assert.Equal(DefaultAbilityAiProjectionRejection.RandomDrawCountExhausted,r.Rejection);
        Assert.Null(r.Outcome); Assert.Equal(rng,r.RngState);
        Assert.Equal((ushort)75,Caster.Current.Mp);
    }

    [Fact]
    public void Resource_failure_invalid_order_and_unsupported_state_publish_no_projection()
    {
        Assert.Equal(DefaultAbilityAiProjectionRejection.ResourceBlocked,DefaultAbilityAiProjection.Calculate(Caster with { Current=Caster.Current with { Mp=5 } },DefaultAbilityAiListTests.Spell(),[Target],new(57,0)).Rejection);
        Assert.False(DefaultAbilityAiProjection.Calculate(Caster,DefaultAbilityAiListTests.Spell(),[Target,Caster],new(57,0)).Accepted);
        Assert.False(DefaultAbilityAiProjection.Calculate(Caster,DefaultAbilityAiListTests.Spell(),[Target with { Current=Target.Current with { Statuses=new(Sleep:true) } }],new(57,0)).Accepted);
    }
}
