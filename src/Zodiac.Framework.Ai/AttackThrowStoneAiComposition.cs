using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Closed two-family certificates; source order and fresh ranks require explicit opt-in.</summary>
public sealed record AttackThrowStoneAiProfile(SwordAiCandidateProfile Attack, ThrowStoneAiCandidateProfile ThrowStone,
    AiScoreProfile Score, SwordAiScanProfile AttackScan, ThrowStoneAiScanProfile ThrowStoneScan,
    ThrowStoneAiBaselineProfile Baseline, bool SourceAttackThenThrowStone = false, bool FreshEmptyRankedList = false);

/// <summary>Closed hypothetical payloads only. Public records cannot be supplied to planning or command authority.</summary>
public abstract record AttackThrowStoneAiAction
{
    private protected AttackThrowStoneAiAction() { }
    public abstract BattleUnitId ActorId { get; }
    public abstract TilePosition Destination { get; }
    public abstract bool ActBeforeMove { get; }
    public abstract AiCandidateScore Score { get; }
    public abstract byte Proximity { get; }
}
public sealed record AttackThrowStoneAiAttack(SwordAiScannedCandidate Value) : AttackThrowStoneAiAction
{
    public override BattleUnitId ActorId => Value.Candidate.ActorId;
    public override TilePosition Destination => Value.Candidate.Destination;
    public override bool ActBeforeMove => Value.Candidate.Order == SwordAiCandidateOrder.ActThenMove;
    public override AiCandidateScore Score => Value.Evaluation.Score!;
    public override byte Proximity => Value.Proximity;
}
public sealed record AttackThrowStoneAiStone(ThrowStoneAiScannedCandidate Value, byte FoeProximity) : AttackThrowStoneAiAction
{
    public override BattleUnitId ActorId => Value.Candidate.ActorId;
    public override TilePosition Destination => Value.Candidate.Destination;
    public override bool ActBeforeMove => Value.Candidate.ActBeforeMove;
    public override AiCandidateScore Score => Value.Evaluation.Score!;
    public override byte Proximity => FoeProximity;
}
public enum AttackThrowStoneAiStage { Admission, Movement, Attack, ThrowStone }
public enum AttackThrowStoneAiRejection { UnsupportedProfile, MismatchedProfiles, AttackRejected, ThrowStoneRejected, MovementRejected }
public sealed record AttackThrowStoneAiFailure(AttackThrowStoneAiStage Stage, AttackThrowStoneAiRejection Reason,
    int? Index = null, SwordAiScanRejection AttackReason = SwordAiScanRejection.None,
    AiScoringRejection ScoringReason = AiScoringRejection.None, SwordAiCandidateRejection ProjectionReason = SwordAiCandidateRejection.None,
    ThrowStoneAiScanFailure? ThrowStoneFailure = null, SwordAiMovementBaselineRejection MovementReason = SwordAiMovementBaselineRejection.None);
