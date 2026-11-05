using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class AiDecisionMakerTests
{
    private static readonly AssetId Self = new("test:self"), Stone = new("test:stone");
    private static readonly BoundedSwordWeapon Sword = new(new("test:roster-sword"),2);
    private static readonly RosterAiGeneralProfile General = new(true,true);
    private static Setup Bind(Setup s) => s with {State=new(s.State.TurnBattle,s.State.Combatants.Select(u=>new AttackUnitState(u.UnitId,u.Hp,u.MaxHp,0,0,u.PhysicalAttack,
        u.ClassPhysicalEvade,0,0,0,u.Brave,u.ZodiacSign,u.Facing,u.Status,u.KoCount,u.UnitId==s.Request.ActorId ? Sword.Id : null)))};
    private static AiAbilityEvaluationProfile Profile(Setup s) => new(Sword,new(new([new(Self)],s.Simulation.Units.Select(u=>new ImmediateAbilityUnitGrant(u.UnitId,[Self],0))),true),
        new(Ground,[new(Stone)],s.Simulation.Units.Select(u=>new TargetedAbilityUnitGrant(u.UnitId,[Stone],0))),
        new(s.Simulation.Units.Select(u=>new RosterAiSpriteHeight(u.UnitId,30))),true,true,true,true);
    private static Setup Sample(bool acted=false,bool reverse=false) => Bind(Make([new("a",0,1,15,Move:1,Acted:acted,Actions:["Self","Stone"]),new("b",1,0,95,Facing:BattleFacing.West),new("c",2,1)],
        positions:[new(1,1,0),new(3,1,0),new(1,2,0)],reverse:reverse));
    private static AiDecisionResult Query(Setup s,AiAbilityEvaluationProfile? profile=null,RandomState? rng=null,AiPriorityWorkspace? workspace=null) =>
        AiDecisionMaker.Decide(s.State,s.Request,new(s.Simulation,Refresh,s.Lists,General,Movement,profile ?? Profile(s)),rng ?? Rng,workspace);

    [Fact]
    public void Twelve_source_tapes_cover_entry_rng_ranks_capacity_selection_order_and_all_peer_restoration()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-list-contract-v1.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var raw=d.GetProperty("units").EnumerateArray().ToArray();string actor=d.GetProperty("actor").GetString()!;
            var units=raw.Select(u=>new Unit(u.GetProperty("id").GetString()!,u.GetProperty("order").GetInt32(),u.GetProperty("team").GetInt32(),u.GetProperty("ct").GetInt32(),
                u.GetProperty("hp").GetInt32(),u.GetProperty("maxHp").GetInt32(),u.GetProperty("speed").GetInt32(),u.GetProperty("move").GetInt32(),
                u.GetProperty("moved").GetBoolean(),u.GetProperty("acted").GetBoolean(),u.GetProperty("support").GetBoolean(),u.GetProperty("conserve").GetBoolean(),
                u.GetProperty("actions").EnumerateArray().Select(a=>a.GetString()!).ToArray(),Enum.Parse<BattleFacing>(u.GetProperty("face").GetString()!),u.GetProperty("pa").GetInt32())).ToArray();
            var rng=new RandomState(d.GetProperty("word").GetUInt32(),d.GetProperty("count").GetUInt64());
            var s=Bind(Make(units,actor,positions:raw.Select(u=>Tile(u.GetProperty("tile"))).ToArray(),ticks:d.GetProperty("ticks").GetUInt64(),rng:rng));
            var evades=raw.ToDictionary(u=>u.GetProperty("id").GetString()!,u=>u.GetProperty("evade").GetInt32());
            s=s with{State=new(s.State.TurnBattle,s.State.Combatants.Select(u=>new AttackUnitState(u.UnitId,u.Hp,u.MaxHp,0,0,u.PhysicalAttack,evades[u.UnitId.Value],0,0,0,u.Brave,u.ZodiacSign,u.Facing,u.Status,u.KoCount,u.RightHandWeaponId)))};
            var before=Snapshot(s.State);var workspace=AiPriorityWorkspace.StartUnknown(s.State.TurnBattle.Battle.Map);var result=Query(s,rng:rng,workspace:workspace);RngEquals(e,"word","count",result.RngState);
            if(e.GetProperty("rejection").GetString()=="UnknownPriority")
            {
                Assert.False(result.Accepted);Assert.Equal(e.GetProperty("occurrence").GetInt32(),result.Failure!.OccurrenceIndex);
                Assert.Equal(RosterThrowStoneAiEntryStage.Priority,result.Failure.ThrowStone!.Stage);Assert.Same(workspace,result.Workspace);Assert.Null(result.Outcome);
            }
            else
            {
                Assert.True(result.Accepted,row.GetProperty("id").GetString()+": "+result.Failure);var o=result.Outcome!;var entries=e.GetProperty("entries").EnumerateArray().ToArray();Assert.Equal(entries.Length,o.Entries.Length);
                for(int i=0;i<entries.Length;i++)
                {
                    var x=entries[i];var a=o.Entries[i];Assert.Equal(x.GetProperty("occurrence").GetInt32(),a.Entry.Index);Assert.Equal(x.GetProperty("kind").GetString(),Name(a.Entry.Source.Kind));
                    RngEquals(x,"entryWord","entryCount",a.EntryRng);RngEquals(x,"exitWord","exitCount",a.ExitRng);
                    Assert.Equal(NullableInt(x.GetProperty("insertion")),a.InsertionIndex);Action(x.GetProperty("best"),a.Best);
                }
                var ranks=e.GetProperty("ranked").EnumerateArray().ToArray();Assert.Equal(8,o.RankedActions.Length);for(int i=0;i<8;i++)Action(ranks[i],o.RankedActions[i]);
                RngEquals(e,"lotteryWord","lotteryCount",o.LotteryEntryRng);var rolls=e.GetProperty("rolls").EnumerateArray().ToArray();Assert.Equal(rolls.Length,o.Lottery.Length);
                for(int i=0;i<rolls.Length;i++)
                {
                    var x=rolls[i];var a=o.Lottery[i];Assert.Equal(x.GetProperty("slot").GetInt32(),a.Slot);Assert.Equal(x.GetProperty("raw").GetUInt32(),a.Raw);
                    Assert.Equal(x.GetProperty("modulo").GetInt32(),a.Modulo);Assert.Equal(x.GetProperty("hit").GetInt32(),a.HitPercent);Assert.Equal(x.GetProperty("passed").GetBoolean(),a.Passed);
                }
                Assert.Equal(NullableInt(e.GetProperty("selected")),o.SelectedRankedIndex);Assert.Equal(e.GetProperty("reason").GetString(),o.Reason.ToString());
                Assert.Equal(NullableInt(e.GetProperty("selectedOccurrence")),o.Plan.Action?.OccurrenceIndex);Assert.Equal(Tile(e.GetProperty("destination")),o.Plan.Destination);Assert.True(o.Plan.ChooseFacingAtWait);
            }
            Assert.Empty(workspace.KnownPriorities);Assert.Equal(before,Snapshot(s.State));count++;
        }
        Assert.Equal(12,count);
    }

    [Fact]
    public void Only_entered_effects_need_profiles_and_post_act_empty_list_needs_none()
    {
        var s=Sample();var noSelf=Profile(s) with{Accumulate=null};var failed=Query(s,noSelf);Assert.False(failed.Accepted);Assert.Equal(1,failed.Failure!.OccurrenceIndex);Assert.Equal(Rng,failed.RngState);
        var noStone=Query(s,Profile(s) with{ThrowStone=null});Assert.False(noStone.Accepted);Assert.Equal(2,noStone.Failure!.OccurrenceIndex);
        var acted=Sample(acted:true);var r=Query(acted,new(NoDefendSupport:true,FreshOrdinarySelectionDefaults:true));Assert.True(r.Accepted);Assert.Null(r.Outcome!.Plan.Action);Assert.Equal(8,r.Outcome.Lottery.Length);
        Assert.All(r.Outcome.Entries,e=>Assert.Equal(e.EntryRng,e.ExitRng));
        var attack=Bind(Make([new("a",0,1,15,Move:1),new("b",1,0,95),new("c",2,1)],positions:[new(1,1,0),new(2,1,0),new(1,2,0)]));
        Assert.True(Query(attack,new(Sword,OrdinaryAttackTilePolicy:true,NoDefendSupport:true,FreshOrdinarySelectionDefaults:true)).Accepted);
        Assert.False(Query(s,Profile(s) with{NoDefendSupport=false}).Accepted);
    }

    [Fact]
    public void Lottery_failure_discards_all_entries_ranked_actions_and_successor_workspace()
    {
        var s=Bind(Make([new("a",0,1,15,Move:1),new("b",1,0,95),new("c",2,1)],positions:[new(1,1,0),new(2,1,0),new(1,2,0)]));
        var workspace=AiPriorityWorkspace.StartUnknown(s.State.TurnBattle.Battle.Map);var rng=new RandomState(1,ulong.MaxValue-9);var before=Snapshot(s.State);
        var r=Query(s,rng:rng,workspace:workspace);Assert.False(r.Accepted);Assert.Equal(AiDecisionStage.Lottery,r.Failure!.Stage);Assert.Equal(0,r.Failure.LotteryIndex);
        Assert.Null(r.Outcome);Assert.Equal(rng,r.RngState);Assert.Same(workspace,r.Workspace);Assert.Empty(workspace.KnownPriorities);Assert.Equal(before,Snapshot(s.State));
    }

    [Fact]
    public void Reordered_storage_replay_and_full_query_workspace_identity_are_deterministic()
    {
        var a=Sample();var b=Sample(reverse:true);var before=Snapshot(a.State);var x=Query(a);var y=Query(b);var repeat=Query(a);
        Assert.True(x.Accepted);Assert.Equal(x.RngState,y.RngState);Assert.Equal(x.RngState,repeat.RngState);
        Assert.Equal(JsonSerializer.Serialize(x.Outcome!.RankedActions),JsonSerializer.Serialize(y.Outcome!.RankedActions));
        Assert.Equal(JsonSerializer.Serialize(x.Outcome.Plan),JsonSerializer.Serialize(repeat.Outcome!.Plan));
        Assert.Equal(before,Snapshot(a.State));Assert.Equal(before,Snapshot(b.State));Assert.Throws<ArgumentException>(()=>Query(b,workspace:x.Workspace));
    }

    private static string Name(AiHumanAbilityKind k)=>k==AiHumanAbilityKind.Accumulate ? "Self" : k==AiHumanAbilityKind.ThrowStone ? "Stone" : k==AiHumanAbilityKind.FrogAttack ? "Frog" : "Attack";
    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
    private static int? NullableInt(JsonElement e)=>e.ValueKind==JsonValueKind.Null ? null : e.GetInt32();
    private static void RngEquals(JsonElement e,string word,string count,RandomState rng)=>Assert.Equal(new RandomState(e.GetProperty(word).GetUInt32(),e.GetProperty(count).GetUInt64()),rng);
    private static void Action(JsonElement e,AiRankedAbility? a)
    {
        if(e.ValueKind==JsonValueKind.Null){Assert.Null(a);return;}Assert.NotNull(a);Assert.Equal(e.GetProperty("occurrence").GetInt32(),a.OccurrenceIndex);
        Assert.Equal(e.GetProperty("kind").GetString(),Name(a.Kind));Assert.Equal(Tile(e.GetProperty("destination")),a.Destination);Assert.Equal(e.GetProperty("actFirst").GetBoolean(),a.ActBeforeMove);
        Assert.Equal(e.GetProperty("target").GetString(),a.TargetId?.Value);Assert.Equal(e.GetProperty("priority").GetInt16(),a.Score.Priority);Assert.Equal(e.GetProperty("hit").GetByte(),a.Score.HitPercent);
        Assert.Equal(e.GetProperty("proximity").GetByte(),a.Proximity);
    }
}
