using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Source-selected pre-scoring mask S and destinations D0/D1; copies caller storage.</summary>
public sealed class SwordAiCompositionScenario
{
    public ImmutableArray<TilePosition> MovementOrigins { get; }
    public TilePosition BaselineDestination { get; }
    public TilePosition AttackDestination { get; }

    public SwordAiCompositionScenario(IEnumerable<TilePosition> movementOrigins, TilePosition baselineDestination, TilePosition attackDestination)
    {
        ArgumentNullException.ThrowIfNull(movementOrigins);
        MovementOrigins = movementOrigins.ToImmutableArray();
        BaselineDestination = baselineDestination;
        AttackDestination = attackDestination;
    }
}

/// <summary>Unrepresented original source facts, not packed flags; reachability alone cannot prove them.</summary>
public sealed record SwordAiCompositionProfile(bool OriginalMovementScoreMask = false,
    bool NoInterveningRandomWork = false, bool FreshEmptyRankedList = false);

public enum SwordAiCompositionStage { Admission, Movement, Attack }
public enum SwordAiCompositionRejection { UnsupportedProfile, InvalidScenario, AdmissionRejected, EvaluationRejected }
public enum SwordAiAdmission { NoScoredCandidate, NotAboveBaseline, Admitted }

public sealed record SwordAiCompositionFailure(SwordAiCompositionRejection Reason, SwordAiCompositionStage Stage,
    int? Index = null, SwordAiMovementBaselineRejection MovementReason = SwordAiMovementBaselineRejection.None,
    SwordAiScanRejection ScanReason = SwordAiScanRejection.None, AiScoringRejection ScoringReason = AiScoringRejection.None,
    SwordAiCandidateRejection ProjectionReason = SwordAiCandidateRejection.None);
public sealed record SwordAiScoredMovementTile(SwordAiMovementBaselineOutcome Outcome, FftRandomState RngState);

/// <summary>Eight source slots with explicit empties; this one-pass subset admits at most one real action.</summary>
public sealed record SwordAiCompositionOutcome(ImmutableArray<SwordAiScoredMovementTile> Movement, int BaselineIndex,
    SwordAiScanScenario AttackScenario, SwordAiScanResult Scan, SwordAiAdmission Admission,
    ImmutableArray<SwordAiScannedCandidate?> RankedActions)
{
    public SwordAiScoredMovementTile Baseline => Movement[BaselineIndex];
}

