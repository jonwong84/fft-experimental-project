using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Tests.RosterCombatTestData;

namespace Zodiac.Framework.Tests;

[Trait("Ruleset", "OriginalFft")]
public sealed class RosterThrowStoneTests
{
    internal static readonly AssetId Stone = new("test:roster-stone");
    internal static TargetedAbilityBattleProfile Ability(AttackBattleSnapshot s,bool grant=true) => new(Ground,[new(Stone)],s.Combatants.Select(u=>new TargetedAbilityUnitGrant(u.UnitId,u.UnitId==A && grant ? [Stone] : [],0)));
    internal static FlatRosterProjectileHeights Heights(AttackBattleSnapshot s) => new(s.Combatants.Select(u=>new FlatRosterSpriteHeight(u.UnitId,30)));

    [Fact]
    public void Thirteen_live_source_vectors_resolve_interceptors_actual_rng_ko_and_all_occupant_pushes()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","roster-live-stone-contract-v1.json")));int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var d=row.GetProperty("input");var e=row.GetProperty("expected");var raw=d.GetProperty("units").EnumerateArray().ToArray();
            var s=Make(raw.Select(u=>new Spec(u.GetProperty("id").GetString()!,Tile(u.GetProperty("tile")),u.GetProperty("team").GetInt32(),u.GetProperty("hp").GetInt32())).ToArray(),width:8,depth:4,seed:d.GetProperty("word").GetUInt32(),count:d.GetProperty("count").GetUInt64());
            var data=raw.ToDictionary(u=>u.GetProperty("id").GetString()!);
            var state=new AttackBattleSnapshot(s.State.TurnBattle,s.State.Combatants.Select(u=>new AttackUnitState(u.UnitId,u.Hp,u.MaxHp,0,0,data[u.UnitId.Value].GetProperty("pa").GetInt32(),
                data[u.UnitId.Value].GetProperty("evade").GetInt32(),0,0,0,data[u.UnitId.Value].GetProperty("brave").GetInt32(),0,
                Enum.Parse<BattleFacing>(data[u.UnitId.Value].GetProperty("face").GetString()!),u.Status,data[u.UnitId.Value].GetProperty("ko").GetInt32(),u.RightHandWeaponId)));
            var h=new FlatRosterProjectileHeights(raw.Select(u=>new FlatRosterSpriteHeight(new(u.GetProperty("id").GetString()!),u.GetProperty("height").GetByte())));
            var before=Snapshot(state);var r=TargetedAbilityBattleSimulator.ApplyRoster(state,new(A,Stone,B),Ability(state),s.Profile,h);Assert.True(r.Accepted);
            var hit=Assert.Single(r.Events.OfType<ThrowStoneResolvedEvent>());Assert.Equal(e.GetProperty("target").GetString(),hit.TargetId.Value);Assert.Equal(e.GetProperty("hit").GetBoolean(),hit.Hit);
            Assert.Equal(e.GetProperty("damage").GetInt32(),hit.Damage);Assert.Equal(e.GetProperty("attempted").GetBoolean(),hit.KnockbackAttempted);
            var target=r.State.Combatants.Single(u=>u.UnitId==hit.TargetId);Assert.Equal(e.GetProperty("hp").GetInt32(),target.Hp);Assert.Equal(e.GetProperty("status").GetString(),target.Status.ToString());Assert.Equal(e.GetProperty("ko").GetInt32(),target.KoCount);
            Assert.Equal(Tile(e.GetProperty("destination")),Position(r.State,hit.TargetId));Assert.Equal(e.GetProperty("intercepted").GetBoolean(),r.Events.OfType<ThrowStoneTargetInterceptedEvent>().Any());
            var rng=new RandomState(e.GetProperty("word").GetUInt32(),e.GetProperty("count").GetUInt64());Assert.Equal(rng,r.State.TurnBattle.Turns.RngState);Assert.Equal(rng,r.State.TurnBattle.Battle.RngState);
            foreach(var peer in state.Combatants.Where(u=>u.UnitId!=hit.TargetId))Assert.Equal(peer,r.State.Combatants.Single(u=>u.UnitId==peer.UnitId));
            foreach(var turn in state.TurnBattle.Turns.Units.Where(u=>u.Id!=A))Assert.Equal(turn,r.State.TurnBattle.Turns.Units.Single(u=>u.Id==turn.Id));
            Assert.True(r.State.TurnBattle.Turns.Units.Single(u=>u.Id==A).ActionTaken);Assert.Equal(before,Snapshot(state));count++;
        }
        Assert.Equal(13,count);
    }

    [Fact]
    public void Pair_actual_results_and_two_unit_guard_remain_unchanged()
    {
        var pair=Make([new("a",new(1,1,0)),new("b",new(3,1,0),1)]);var profile=Ability(pair.State);var command=new UseTargetedAbilityCommand(A,Stone,B);
        var old=TargetedAbilityBattleSimulator.Apply(pair.State,command,profile);var roster=TargetedAbilityBattleSimulator.ApplyRoster(pair.State,command,profile,pair.Profile,Heights(pair.State));
        Assert.True(old.Accepted);Assert.Equal(Snapshot(old.State),Snapshot(roster.State));Assert.Equal(SerializeEvents(old.Events),SerializeEvents(roster.Events));
        var s=Make();var rejected=TargetedAbilityBattleSimulator.Apply(s.State,command,Ability(s.State));Assert.False(rejected.Accepted);Assert.Equal(AttackCommandRejection.UnsupportedSetup,rejected.AttackRejection);
    }

    [Fact]
    public void Actual_rng_exhaustion_is_atomic_and_exact_seven_or_four_draw_budgets_succeed()
    {
        foreach(ulong remaining in new ulong[]{0,3,4,6,7})
        {
            var s=Make([new("a",new(1,1,0)),new("b",new(3,1,0),1),new("c",new(1,2,0))],seed:57,count:ulong.MaxValue-remaining);var before=Snapshot(s.State);
            var r=TargetedAbilityBattleSimulator.ApplyRoster(s.State,new(A,Stone,B),Ability(s.State),s.Profile,Heights(s.State));Assert.Equal(remaining==7,r.Accepted);
            if(!r.Accepted){Assert.Same(s.State,r.State);Assert.Empty(r.Events);Assert.Equal(AttackCommandRejection.RandomDrawCountExhausted,r.AttackRejection);}else Assert.Equal(ulong.MaxValue,r.State.TurnBattle.Turns.RngState.DrawCount);
            Assert.Equal(before,Snapshot(s.State));
        }
        var miss=Make([new("a",new(1,1,0)),new("b",new(3,1,0),1),new("c",new(1,2,0))],evade:100,count:ulong.MaxValue-4);
        var accepted=TargetedAbilityBattleSimulator.ApplyRoster(miss.State,new(A,Stone,B),Ability(miss.State),miss.Profile,Heights(miss.State));Assert.True(accepted.Accepted);Assert.Equal(ulong.MaxValue,accepted.State.TurnBattle.Turns.RngState.DrawCount);
    }

    [Fact]
    public void Exact_actual_grant_setup_height_range_and_choice_admission_precedes_draws()
    {
        var s=Make();var c=new UseTargetedAbilityCommand(A,Stone,B);
        foreach(var r in new[]{TargetedAbilityBattleSimulator.ApplyRoster(s.State,c,Ability(s.State,false),s.Profile,Heights(s.State)),
            TargetedAbilityBattleSimulator.ApplyRoster(s.State,c,Ability(s.State),s.Profile,new([new(A,30),new(B,30)])),
            TargetedAbilityBattleSimulator.ApplyRoster(s.State,c with{TargetId=A},Ability(s.State),s.Profile,Heights(s.State)),
            TargetedAbilityBattleSimulator.ApplyRoster(s.State,c with{AbilityId=new("test:absent")},Ability(s.State),s.Profile,Heights(s.State))})
        {Assert.False(r.Accepted);Assert.Same(s.State,r.State);Assert.Empty(r.Events);}
        var far=Make([new("a",new(0,1,0)),new("b",new(5,1,0),1)]);Assert.Equal(AttackCommandRejection.OutOfRange,TargetedAbilityBattleSimulator.ValidateRosterCommand(far.State,c,Ability(far.State),far.Profile,Heights(far.State)));
        var acted=Make(acted:true);Assert.Equal(AttackCommandRejection.ActionAlreadyTaken,TargetedAbilityBattleSimulator.ValidateRosterCommand(acted.State,c,Ability(acted.State),acted.Profile,Heights(acted.State)));
    }

    [Fact]
    public void Replay_preserves_source_collection_order_and_current_boosted_pa()
    {
        var a=Make(pa:6);var b=Make(pa:6,reverse:true);var command=new UseTargetedAbilityCommand(A,Stone,B);
        var x=TargetedAbilityBattleSimulator.ApplyRoster(a.State,command,Ability(a.State),a.Profile,Heights(a.State));var y=TargetedAbilityBattleSimulator.ApplyRoster(b.State,command,Ability(b.State),b.Profile,Heights(b.State));
        Assert.True(x.Accepted);Assert.Equal(Snapshot(x.State),Snapshot(y.State));Assert.Equal(SerializeEvents(x.Events),SerializeEvents(y.Events));Assert.Equal(6,Assert.Single(x.Events.OfType<ThrowStoneResolvedEvent>()).Damage);
    }

    private static TilePosition Tile(JsonElement e)=>new(e[0].GetInt32(),e[1].GetInt32(),0);
}
