using System.Text.Json;
using Xunit;
using Zodiac.Framework.Random;
using static Zodiac.Framework.Ai.Tests.RosterAiTestData;

namespace Zodiac.Framework.Ai.Tests;

internal static class RosterAiSessionTestData
{
    private static TilePosition Tile(JsonElement a) => new(a[0].GetInt32(), a[1].GetInt32(), 0);
    private static readonly AssetId Self = new("test:self"), Stone = new("test:stone");
    private static readonly BoundedSwordWeapon Sword = new(new("test:roster-sword"), 2);
    internal sealed record Sample(Setup Setup, RosterFacingTurnState State, RosterBasicSkillAiTurnConfiguration Configuration);

    internal static Sample Load(JsonElement d, bool reverse = false, bool allSwordBindings = false)
    {
        string actor = d.GetProperty("actor").GetString()!;
        var raw = d.GetProperty("units").EnumerateArray().ToArray();
        var units = raw.Select(u => new Unit(u.GetProperty("id").GetString()!, u.GetProperty("order").GetInt32(), u.GetProperty("team").GetInt32(),
            u.GetProperty("ct").GetInt32(), u.GetProperty("hp").GetInt32(), u.GetProperty("maxHp").GetInt32(), u.GetProperty("speed").GetInt32(),
            u.GetProperty("move").GetInt32(), Actions: u.GetProperty("actions").EnumerateArray().Select(a => a.GetString()!).ToArray(),
            Facing: Enum.Parse<BattleFacing>(u.GetProperty("face").GetString()!), Pa: u.GetProperty("pa").GetInt32(),
            Support: u.GetProperty("support").GetBoolean(), Conserve: u.GetProperty("conserve").GetBoolean())).ToArray();
        var rng = new RandomState(d.GetProperty("word").GetUInt32(), d.GetProperty("count").GetUInt64());
        var s = Make(units, actor, reverse, d.GetProperty("ticks").GetUInt64(), rng, raw.Select(u => Tile(u.GetProperty("tile"))).ToArray());
        var ability = new ImmediateAbilityBattleProfile([new(Self)], units.Select(u => new ImmediateAbilityUnitGrant(new(u.Id), [Self], 0)));
        var selection = new RosterBasicSkillAiProfile(Sword, new(ability, true),
            new(Ground, [new(Stone)], units.Select(u => new TargetedAbilityUnitGrant(new(u.Id), [Stone], 0))),
            new(units.Select(u => new RosterAiSpriteHeight(new(u.Id), 30))), true, true, true, true);
        var facing = d.GetProperty("facing").ValueKind == JsonValueKind.Null ? (BattleFacing?)null : Enum.Parse<BattleFacing>(d.GetProperty("facing").GetString()!);
        var c = new RosterBasicSkillAiTurnConfiguration(s.Simulation, Refresh, s.Lists, new(true, true), Movement, selection, new(true, true, true, true), facing);
        bool acted = raw.Single(u => u.GetProperty("id").GetString() == actor).GetProperty("acted").GetBoolean();
        var evades = raw.ToDictionary(u => u.GetProperty("id").GetString()!, u => u.GetProperty("evade").GetInt32());
        var combatants = s.State.Combatants.Select(u => new AttackUnitState(u.UnitId, u.Hp, u.MaxHp, 0, 0,
            u.PhysicalAttack - (acted && u.UnitId.Value == actor ? 1 : 0), evades[u.UnitId.Value], 0, 0, 0, u.Brave, u.ZodiacSign, u.Facing,
            u.Status, u.KoCount, allSwordBindings || u.UnitId.Value == actor ? Sword.Id : null));
        // Enter through public AddCt/Advance: no internal constructor, forged UNIT_TURN, or new friend access.
        var prior = new RandomState(unchecked((rng.Value - 12345u) * 4005161829u), rng.DrawCount - 1);
        var turns = s.State.TurnBattle.Turns.Units.Select(u => new TurnUnitState(u.Id, u.Speed,
            u.Id.Value == actor ? 110 : u.Ct - u.Speed, u.TieKey));
        var fresh = RosterFacingTurnBattle.Begin(new(new(new(s.State.TurnBattle.Battle.Map, s.State.TurnBattle.Battle.Units, prior),
            new(turns, prior)), combatants), s.Simulation.Battle);
        var entered = RosterFacingTurnBattle.Advance(fresh); Assert.True(entered.Accepted);
        var state = entered.State;
        if (acted)
        {
            var self = RosterFacingTurnBattle.UseImmediateAbility(state, new(new(actor), Self), ability);
            Assert.True(self.Accepted); state = self.State;
        }
        Assert.Equal(rng, state.Battle.TurnBattle.Turns.RngState);
        return new(s, state, c);
    }

}
