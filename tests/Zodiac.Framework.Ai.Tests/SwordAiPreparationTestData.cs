using System.Collections.Immutable;
using System.Text.Json;

namespace Zodiac.Framework.Ai.Tests;

internal static class SwordAiPreparationTestData
{
    internal static readonly BattleUnitId A = new("sample:unit/a"), B = new("sample:unit/b");
    internal static readonly TerrainSurfaceId Ground = new("sample:terrain/ordinary-ground");
    internal static readonly AssetId Weapon = new("sample:item/plain-sword");
    internal static readonly FftRandomState Rng = new(1, 7), LiveRng = new(57, 100);
    internal static readonly SwordAiCandidateProfile Candidate = new(new(Ground), new(Ground, A, B), [new(A, 255), new(B, 19)]);
    internal static readonly AiScoreProfile Score = new([new(A, AiScoreFacts.ClosedProfile), new(B, AiScoreFacts.ClosedProfile)]);
    internal static readonly SwordAiRefreshProfile Refresh = new(true, true, true);
    internal static readonly SwordAiPreparationProfile Preparation = new(true, true, true);
    internal static SwordAiRefreshRequest Request(bool second = false, bool cachedSupport = false, bool reverse = false)
    {
        var actor = second ? B : A; var target = second ? A : B;
        ImmutableArray<SwordAiRefreshPolicy> policies = [new(actor, true, cachedSupport), new(target, false, false)];
        return new(actor, target, reverse ? policies.Reverse().ToImmutableArray() : policies);
    }
    internal static AttackBattleSnapshot State(int ct = 15, int targetCt = 95, int speed = 5, int targetSpeed = 5,
        ulong ticks = 20, int hp = 50, int evade = 0, int move = 3, bool second = false, bool reverse = false,
        bool moved = true, int distance = 1, bool acted = false)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var movement = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, new(1, 1, 0), movement, moved), new(target, new(1 + distance, 1, 0), movement, true) };
        var turns = new TurnUnitState[] { new(actor, speed, ct, 2, true, moved, acted), new(target, targetSpeed, targetCt, 17, false, true, true) };
        AttackUnitState Combat(BattleUnitId id, int current, int classEvade, BattleFacing facing) =>
            new(id, current, 50, 0, 0, 5, classEvade, 0, 0, 0, 70, 0, facing,
                current <= 10 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, hp, 0, BattleFacing.East), Combat(target, 50, evade, BattleFacing.West) };
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), 36).ToArray();
        return new(new(new(new(6, 6, 1, tiles), reverse ? bodies.Reverse() : bodies, LiveRng),
            new(reverse ? turns.Reverse() : turns, LiveRng, ticks, TurnCyclePhase.UnitTurn)), reverse ? combat.Reverse() : combat);
    }
    internal static string Serialize(object? value) => JsonSerializer.Serialize(value);
    internal static FacingTurnState Ready(int evade = 89, uint entryWord = 1, ulong count = 7,
        bool second = false, bool reverse = false, bool outOfReach = false, bool move = true, TilePosition? origin = null, int movePoints = 1)
    {
        var actor = second ? B : A; var target = second ? A : B;
        // Inverse BIOS step gives the requested entry through an actual upkeep draw; legacy fixtures then Move.
        var rng = new FftRandomState(unchecked((entryWord - 12345u) * 4005161829u), count - 1);
        var movement = new UnitMovementProfile(movePoints, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        BattleUnitState[] bodies = [new(actor, origin ?? new(3, 1, 0), movement), new(target, new(1, 1, 0), movement)];
        TurnUnitState[] turns = [new(actor, 5, 110, 2), new(target, 5, 90, 17)];
        AttackUnitState Combat(BattleUnitId id, int evasion) => new(id, 50, 50, 0, 0, 5, evasion, 0, 0, 0, 70, 0,
            BattleFacing.East, AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, 0), Combat(target, evade) };
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), 15).ToArray();
        var battle = new AttackBattleSnapshot(new(new(new(5, 3, 1, tiles), reverse ? bodies.Reverse() : bodies, rng),
            new(reverse ? turns.Reverse() : turns, rng)), reverse ? combat.Reverse() : combat);
        var ready = FacingTurnBattle.Advance(FacingTurnBattle.Begin(battle, new(Ground)));
        if (!ready.Accepted) throw new InvalidOperationException("Synthetic upkeep setup failed.");
        if (!move) return ready.State;
        var moved = FacingTurnBattle.Move(ready.State, new(actor, new(outOfReach ? 4 : 2, 1, 0)));
        return moved.Accepted ? moved.State : throw new InvalidOperationException("Synthetic Move setup failed.");
    }

    internal static FacingTurnState AfterAct(int evade = 0, int power = 5, bool second = false, bool reverse = false, uint entryWord = 1, ulong count = 7)
    {
        var ready = Ready(evade: evade, entryWord: entryWord, count: count, second: second, reverse: reverse, move: false, origin: new(2, 1, 0));
        var result = SwordAiActFirstExecution.StartPreparedInReach(ready, Request(second, reverse: reverse), new(Weapon, power),
            Candidate, Score, Refresh, Preparation, SwordAiScenarioTeam.NonBlue, new(true, true, true, true, true, true),
            new(true, true, true, true));
        return result.Accepted ? result.State : throw new InvalidOperationException("Synthetic ACT-first setup failed.");
    }

    internal static string Snapshot(AttackBattleSnapshot state) => Serialize(new {
        Combatants = state.Combatants.OrderBy(u => u.UnitId.Value),
        Turns = state.TurnBattle.Turns.Units.OrderBy(u => u.Id.Value), state.TurnBattle.Turns.Phase,
        state.TurnBattle.Turns.Ticks, state.TurnBattle.Battle.Map, state.TurnBattle.Battle.RngState,
        TurnRng = state.TurnBattle.Turns.RngState,
        Bodies = state.TurnBattle.Battle.Units.OrderBy(u => u.Id.Value).Select(u => new { u.Id, u.Position, u.HasMoved, u.MovementProfile.Move }) });
}
