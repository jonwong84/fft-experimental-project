using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Immutable move-only projection and absolute score; grants no turn or command authority.</summary>
public sealed record AiMovementBaselineOutcome(BattleUnitId ActorId, BattleUnitId TargetId,
    TilePosition Destination, int TurnEndCt, ImmutableArray<AiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks,
    ImmutableArray<AiUpkeepPick> UpkeepPicks, AiCandidateScore Score);

internal sealed record AiMovementSimulationResult(AiSimulationClockRejection Rejection,
    AiMovementBaselineOutcome? Outcome, FftRandomState RngState);

/// <summary>Shared move-only arithmetic after action-family admission. Never consumes ACT.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-135/189.</remarks>
internal static class AiMovementSimulation
{
    internal static AiMovementSimulationResult Evaluate(AttackBattleSnapshot state, BattleUnitId actorId,
        BattleUnitId targetId, TilePosition destination, ImmutableArray<AiUnitBaseline> baselines, FftRandomState decisionRng)
    {
        var turns = state.TurnBattle.Turns;
        var actorTurn = turns.Units.Single(u => u.Id == actorId);
        var battle = state.TurnBattle.Battle;
        battle.TryGetUnit(actorId, out var body);
        bool moved = actorTurn.MovementTaken || destination != body!.Position;
        // Source turn_end uses u16 arithmetic before the byte store; refund only genuinely unused choices.
        int turnEndCt = Math.Min(60, actorTurn.Ct + (actorTurn.ActionTaken ? 0 : 20) + (moved ? 0 : 20));
        var units = turns.Units.OrderBy(u => u.TieKey).Select(turn =>
        {
            var combat = state.Combatants.Single(u => u.UnitId == turn.Id);
            battle.TryGetUnit(turn.Id, out var unitBody);
            bool isActor = turn.Id == actorId;
            return new AiProjectedUnit(turn.Id, isActor ? destination : unitBody!.Position, combat.Facing,
                combat.Hp, combat.MaxHp, combat.Status, baselines.Single(u => u.UnitId == turn.Id).DeathCounter,
                turn.Speed, turn.TieKey, isActor ? turnEndCt : turn.Ct,
                isActor ? moved : turn.MovementTaken, turn.ActionTaken);
        }).ToArray();
        var rejection = AiSimulationClock.Advance(units, turns.Ticks, decisionRng, out var clock);
        if (rejection != AiSimulationClockRejection.None) return new(rejection, null, decisionRng);
        var score = AiCandidateScoring.ScoreFreshUnits(clock!.Units, targetId, 100);
        // Negating into the source u16 grid and back to s16 preserves this closed score.
        return new(AiSimulationClockRejection.None,
            new(actorId, targetId, destination, turnEndCt, clock.Units, clock.MoveOpportunityUnitId,
                clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks, score), clock.RngState);
    }
}
