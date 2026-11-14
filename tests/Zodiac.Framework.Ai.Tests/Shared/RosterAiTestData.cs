using System.Collections.Immutable;
using System.Text.Json;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai.Tests;

internal static class RosterAiTestData
{
    internal sealed record Unit(string Id, int Order, int Team, int Ct = 0, int Hp = 50, int MaxHp = 50,
        int Speed = 5, int Move = 3, bool Moved = false, bool Acted = false, bool Support = false,
        bool Conserve = false, string[]? Actions = null, BattleFacing Facing = BattleFacing.South, int Pa = 5);
    internal sealed record Setup(AttackBattleSnapshot State, RosterAiSimulationProfile Simulation,
        RosterAiRefreshRequest Request, AiHumanAbilityLists Lists);
    internal static readonly BattleUnitId A = new("a"), B = new("b"), C = new("c");
    internal static readonly TerrainSurfaceId Ground = new("test:roster-ground");
    internal static readonly RosterAiRefreshProfile Refresh = new(true, true, true, true);
    internal static readonly RosterAiMovementProfile Movement = new(true, true, true, true, true);
    internal static readonly RandomState Rng = new(1, 7);

    internal static Setup Make(Unit[]? units = null, string actor = "a", bool reverse = false, ulong ticks = 20,
        RandomState? rng = null, TilePosition[]? positions = null)
    {
        units ??= [new("a",0,0,15),new("b",1,1,95),new("c",2,0)];
        var random = rng ?? Rng;
        var bodies = units.Select((u,i) => new BattleUnitState(new(u.Id), positions?[i] ?? new(i%8,i/8+1,0),
            new UnitMovementProfile(u.Move,0,6,UnitMovementMode.Normal,UnitMovementTraits.None,[KeyValuePair.Create(Ground,new TerrainMovementCost(1))]),u.Moved));
        var turns = units.Select(u => new TurnUnitState(new(u.Id),u.Speed,u.Ct,u.Order,u.Id==actor,u.Moved,u.Acted));
        var combat = units.Select(u => new AttackUnitState(new(u.Id),u.Hp,u.MaxHp,0,0,u.Pa,0,0,0,0,70,0,u.Facing,
            u.Hp==0 ? AttackUnitStatus.Dead : u.Hp<=u.MaxHp/5 ? AttackUnitStatus.Critical : AttackUnitStatus.None,0,null));
        var state = new AttackBattleSnapshot(new(new(new(8,4,1,Enumerable.Repeat(new BattleTile(Ground,10),32).ToArray()),
            reverse ? bodies.Reverse() : bodies,random),new(reverse ? turns.Reverse() : turns,random,ticks,TurnCyclePhase.UnitTurn)),reverse ? combat.Reverse() : combat);
        var battle = new FlatRosterBattleProfile(Ground,units.Select(u => new FlatRosterUnit(new(u.Id),u.Team,u.Order)));
        var simulation = new RosterAiSimulationProfile(battle,units.Select(u => new AiUnitBaseline(new(u.Id),255)),true);
        var policies = units.Select(u => new AiUnitPolicy(new(u.Id),u.Conserve,u.Support)).ToImmutableArray();
        var lists = AiHumanAbilityLists.Build(units.Select(u => new AiHumanUnitAbilitySource(new(u.Id),
            new(new("test:skill/"+u.Id),(u.Actions ?? []).Select((a,i) => new AiHumanActionSlot(i,new(a=="Self" ? "test:self" : a=="Dash" ? "test:dash" : "test:stone"),
                a=="Self" ? AiHumanAbilityKind.Accumulate : a=="Dash" ? AiHumanAbilityKind.Dash : AiHumanAbilityKind.ThrowStone,true,true))))),new(true,true));
        return new(state,simulation,new(new(actor),policies),lists);
    }

    internal static string Snapshot(AttackBattleSnapshot s) => JsonSerializer.Serialize(new {
        Combat=s.Combatants.OrderBy(u=>u.UnitId.Value),Turns=s.TurnBattle.Turns.Units.OrderBy(u=>u.Id.Value),
        Bodies=s.TurnBattle.Battle.Units.OrderBy(u=>u.Id.Value).Select(u=>new {u.Id,u.Position,u.HasMoved,u.Move}),
        s.TurnBattle.Turns.RngState,s.TurnBattle.Turns.Ticks,s.TurnBattle.Turns.Phase});
}
