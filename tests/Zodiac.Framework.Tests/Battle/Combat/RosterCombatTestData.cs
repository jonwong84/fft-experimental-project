using System.Text.Json;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Tests;

internal static class RosterCombatTestData
{
    internal static readonly BattleUnitId A = new("a"), B = new("b"), C = new("c");
    internal static readonly TerrainSurfaceId Ground = new("test:ordinary-ground");
    internal static readonly AssetId Sword = new("test:ordinary-sword");
    internal sealed record Spec(string Id, TilePosition Tile, int Team = 0, int Hp = 50, int Ct = 0);
    internal sealed record Setup(AttackBattleSnapshot State, FlatRosterBattleProfile Profile);
    internal static Setup Make(Spec[]? specs = null, int width = 6, int depth = 5, int move = 4, int jump = 0,
        bool moved = false, bool acted = false, bool active = true, bool reverse = false, uint seed = 57, ulong count = 0,
        int pa = 5, int evade = 0, ulong ticks = 20)
    {
        specs ??= [new("a", new(1, 1, 0)), new("b", new(2, 1, 0), 1), new("c", new(3, 1, 0))];
        var rng = new RandomState(seed, count);
        var movement = new UnitMovementProfile(move, jump, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = specs.Select(u => new BattleUnitState(new(u.Id), u.Tile, movement, u.Id == "a" && moved));
        var turns = specs.Select((u, i) => new TurnUnitState(new(u.Id), 5, active && u.Id == "a" ? 15 : u.Ct, i,
            active && u.Id == "a", u.Id == "a" && moved, u.Id == "a" && acted));
        var combat = specs.Select(u => new AttackUnitState(new(u.Id), u.Hp, 50, 0, 0, pa, u.Id == "b" ? evade : 0,
            0, 0, 0, 70, 0, u.Id == "b" ? BattleFacing.West : BattleFacing.East,
            u.Hp == 0 ? AttackUnitStatus.Dead : u.Hp <= 10 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, Sword));
        var map = new BattleMapDefinition(width, depth, 1, Enumerable.Repeat(new BattleTile(Ground, 10), width * depth).ToArray());
        var state = new AttackBattleSnapshot(new(new(map, reverse ? bodies.Reverse() : bodies, rng),
            new(reverse ? turns.Reverse() : turns, rng, ticks, active ? TurnCyclePhase.UnitTurn : TurnCyclePhase.AddCt)), reverse ? combat.Reverse() : combat);
        return new(state, new(Ground, specs.Select((u, i) => new FlatRosterUnit(new(u.Id), u.Team, i))));
    }

    internal static RosterFacingTurnState Ready(bool moveFirst = false, bool reverse = false, int hp = 50)
    {
        var specs = new[] { new Spec("a", new(moveFirst ? 0 : 1, 1, 0), Ct: 110), new Spec("b", new(2, 1, 0), 1, hp), new Spec("c", new(4, 3, 0)) };
        var setup = Make(specs, active: false, reverse: reverse, seed: unchecked((57u - 12345u) * 4005161829u), count: 6, ticks: 0);
        var result = RosterFacingTurnBattle.Advance(RosterFacingTurnBattle.Begin(setup.State, setup.Profile));
        if (!result.Accepted) throw new InvalidOperationException("Synthetic roster ready setup failed.");
        return result.State;
    }
    internal static TilePosition Position(AttackBattleSnapshot s, BattleUnitId id) => s.TurnBattle.Battle.Units.Single(u => u.Id == id).Position;
    internal static string Snapshot(AttackBattleSnapshot s) => JsonSerializer.Serialize(new {
        Units = s.Combatants.OrderBy(u => u.UnitId.Value), Bodies = s.TurnBattle.Battle.Units.OrderBy(u => u.Id.Value)
            .Select(u => new { u.Id, u.Position, u.HasMoved, u.Move, u.MovementProfile.Jump, u.MovementProfile.BodyHeight,
                u.MovementProfile.Mode, u.MovementProfile.Traits, Costs = u.MovementProfile.TerrainCosts.OrderBy(k => k.Key.Value)
                    .Select(k => new { Surface = k.Key.Value, k.Value.MovePoints, k.Value.IsBlocked }) }),
        Turns = s.TurnBattle.Turns.Units.OrderBy(u => u.Id.Value), s.TurnBattle.Turns.Phase, s.TurnBattle.Turns.Ticks,
        s.TurnBattle.Turns.RngState, BattleRng = s.TurnBattle.Battle.RngState });
    internal static string SerializeEvents(IEnumerable<AttackResultEvent> events) => string.Join("\n", events.Select(e => e.GetType().Name + JsonSerializer.Serialize(e, e.GetType())));
}
