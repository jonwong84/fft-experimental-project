using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record ThrowStoneAiScanRequest(BattleUnitId ActorId, AssetId AbilityId, BattleUnitId TargetId);

/// <summary>Caller-certified movement score mask, scenario mask and D0/D1. Copies caller storage.</summary>
public sealed class ThrowStoneAiScanScenario
{
    public ImmutableArray<TilePosition> ScoredOrigins { get; }
    public ImmutableArray<TilePosition> ScenarioOrigins { get; }
    public TilePosition BaselineDestination { get; }
    public TilePosition ActFirstDestination { get; }
    public ThrowStoneAiScanScenario(IEnumerable<TilePosition> scoredOrigins, IEnumerable<TilePosition> scenarioOrigins,
        TilePosition baselineDestination, TilePosition actFirstDestination)
    {
        ArgumentNullException.ThrowIfNull(scoredOrigins); ArgumentNullException.ThrowIfNull(scenarioOrigins);
        ScoredOrigins = scoredOrigins.ToImmutableArray(); ScenarioOrigins = scenarioOrigins.ToImmutableArray();
        BaselineDestination = baselineDestination; ActFirstDestination = actFirstDestination;
    }
}

/// <summary>Original source-context assertions, not packed flags; stable IDs and range alone cannot prove them.</summary>
/// <remarks>PreparedThrowStoneEntry certifies a usable default-menu, instant direct enemy entry, ranked origins,
/// consume-aim policy and no self/reflect/FollowTarget/random-use pass. Clear paths include original sprite collision facts.</remarks>
public sealed record ThrowStoneAiScanProfile(bool TargetActive, bool OriginalMovementScoreMask = false,
    bool OriginalScenarioMask = false, bool PreparedThrowStoneEntry = false, bool FreshDecisionBaseline = false,
    bool FreshAbilityBestReset = false, bool NoInterveningRandomWork = false, bool ClearDirectTrajectories = false);

public enum ThrowStoneAiScanDisposition { Rejected, NoCandidates, NotValuable, Scored }
public enum ThrowStoneAiScanStage { Admission, Movement, Candidate }
public enum ThrowStoneAiScanRejection { UnsupportedScanProfile, InvalidScenario, AdmissionRejected, EvaluationRejected }
public sealed record ThrowStoneAiScanFailure(ThrowStoneAiScanStage Stage, ThrowStoneAiScanRejection Reason,
    int? MovementIndex = null, ThrowStoneAiBaselineComparisonFailure? Detail = null);

public sealed record ThrowStoneAiScannedCandidate(TilePosition Origin, TilePosition TargetPanel,
    ThrowStoneAiCandidate Candidate, ThrowStoneAiScoringResult Evaluation);
public sealed record ThrowStoneAiScanOutcome(ImmutableArray<AiScoredMovementTile> Movement, int BaselineIndex,
    ThrowStoneAiScannedCandidate? Candidate, AiRankOrder? Rank)
{
    public AiMovementBaselineOutcome Baseline => Movement[BaselineIndex].Outcome;
}

