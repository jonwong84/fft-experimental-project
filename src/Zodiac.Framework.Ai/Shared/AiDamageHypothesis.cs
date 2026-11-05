using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

internal sealed record AiDamageHypothesis(int TurnEndCt, AiClockOutcome Clock, AiCandidateScore Score);

/// <summary>Complete one admitted selected-target HP hypothesis through the existing all-unit clock and score.</summary>
/// <remarks>Callers retain formula/range/trajectory admission and their original failure/RNG contracts.</remarks>
internal static class AiDamageHypotheses
{
    internal static AiSimulationClockRejection Complete(AttackBattleSnapshot state, BattleUnitId actorId, BattleUnitId targetId,
        TilePosition destination, int targetHp, byte hitPercent, RosterAiSimulationProfile simulation,
        RandomState formulaRng, out AiDamageHypothesis? result)
    {
        result = null;
        var turn = state.TurnBattle.Turns.Units.Single(u => u.Id == actorId);
        var origin = state.TurnBattle.Battle.Units.Single(u => u.Id == actorId).Position;
        bool moved = turn.MovementTaken || destination != origin;
        int end = Math.Min(60, turn.Ct + (moved ? 0 : 20));
        var units = AiCandidateState.CreateUnits(state, actorId, targetId, destination, targetHp, end, moved, simulation.Units);
        var reason = AiSimulationClock.Advance(units, state.TurnBattle.Turns.Ticks, formulaRng, out var clock);
        if (reason != AiSimulationClockRejection.None) return reason;
        int team = simulation.Battle.Units[actorId].StableTeam;
        var enemies = simulation.Battle.Members.Where(u => u.StableTeam != team).Select(u => u.UnitId).ToImmutableHashSet();
        result = new(end, clock!, AiCandidateScoring.ScoreFreshUnits(clock!.Units, enemies, hitPercent));
        return AiSimulationClockRejection.None;
    }
}