public sealed record AttackThrowStoneAiCompositionOutcome(ImmutableArray<AiScoredMovementTile> Movement, int BaselineIndex,
    SwordAiScanOutcome Attack, ThrowStoneAiScanOutcome ThrowStone, ImmutableArray<AttackThrowStoneAiAction?> RankedActions,
    int? AttackInsertionIndex, int? ThrowStoneInsertionIndex)
{
    public AiMovementBaselineOutcome Baseline => Movement[BaselineIndex].Outcome;
}
/// <summary>Failure has no partial prefix, scan or ranks and restores incoming whole-query RNG.</summary>
public sealed record AttackThrowStoneAiCompositionResult(bool Accepted, AttackThrowStoneAiFailure? Failure,
    AttackThrowStoneAiCompositionOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh movement prefix followed by exactly two certified entries and retained source ranks.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-192/193.
/// No generic list, cached-result input, lottery, command authority or live RNG adoption.</remarks>
public static class AttackThrowStoneAiComposition
{
    public static AttackThrowStoneAiCompositionResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiScanRequest request,
        ThrowStoneAiScanScenario scenario, BoundedSwordWeapon? weapon, AttackThrowStoneAiProfile profile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(profile.Attack); ArgumentNullException.ThrowIfNull(profile.ThrowStone);
        ArgumentNullException.ThrowIfNull(profile.Score); ArgumentNullException.ThrowIfNull(profile.AttackScan);
        ArgumentNullException.ThrowIfNull(profile.ThrowStoneScan); ArgumentNullException.ThrowIfNull(profile.Baseline);
        AttackThrowStoneAiCompositionResult Reject(AttackThrowStoneAiFailure failure) => new(false, failure, null, decisionRng);
        if (!profile.SourceAttackThenThrowStone || !profile.FreshEmptyRankedList)
            return Reject(new(AttackThrowStoneAiStage.Admission, AttackThrowStoneAiRejection.UnsupportedProfile));
        if (profile.Attack.Movement.Ground != profile.ThrowStone.Movement.Ground ||
            profile.AttackScan.TargetActive != profile.ThrowStoneScan.TargetActive ||
            !profile.Attack.Units.OrderBy(u => u.UnitId.Value).SequenceEqual(profile.ThrowStone.Units.OrderBy(u => u.UnitId.Value)))
            return Reject(new(AttackThrowStoneAiStage.Admission, AttackThrowStoneAiRejection.MismatchedProfiles));

        var comparisonProfile = new SwordAiComparisonProfile(request.ActorId, request.TargetId, profile.ThrowStoneScan.FreshDecisionBaseline);
        var attackScenario = new SwordAiScanScenario(scenario.ScenarioOrigins,
            scenario.ScenarioOrigins.IsEmpty ? null : scenario.ActFirstDestination);
        var attackInvalid = SwordAiCandidateScan.ValidateInputs(state, attackScenario, weapon, profile.Attack, profile.Score,
            comparisonProfile, profile.AttackScan, decisionRng);
        if (attackInvalid is not null) return Reject(AttackFailure(AttackThrowStoneAiStage.Admission, attackInvalid));
        var stoneInvalid = ThrowStoneAiCandidateScan.ValidateInputs(state, request, scenario, profile.ThrowStone, profile.Score,
            profile.Baseline, profile.ThrowStoneScan, decisionRng);
        if (stoneInvalid is not null)
            return Reject(new(AttackThrowStoneAiStage.Admission, AttackThrowStoneAiRejection.ThrowStoneRejected, ThrowStoneFailure: stoneInvalid.Failure));

        var prefix = AiMovementPrefix.Evaluate(state, request.ActorId, request.TargetId, scenario.ScoredOrigins,
            scenario.BaselineDestination, profile.ThrowStone.Units, decisionRng);
        if (prefix.Rejection != AiSimulationClockRejection.None)
            return Reject(new(AttackThrowStoneAiStage.Movement, AttackThrowStoneAiRejection.MovementRejected, prefix.FailedIndex,
                MovementReason: prefix.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                    ? SwordAiMovementBaselineRejection.RandomDrawCountExhausted : SwordAiMovementBaselineRejection.TickCountExhausted));
        var fresh = prefix.Outcome!;
        var attack = SwordAiCandidateScan.EvaluateFresh(state, attackScenario, weapon, profile.Attack, profile.Score,
            comparisonProfile, profile.AttackScan, fresh.Proximity, prefix.RngState);
        if (attack.Disposition == SwordAiScanDisposition.Rejected) return Reject(AttackFailure(AttackThrowStoneAiStage.Attack, attack));
        // Source capacity is a compatibility behavior, independent of authored content/roster sizes.
        var ranked = new AttackThrowStoneAiAction?[8];
        int? attackIndex = attack.Outcome!.RetainedCandidateIndex is { } best
            ? Insert(new AttackThrowStoneAiAttack(attack.Outcome.Candidates[best]), fresh.Movement[fresh.BaselineIndex].Outcome.Score, ranked) : null;
        // Each scan owns a fresh working best. Only original units, prefix/cache and actual RNG cross entries.
        var stone = ThrowStoneAiCandidateScan.ContinueFromMovement(state, request, scenario, profile.ThrowStone, profile.Score,
            profile.ThrowStoneScan, fresh, attack.RngState, decisionRng);
        if (stone.Disposition == ThrowStoneAiScanDisposition.Rejected)
            return Reject(new(AttackThrowStoneAiStage.ThrowStone, AttackThrowStoneAiRejection.ThrowStoneRejected, ThrowStoneFailure: stone.Failure));
        int? stoneIndex = stone.Outcome!.Candidate is { Evaluation.Score: not null } candidate
            ? Insert(new AttackThrowStoneAiStone(candidate, fresh.Proximity.Destinations[candidate.Candidate.Destination]),
                fresh.Movement[fresh.BaselineIndex].Outcome.Score, ranked) : null;
        return new(true, null, new(fresh.Movement, fresh.BaselineIndex, attack.Outcome, stone.Outcome,
            ranked.ToImmutableArray(), attackIndex, stoneIndex), stone.RngState);
    }

    private static AttackThrowStoneAiFailure AttackFailure(AttackThrowStoneAiStage stage, SwordAiScanResult result) =>
        new(stage, AttackThrowStoneAiRejection.AttackRejected, result.FailedCandidateIndex, result.Rejection,
            result.ScoringRejection, result.ProjectionRejection);

    private static int? Insert(AttackThrowStoneAiAction best, AiCandidateScore baseline, AttackThrowStoneAiAction?[] ranked)
    {
        if (AiCandidateRank.Compare(best.Score, baseline) != AiRankOrder.Above) return null;
        bool Higher(AttackThrowStoneAiAction? entry) => entry is null || AiCandidateRank.Compare(best.Score, entry.Score) is AiRankOrder.Above ||
            AiCandidateRank.Compare(best.Score, entry.Score) == AiRankOrder.Equal && best.Proximity < entry.Proximity;
        if (!Higher(ranked[^1])) return null;
        for (int i = 0; i < ranked.Length; i++)
        {
            if (!Higher(ranked[i])) continue;
            for (int j = ranked.Length - 1; j > i; j--) ranked[j] = ranked[j - 1];
            ranked[i] = best; return i;
        }
        return null;
    }
}