/// <summary>Failure publishes reasons only and restores whole-query RNG, including late prefix/candidate failures.</summary>
public sealed record ThrowStoneAiScanResult(ThrowStoneAiScanDisposition Disposition, ThrowStoneAiScanFailure? Failure,
    ThrowStoneAiScanOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure fresh movement prefix and one prepared Throw Stone scan in certified clear two-opponent geometry.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-190/191. No cached input, scenario derivation,
/// obstructed-path traversal, ability-list/ranked insertion/lottery, command authority or live RNG adoption.</remarks>
public static class ThrowStoneAiCandidateScan
{
    public static ThrowStoneAiScanResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiScanRequest request,
        ThrowStoneAiScanScenario scenario, ThrowStoneAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        ThrowStoneAiBaselineProfile baselineProfile, ThrowStoneAiScanProfile scanProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(candidateProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(baselineProfile); ArgumentNullException.ThrowIfNull(scanProfile);
        var invalid = ValidateInputs(state, request, scenario, candidateProfile, scoreProfile, baselineProfile, scanProfile, decisionRng);
        if (invalid is not null) return invalid;
        var prefix = AiMovementPrefix.Evaluate(state, request.ActorId, request.TargetId, scenario.ScoredOrigins,
            scenario.BaselineDestination, candidateProfile.Units, decisionRng);
        if (prefix.Rejection != AiSimulationClockRejection.None)
            return new(ThrowStoneAiScanDisposition.Rejected, new(ThrowStoneAiScanStage.Movement, ThrowStoneAiScanRejection.EvaluationRejected,
                prefix.FailedIndex, new(ThrowStoneAiBaselineComparisonStage.Baseline,
                    prefix.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                        ? ThrowStoneAiBaselineComparisonRejection.RandomDrawCountExhausted : ThrowStoneAiBaselineComparisonRejection.TickCountExhausted)), null, decisionRng);
        return ContinueFromMovement(state, request, scenario, candidateProfile, scoreProfile, scanProfile,
            prefix.Outcome!, prefix.RngState, decisionRng);
    }

    internal static ThrowStoneAiScanResult? ValidateInputs(AttackBattleSnapshot state, ThrowStoneAiScanRequest request,
        ThrowStoneAiScanScenario scenario, ThrowStoneAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        ThrowStoneAiBaselineProfile baselineProfile, ThrowStoneAiScanProfile scanProfile, FftRandomState decisionRng)
    {
        ThrowStoneAiScanResult Reject(ThrowStoneAiScanStage stage, ThrowStoneAiScanRejection reason,
            int? index = null, ThrowStoneAiBaselineComparisonFailure? detail = null) =>
            new(ThrowStoneAiScanDisposition.Rejected, new(stage, reason, index, detail), null, decisionRng);
        if (!scanProfile.OriginalMovementScoreMask || !scanProfile.OriginalScenarioMask || !scanProfile.PreparedThrowStoneEntry ||
            !scanProfile.FreshDecisionBaseline || !scanProfile.FreshAbilityBestReset ||
            !scanProfile.NoInterveningRandomWork || !scanProfile.ClearDirectTrajectories)
            return Reject(ThrowStoneAiScanStage.Admission, ThrowStoneAiScanRejection.UnsupportedScanProfile);
        var template = new ThrowStoneAiCandidate(request.ActorId, request.AbilityId, request.TargetId, scenario.ActFirstDestination, true);
        var invalid = ThrowStoneAiBaselineComparison.ValidateInputs(state, scenario.BaselineDestination, template,
            candidateProfile, scoreProfile, baselineProfile);
        if (invalid is not null) return Reject(ThrowStoneAiScanStage.Admission, ThrowStoneAiScanRejection.AdmissionRejected, detail: invalid);
        var scoredMask = scenario.ScoredOrigins.ToHashSet(); var scenarioMask = scenario.ScenarioOrigins.ToHashSet();
        var movement = FlatOrdinaryMovement.Evaluate(state, request.ActorId, candidateProfile.Movement);
        if (scoredMask.Count == 0 || scoredMask.Count != scenario.ScoredOrigins.Length ||
            scenarioMask.Count != scenario.ScenarioOrigins.Length || !scoredMask.Contains(scenario.BaselineDestination) ||
            !scoredMask.Contains(scenario.ActFirstDestination) || !scenarioMask.IsSubsetOf(scoredMask) ||
            scenarioMask.Count != 0 && !scenarioMask.Contains(scenario.ActFirstDestination) ||
            !scoredMask.IsSubsetOf(movement.Outcome!.AiOrigins))
            return Reject(ThrowStoneAiScanStage.Admission, ThrowStoneAiScanRejection.InvalidScenario);

        return null;
    }

    // Internal fresh ownership only: shared prefix is never a public cached-result input.
    internal static ThrowStoneAiScanResult ContinueFromMovement(AttackBattleSnapshot state, ThrowStoneAiScanRequest request,
        ThrowStoneAiScanScenario scenario, ThrowStoneAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        ThrowStoneAiScanProfile scanProfile, AiMovementPrefixOutcome prefix, FftRandomState rng, FftRandomState queryRng)
    {
        var tiles = prefix.Movement; int baselineIndex = prefix.BaselineIndex;
        var scenarioMask = scenario.ScenarioOrigins.ToHashSet();
        var battle = state.TurnBattle.Battle;
        battle.TryGetUnit(request.TargetId, out var target); battle.TryGetUnit(request.ActorId, out var actor);
        var targetPosition = target!.Position; var actorPosition = actor!.Position;
        // origin_next_take uses >= while walking layer/y/x ascending: later coordinates win a complete key tie.
        // Instant CT does not exclude an otherwise active enemy, even if its turn is already ready.
        var selected = scanProfile.TargetActive ? tiles.Where(t => scenarioMask.Contains(t.Outcome.Destination) &&
                Math.Abs(t.Outcome.Destination.X - targetPosition.X) + Math.Abs(t.Outcome.Destination.Y - targetPosition.Y) <= 4)
            .OrderBy(t => t.OriginKey).ThenByDescending(t => t.Outcome.Destination.Layer)
            .ThenByDescending(t => t.Outcome.Destination.Y).ThenByDescending(t => t.Outcome.Destination.X).FirstOrDefault() : null;
        if (selected is null)
            return new(ThrowStoneAiScanDisposition.NoCandidates, null, new(tiles, baselineIndex, null, null), rng);
        var origin = selected.Outcome.Destination;
        bool actFirst = origin == actorPosition;
        var candidate = new ThrowStoneAiCandidate(request.ActorId, request.AbilityId, request.TargetId,
            actFirst ? scenario.ActFirstDestination : origin, actFirst);
        // One clear evaluated aim is consumed even for NotValuable; never retry a second origin or rescore D0.
        var comparison = ThrowStoneAiBaselineComparison.ContinueFromBaseline(state, candidate, candidateProfile, scoreProfile,
            tiles[baselineIndex].Outcome, rng, queryRng);
        if (!comparison.Accepted)
            return new(ThrowStoneAiScanDisposition.Rejected, new(ThrowStoneAiScanStage.Candidate,
                ThrowStoneAiScanRejection.EvaluationRejected, Detail: comparison.Failure), null, queryRng);
        var evaluation = comparison.Outcome!.Candidate;
        return new(evaluation.Disposition == AiScoringDisposition.NotValuable ? ThrowStoneAiScanDisposition.NotValuable : ThrowStoneAiScanDisposition.Scored,
            null, new(tiles, baselineIndex, new(origin, targetPosition, candidate, evaluation), comparison.Outcome.Rank), comparison.RngState);
    }
}
