using System.Collections.Immutable;
using System.Text.Json;
using TestSupport;
using Zodiac.Content.BattleSetup;
using Zodiac.Content.ContentPackages;
using Zodiac.Content.Definitions;
using Zodiac.Framework.Random;
using Xunit;

namespace Zodiac.Framework.Ai.Tests;

public sealed class DefaultAbilityAiReadyTests
{
    internal static readonly BattleUnitId Mage=new("mage"), Guard=new("guard");
    internal static (PreparedDefaultAbilityBattle Content, DefaultAbilityAiCatalog Catalog, DefaultAbilityBattleState State) Setup(
        byte area=1, ushort mp=75, byte faith=70, bool heal=false, bool learn=false, byte charge=4, ushort? targetHp=null)
    {
        var d=AuthoredDefaultAbility.Document() with {SchemaVersion=3,MetadataPackageId="test:package/spell-ai"};
        d.Spells[0]=d.Spells[0] with {Targeting=d.Spells[0].Targeting with {Area=area},ChargeTime=charge,
            Formula=heal?new(FaithMagicAmountKind.Healing,14,MagicElement.None,false):d.Spells[0].Formula};
        d.Units[0]=d.Units[0] with {LearnedPrimarySlots=[0],Mp=mp,Faith=faith};
        d.Units[1]=d.Units[1] with {LearnedPrimarySlots=[],Team=heal?SpellTargetTeam.Blue:SpellTargetTeam.Red,Hp=targetHp??(heal?(ushort)30:(ushort)200)};
        var behavior=heal?new AiAbilityBehavior(TargetsAllies:true,AffectsHp:true,AffectedBySilence:true,AffectedByFaith:true,FollowTarget:true,Reflectable:true):DefaultAbilityAiListTests.FireBehavior;
        var meta=new ContentPackage(d.MetadataPackageId!,"test:profile/ai",ContentPackageKind.Baseline,null,[],
            [new(new("test:skillset/magic"),[new(0,new(AuthoredDefaultAbility.Fire))])],
            [new(new(AuthoredDefaultAbility.Fire),50,100,1,behavior,true,learnOnHit:learn)]);
        var content=PreparedDefaultAbilityBattle.Create(d,meta);
        var spell=content.Definition.Spells[new(AuthoredDefaultAbility.Fire)];
        var catalog=new DefaultAbilityAiCatalog(content.Definition,
            new Dictionary<AssetId,DefaultAbilityAiSkillset>{{new(AuthoredDefaultAbility.Wizard),new(11,new("test:skillset/magic"),[new(0,spell,behavior,true)])}});
        return(content,catalog,DefaultAbilityBattle.Apply(content.Definition.Start(),new DefaultAbilityCommand.Advance()).State);
    }

    internal static string Receipt(DefaultAbilityBattleState state)=>JsonSerializer.Serialize(new {
        state.Revision,state.Turns.Ticks,state.Turns.RngState,state.Turns.ActiveUnitId,state.LearningReport,state.Finished,
        units=state.Units.Select(u=>new {u.Id,u.Position,u.Facing,u.Current,u.Vitals,u.RawStats,u.Level,u.Experience,u.OutcomeMarker,
            career=u.JobPoints.OrderBy(p=>p.Key.Value).Select(p=>new {id=p.Key.Value,p.Value}),
            learned=u.LearnedAbilitySlots.OrderBy(p=>p.Key.Value).Select(p=>new {id=p.Key.Value,slots=p.Value.Order()})}),
        pending=state.Pending.OrderBy(p=>p.Key.Value).Select(p=>new {id=p.Key.Value,p.Value.Counter,p.Value.Aim})});

