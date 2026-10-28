using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Unrepresented source facts; none are inferred from IDs or a supplied destination.</summary>
/// <remarks>Ordinary policy excludes stay-near, reflected/ranged/crystal overrides and special search modes.</remarks>
public sealed record SwordAiMovementScenarioProfile(bool FocusedOpponent = false,
    bool OrdinarySwordReach = false, bool OrdinaryScenarioPolicy = false);

public enum SwordAiMovementScenarioStage { Admission, Movement }
public enum SwordAiMovementScenarioRejection { UnsupportedProfile, AdmissionRejected, OutOfReach, EvaluationRejected }
public sealed record SwordAiMovementScenarioFailure(SwordAiMovementScenarioRejection Reason,
    SwordAiMovementScenarioStage Stage, int? Index = null,
    SwordAiMovementBaselineRejection MovementReason = SwordAiMovementBaselineRejection.None);
public sealed record SwordAiMovementTileOrdering(TilePosition Tile, byte TargetDistance, byte Proximity, int PackedScore);
public sealed record SwordAiMovementScenarioOutcome(SwordAiCompositionScenario Scenario,
    ImmutableArray<SwordAiScoredMovementTile> Movement, int BaselineIndex,
    ImmutableArray<SwordAiMovementTileOrdering> Ordering, int TargetDistance, int TargetReach, bool BaselineUsedDistanceFallback);
/// <summary>Failure publishes no partial scenario/evaluations and returns the incoming decision RNG.</summary>
public sealed record SwordAiMovementScenarioResult(bool Accepted, SwordAiMovementScenarioFailure? Failure,
    SwordAiMovementScenarioOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh, pure derivation of the focused in-reach ordinary-sword general-search movement branch.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-141/143. No Attack, lottery or command execution.</remarks>
public static class SwordAiMovementScenario
{
    public static SwordAiMovementScenarioResult Evaluate(AttackBattleSnapshot state,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiMovementScenarioProfile scenarioProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        SwordAiMovementScenarioResult Reject(SwordAiMovementScenarioRejection reason,
            SwordAiMovementScenarioStage stage = SwordAiMovementScenarioStage.Admission, int? index = null,
            SwordAiMovementBaselineRejection movementReason = SwordAiMovementBaselineRejection.None) =>
            new(false, new(reason, stage, index, movementReason), null, decisionRng);
        if (!scenarioProfile.FocusedOpponent || !scenarioProfile.OrdinarySwordReach || !scenarioProfile.OrdinaryScenarioPolicy ||
            !comparisonProfile.FreshDecisionBaseline)
            return Reject(SwordAiMovementScenarioRejection.UnsupportedProfile);
        var battle = state.TurnBattle.Battle;
        if (!battle.TryGetUnit(comparisonProfile.ActorId, out var actor))
            return Reject(SwordAiMovementScenarioRejection.AdmissionRejected, movementReason: SwordAiMovementBaselineRejection.UnknownActor);
        var invalid = SwordAiMovementBaseline.ValidateInputs(state,
            new(comparisonProfile.ActorId, comparisonProfile.TargetId, actor!.Position), candidateProfile, scoreProfile);
        if (invalid != SwordAiMovementBaselineRejection.None)
            return Reject(SwordAiMovementScenarioRejection.AdmissionRejected, movementReason: invalid);
        // Source reach adds RAW Move; admit only where it equals the spread's effective Move cap.
        if (actor.Move > 124) return Reject(SwordAiMovementScenarioRejection.UnsupportedProfile);
        var geometry = FlatOrdinaryMovement.Evaluate(state, actor.Id, candidateProfile.Movement).Outcome!;
        byte Distance(TilePosition p) => geometry.TargetDistanceRows.Single(row => row.Y == p.Y).Distances[p.X];
        int distance = Distance(actor.Position), reach = actor.Move + 1;
        if (distance > reach) return Reject(SwordAiMovementScenarioRejection.OutOfReach);
        var origins = geometry.AiOrigins.Where(p => Distance(p) <= actor.Move + distance).ToImmutableArray();
        if (origins.IsEmpty) origins = geometry.AiOrigins; // Original no-match filter copies scenario 0.

        var turns = state.TurnBattle.Turns.Units;
        static int Delay(TurnUnitState unit) => (Math.Max(0, 100 - unit.Ct) + unit.Speed - 1) / unit.Speed;
        bool imminent = Delay(turns.Single(u => u.Id == comparisonProfile.TargetId)) <= Delay(turns.Single(u => u.Id == actor.Id));
        var movement = ImmutableArray.CreateBuilder<SwordAiScoredMovementTile>(origins.Length);
        var ordering = ImmutableArray.CreateBuilder<SwordAiMovementTileOrdering>(origins.Length);
        var rng = decisionRng;
        foreach (var tile in origins) // Existing geometry emits layer/y/x.
        {
            var evaluation = SwordAiMovementBaseline.EvaluateFreshTile(state,
                new(actor.Id, comparisonProfile.TargetId, tile), candidateProfile, scoreProfile, rng);
            if (!evaluation.Accepted)
                return Reject(SwordAiMovementScenarioRejection.EvaluationRejected, SwordAiMovementScenarioStage.Movement,
                    movement.Count, evaluation.Rejection);
            movement.Add(new(evaluation.Outcome!, evaluation.RngState)); rng = evaluation.RngState;
            byte proximity = imminent ? (byte)Math.Max(0, 10 - Distance(tile)) : (byte)0;
            ushort pattern = unchecked((ushort)-evaluation.Outcome!.Score.Priority);
            ordering.Add(new(tile, Distance(tile), proximity, unchecked((pattern << 16) + proximity)));
        }
        var values = ordering.ToImmutable();
        TilePosition Select(IEnumerable<SwordAiMovementTileOrdering> cells, bool distanceFirst) =>
            (distanceFirst
                ? cells.OrderBy(c => c.TargetDistance).ThenBy(c => c.PackedScore)
                : cells.OrderBy(c => c.PackedScore).ThenBy(c => c.TargetDistance))
            .ThenByDescending(c => c.Tile.Layer).ThenByDescending(c => c.Tile.Y).ThenByDescending(c => c.Tile.X).First().Tile;
        var d1 = Select(values, false);
        var closer = values.Where(c => c.TargetDistance <= distance - 1).ToArray();
        bool fallback = closer.Length == 0;
        var d0 = Select(fallback ? values : closer, fallback);
        var scenario = new SwordAiCompositionScenario(origins, d0, d1);
        return new(true, null, new(scenario, movement.ToImmutable(), origins.IndexOf(d0), values, distance, reach, fallback), rng);
    }
}