/// <summary>Rejection publishes no partial evaluations/cache/list and restores the incoming hypothetical RNG.</summary>
public sealed record SwordAiCompositionResult(bool Accepted, SwordAiCompositionFailure? Failure,
    SwordAiCompositionOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure opt-in source-ordered movement prefix, one ordinary Attack scan and ranked admission.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, ZOD-137/138.
/// Starts before the supplied movement scoring mask; stops before lottery/fallback/commands.
/// No scenario generation, caller-populated cached outcomes, live RNG commitment or native activation.
/// </remarks>
public static class SwordAiComposition
{
    public static SwordAiCompositionResult Evaluate(AttackBattleSnapshot state, SwordAiCompositionScenario scenario,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiMovementBaselineProfile baselineProfile,
        SwordAiScanProfile scanProfile, SwordAiCompositionProfile compositionProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(baselineProfile);
        ArgumentNullException.ThrowIfNull(scanProfile);
        ArgumentNullException.ThrowIfNull(compositionProfile);
        SwordAiCompositionResult Reject(SwordAiCompositionFailure failure) => new(false, failure, null, decisionRng);
        if (!compositionProfile.OriginalMovementScoreMask || !compositionProfile.NoInterveningRandomWork || !compositionProfile.FreshEmptyRankedList)
            return Reject(new(SwordAiCompositionRejection.UnsupportedProfile, SwordAiCompositionStage.Admission));
        if (!SwordAiMovementBaseline.SupportsProfile(baselineProfile))
            return Reject(new(SwordAiCompositionRejection.UnsupportedProfile, SwordAiCompositionStage.Admission,
                MovementReason: SwordAiMovementBaselineRejection.UnsupportedBaselineProfile));

        var mask = scenario.MovementOrigins.ToHashSet();
        if (mask.Count == 0 || mask.Count != scenario.MovementOrigins.Length ||
            !mask.Contains(scenario.BaselineDestination) || !mask.Contains(scenario.AttackDestination))
            return Reject(new(SwordAiCompositionRejection.InvalidScenario, SwordAiCompositionStage.Admission));
        var attackMask = new HashSet<TilePosition>(mask);
        if (state.TurnBattle.Battle.TryGetUnit(comparisonProfile.ActorId, out var actor)) attackMask.Add(actor!.Position);
        var attackScenario = new SwordAiScanScenario(attackMask.OrderBy(p => p.Layer).ThenBy(p => p.Y).ThenBy(p => p.X), scenario.AttackDestination);
        var invalid = SwordAiCandidateScan.ValidateInputs(state, attackScenario, weapon, candidateProfile, scoreProfile,
            comparisonProfile, scanProfile, decisionRng);
        if (invalid is not null)
            return Reject(new(SwordAiCompositionRejection.AdmissionRejected, SwordAiCompositionStage.Admission,
                ScanReason: invalid.Rejection, ScoringReason: invalid.ScoringRejection, ProjectionReason: invalid.ProjectionRejection));

        var movement = ImmutableArray.CreateBuilder<SwordAiScoredMovementTile>(mask.Count);
        int baselineIndex = -1;
        var rng = decisionRng;
        foreach (var destination in mask.OrderBy(p => p.Layer).ThenBy(p => p.Y).ThenBy(p => p.X))
        {
            var result = SwordAiMovementBaseline.EvaluateFreshTile(state,
                new(comparisonProfile.ActorId, comparisonProfile.TargetId, destination), candidateProfile, scoreProfile, rng);
            if (!result.Accepted)
                return Reject(new(SwordAiCompositionRejection.EvaluationRejected, SwordAiCompositionStage.Movement,
                    movement.Count, MovementReason: result.Rejection));
            if (destination == scenario.BaselineDestination) baselineIndex = movement.Count;
            movement.Add(new(result.Outcome!, result.RngState));
            rng = result.RngState;
        }
        return ContinueFromMovement(state, movement.ToImmutable(), baselineIndex, attackScenario, weapon,
            candidateProfile, scoreProfile, comparisonProfile, scanProfile, rng, decisionRng);
    }

    // Entry points own fresh movement admission/evaluation. Never exposed as a cached-result API.
    internal static SwordAiCompositionResult ContinueFromMovement(AttackBattleSnapshot state,
        ImmutableArray<SwordAiScoredMovementTile> movement, int baselineIndex, SwordAiScanScenario attackScenario,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiScanProfile scanProfile, FftRandomState rng, FftRandomState queryRng)
    {
        SwordAiCompositionResult Reject(SwordAiCompositionFailure failure) => new(false, failure, null, queryRng);
        var baseline = movement[baselineIndex].Outcome.Score; // Already-scored cell; no extra evaluation/draw.
        var scan = SwordAiCandidateScan.Evaluate(state, attackScenario, weapon, candidateProfile, scoreProfile,
            comparisonProfile, scanProfile, rng);
        if (scan.Disposition == SwordAiScanDisposition.Rejected)
            return Reject(new(SwordAiCompositionRejection.EvaluationRejected, SwordAiCompositionStage.Attack, scan.FailedCandidateIndex,
                ScanReason: scan.Rejection, ScoringReason: scan.ScoringRejection, ProjectionReason: scan.ProjectionRejection));

        // Original compatibility capacity; independent of managed roster/content sizes.
        var ranked = new SwordAiScannedCandidate?[8];
        SwordAiAdmission admission;
        // Working best is scoped to this single insertion attempt and is discarded on every exit.
        if (scan.Outcome!.RetainedCandidateIndex is not { } bestIndex) admission = SwordAiAdmission.NoScoredCandidate;
        else admission = InsertFreshBest(scan.Outcome.Candidates[bestIndex], baseline, ranked);
        return new(true, null, new(movement, baselineIndex, attackScenario, scan, admission, ranked.ToImmutableArray()), scan.RngState);
    }

    private static SwordAiAdmission InsertFreshBest(SwordAiScannedCandidate best, AiCandidateScore baseline,
        SwordAiScannedCandidate?[] ranked)
    {
        var score = best.Evaluation.Score!;
        // Markers are 0, baseline hit=100, candidate hit<=100: rank-only gate requires greater signed priority.
        if (AiCandidateRank.Compare(score, baseline) != AiRankOrder.Above) return SwordAiAdmission.NotAboveBaseline;
        bool HigherThan(SwordAiScannedCandidate? entry) => entry is null ||
            SwordAiCandidateScan.IsHigher(score, best.Proximity, entry.Evaluation.Score!, entry.Proximity);
        if (!HigherThan(ranked[^1])) return SwordAiAdmission.NotAboveBaseline;
        for (int i = 0; i < ranked.Length; i++)
        {
            if (!HigherThan(ranked[i])) continue;
            for (int j = ranked.Length - 1; j > i; j--) ranked[j] = ranked[j - 1];
            ranked[i] = best; // Full immutable action/diagnostics; no identity or destination deduplication.
            return SwordAiAdmission.Admitted;
        }
        return SwordAiAdmission.NotAboveBaseline;
    }
}