    [Fact]
    public void Fresh_query_composes_literal_fire_score_and_preserves_all_live_state()
    {
        var (_,catalog,state)=Setup(); string before=Receipt(state);var rng=new RandomState(57,9);
        var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(Mage,0,new(4,2,0),[]),rng);
        Assert.True(r.Accepted,r.Rejection);Assert.Equal(DefaultAbilityAiReadyDisposition.Scored,r.Disposition);
        Assert.Equal((short)49,r.Outcome!.Score!.Priority);Assert.Equal((ushort)146,r.Outcome.Units[1].Vitals.Hp);
        Assert.Equal((ushort)69,r.Outcome.Units[0].Current.Mp);Assert.Equal(13UL,r.RngState.DrawCount);
        Assert.Equal(before,Receipt(state));Assert.Equal(new RandomState(57,9),rng);
    }

    [Fact]
    public void Area_self_hit_applies_cost_once_and_accounts_collateral_before_absolute_score()
    {
        var (_,catalog,state)=Setup(area:2);
        var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(Mage,0,new(2,2,0),[],false),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal(2,r.Outcome!.Frames.Length);
        Assert.Equal((ushort)46,r.Outcome.Units[0].Vitals.Hp);Assert.Equal((ushort)146,r.Outcome.Units[1].Vitals.Hp);
        Assert.Equal((short)-22,r.Outcome.Score!.Priority);Assert.Equal((short)-1,r.Outcome.Accounting.PriorityDelta);
        Assert.Equal(8UL,r.RngState.DrawCount);
    }

    [Fact]
    public void Movement_exclusions_happen_after_real_formula_draws()
    {
        var (_,catalog,state)=Setup(area:4);
        var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(Mage,0,new(4,2,0),[Guard]),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal(2,r.Outcome!.Frames.Length);
        Assert.Equal((ushort)100,r.Outcome.Units[0].Vitals.Hp);Assert.Equal((short)49,r.Outcome.Score!.Priority);
        Assert.Equal(8UL,r.RngState.DrawCount);
        var tile=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(Mage,0,new(4,2,0),[Guard],false),new(57,0));
        Assert.Equal(DefaultAbilityAiReadyDisposition.NotValuable,tile.Disposition);Assert.Null(tile.Outcome!.Score);
        Assert.Equal(8UL,tile.RngState.DrawCount);
    }

    [Fact]
    public void Zero_numeric_damage_can_still_be_a_valuable_positive_accuracy_hit()
    {
        var (_,catalog,state)=Setup(faith:0);
        var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(Mage,0,new(4,2,0),[]),new(57,0));
        Assert.True(r.Accepted,r.Rejection);Assert.True(r.Outcome!.Accounting.Valuable);
        Assert.Equal((ushort)200,r.Outcome.Units[1].Vitals.Hp);Assert.Equal((short)14,r.Outcome.Score!.Priority);
    }

    [Fact]
    public void No_useful_healing_target_stops_before_formula_but_injured_ally_projects_cure_without_draws()
    {
        var (_,catalog,state)=Setup(heal:true);
        var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(Mage,0,new(4,2,0),[]),new(57,9));
        Assert.True(r.Accepted,r.Rejection);Assert.Equal((ushort)84,r.Outcome!.Units[1].Vitals.Hp);
        Assert.False(r.Outcome.Units[1].Vitals.Critical);Assert.Equal(new RandomState(57,9),r.RngState);
        var full=state.With(units:state.Units.SetItem(1,state.Units[1] with {Vitals=state.Units[1].Vitals with {Hp=200,Critical=false}}));
        var none=DefaultAbilityAiReadyQuery.Evaluate(full,catalog,new(Mage,0,new(4,2,0),[]),new(57,9));
        Assert.Equal(DefaultAbilityAiReadyDisposition.NotValuable,none.Disposition);
        Assert.Empty(none.Outcome!.Frames);Assert.Equal(new RandomState(57,9),none.RngState);
    }

    [Theory]
    [InlineData("slot")] [InlineData("aim")] [InlineData("actor")] [InlineData("foreign")] [InlineData("follow")]
    public void Unbound_slot_illegal_aim_wrong_actor_and_foreign_catalog_reject_atomically(string change)
    {
        var (_,catalog,state)=Setup();var request=new DefaultAbilityAiReadyRequest(Mage,0,new(4,2,0),[]);
        if(change=="slot")request=request with {PrimarySlot=1};
        if(change=="aim")request=request with {Aim=new(6,4,0)};
        if(change=="actor")request=request with {ActorId=Guard};
        if(change=="foreign")catalog=Setup().Catalog;
        if(change=="follow")catalog=new(state.Definition,catalog.PrimaryByJob.Select(p=>KeyValuePair.Create(p.Key,
            p.Value with {Slots=p.Value.Slots.Select(s=>s with {Behavior=s.Behavior with {FollowTarget=false}}).ToImmutableArray()})));
        string before=Receipt(state);var rng=new RandomState(57,9);
        var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,request,rng);
        Assert.False(r.Accepted);Assert.Null(r.Outcome);Assert.Equal(rng,r.RngState);Assert.Equal(before,Receipt(state));
    }

    [Fact]
    public void Insufficient_current_mp_pending_cast_and_report_are_not_ready_hypotheses()
    {
        var low=Setup(mp:5);
        Assert.False(DefaultAbilityAiReadyQuery.Evaluate(low.State,low.Catalog,new(Mage,0,new(4,2,0),[]),new(57,0)).Accepted);
        var x=Setup(learn:true);var state=DefaultAbilityBattle.Apply(x.State,new DefaultAbilityCommand.Cast(Mage,new(AuthoredDefaultAbility.Fire),new(4,2,0))).State;
        Assert.False(DefaultAbilityAiReadyQuery.Evaluate(state,x.Catalog,new(Mage,0,new(4,2,0),[]),new(57,0)).Accepted);
        state=DefaultAbilityBattle.Apply(state,new DefaultAbilityCommand.Wait(Mage)).State;
        state=DefaultAbilityBattle.Apply(state,new DefaultAbilityCommand.Advance()).State;
        state=DefaultAbilityBattle.Apply(state,new DefaultAbilityCommand.Wait(Guard)).State;
        state=DefaultAbilityBattle.Apply(state,new DefaultAbilityCommand.Advance()).State;
        Assert.NotNull(state.LearningReport);
        Assert.False(DefaultAbilityAiReadyQuery.Evaluate(state,x.Catalog,new(Mage,0,new(4,2,0),[]),new(57,0)).Accepted);
    }

    [Fact]
    public void Late_formula_counter_failure_rolls_back_the_entire_composed_query()
    {
        var (_,catalog,state)=Setup(area:2);var rng=new RandomState(57,ulong.MaxValue-7);string before=Receipt(state);
        var r=DefaultAbilityAiReadyQuery.Evaluate(state,catalog,new(Mage,0,new(2,2,0),[],false),rng);
        Assert.False(r.Accepted);Assert.Null(r.Outcome);Assert.Equal(rng,r.RngState);Assert.Equal(before,Receipt(state));
    }
}
