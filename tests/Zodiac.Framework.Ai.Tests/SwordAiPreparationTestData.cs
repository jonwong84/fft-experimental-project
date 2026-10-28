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
    internal static readonly SwordAiScoreProfile Score = new([new(A, SwordAiScoreFacts.ClosedProfile), new(B, SwordAiScoreFacts.ClosedProfile)]);
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
        bool moved = true, int distance = 1)
    {
        var actor = second ? B : A; var target = second ? A : B;
        var movement = new UnitMovementProfile(move, 0, 6, UnitMovementMode.Normal, UnitMovementTraits.None,
            [KeyValuePair.Create(Ground, new TerrainMovementCost(1))]);
        var bodies = new BattleUnitState[] { new(actor, new(1, 1, 0), movement, moved), new(target, new(1 + distance, 1, 0), movement, true) };
        var turns = new TurnUnitState[] { new(actor, speed, ct, 2, true, moved, false), new(target, targetSpeed, targetCt, 17, false, true, true) };
        AttackUnitState Combat(BattleUnitId id, int current, int classEvade, BattleFacing facing) =>
            new(id, current, 50, 0, 0, 5, classEvade, 0, 0, 0, 70, 0, facing,
                current <= 10 ? AttackUnitStatus.Critical : AttackUnitStatus.None, 0, Weapon);
        var combat = new[] { Combat(actor, hp, 0, BattleFacing.East), Combat(target, 50, evade, BattleFacing.West) };
        var tiles = Enumerable.Repeat(new BattleTile(Ground, 10), 36).ToArray();
        return new(new(new(new(6, 6, 1, tiles), reverse ? bodies.Reverse() : bodies, LiveRng),
            new(reverse ? turns.Reverse() : turns, LiveRng, ticks, TurnCyclePhase.UnitTurn)), reverse ? combat.Reverse() : combat);
    }
    internal static string Serialize(object? value) => JsonSerializer.Serialize(value);
    internal static string Snapshot(AttackBattleSnapshot state) => Serialize(new { state.Combatants, state.TurnBattle.Turns,
        state.TurnBattle.Battle.Map, state.TurnBattle.Battle.RngState,
        Bodies = state.TurnBattle.Battle.Units.Select(u => new { u.Id, u.Position, u.HasMoved, u.MovementProfile.Move }) });
}
