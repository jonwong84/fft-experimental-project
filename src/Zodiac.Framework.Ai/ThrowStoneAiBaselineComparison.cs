namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented original baseline provenance; reachability and ability IDs cannot establish these facts.</summary>
public sealed record ThrowStoneAiBaselineProfile(bool OriginalScenarioZeroDestination = false,
    bool FreshSourceScoreCell = false, bool NormalScoring = false, bool CanonicalCoordinates = false);

public enum ThrowStoneAiBaselineComparisonStage { Baseline, Candidate }
public enum ThrowStoneAiBaselineComparisonRejection
{
    UnsupportedBaselineProfile, UnsupportedScoreProfile, UnknownActor, UnknownTarget, TerminalInput,
    UnavailableAction, UnsupportedProfile, InvalidBaselineDestination, RandomDrawCountExhausted,
    TickCountExhausted, CandidateRejected
}
public sealed record ThrowStoneAiBaselineComparisonFailure(ThrowStoneAiBaselineComparisonStage Stage,
    ThrowStoneAiBaselineComparisonRejection Reason,
    AttackCommandRejection AbilityRejection = AttackCommandRejection.None,
    AiScoringRejection ScoringRejection = AiScoringRejection.None,
    ThrowStoneAiCandidateRejection CandidateRejection = ThrowStoneAiCandidateRejection.None);

/// <summary>Rank is absent for a supported NotValuable candidate. Both fresh markers are zero; baseline hit is 100.</summary>
public sealed record ThrowStoneAiBaselineComparisonOutcome(AiMovementBaselineOutcome Baseline,
    ThrowStoneAiScoringResult Candidate, AiRankOrder? Rank);

/// <summary>Failure exposes reasons only and restores whole-query RNG, including a late candidate failure.</summary>
/// <remarks>Rollback is managed unsupported/exhaustion policy, not an original search rewind.</remarks>
public sealed record ThrowStoneAiBaselineComparisonResult(bool Accepted, ThrowStoneAiBaselineComparisonFailure? Failure,
    ThrowStoneAiBaselineComparisonOutcome? Outcome, FftRandomState RngState);

/// <summary>Own one certified fresh move-only baseline, then one fresh supplied Throw Stone candidate on original state.</summary>
/// <remarks>
/// Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-185/189.
/// Isolated evaluation costs, not a full search RNG schedule. No cached input, scan, ranked insertion, lottery or authority.
/// </remarks>
public static class ThrowStoneAiBaselineComparison
{
    public static ThrowStoneAiBaselineComparisonResult Evaluate(AttackBattleSnapshot state, TilePosition baselineDestination,
        ThrowStoneAiCandidate candidate, ThrowStoneAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        ThrowStoneAiBaselineProfile baselineProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidateProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(baselineProfile);
        ThrowStoneAiBaselineComparisonResult Reject(ThrowStoneAiBaselineComparisonRejection reason,
            AttackCommandRejection ability = AttackCommandRejection.None) =>
            new(false, new(ThrowStoneAiBaselineComparisonStage.Baseline, reason, ability), null, decisionRng);
        if (!baselineProfile.OriginalScenarioZeroDestination || !baselineProfile.FreshSourceScoreCell ||
            !baselineProfile.NormalScoring || !baselineProfile.CanonicalCoordinates)
            return Reject(ThrowStoneAiBaselineComparisonRejection.UnsupportedBaselineProfile);
        if (!scoreProfile.Supports(state, candidateProfile.Units.Select(u => u.UnitId)))
            return Reject(ThrowStoneAiBaselineComparisonRejection.UnsupportedScoreProfile);
        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == candidate.ActorId);
        if (actor is null) return Reject(ThrowStoneAiBaselineComparisonRejection.UnknownActor);
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == candidate.TargetId);
        if (target is null) return Reject(ThrowStoneAiBaselineComparisonRejection.UnknownTarget);
        if (actor.UnitId == target.UnitId) return Reject(ThrowStoneAiBaselineComparisonRejection.UnsupportedProfile);
        if (state.Combatants.Any(u => u.Hp == 0)) return Reject(ThrowStoneAiBaselineComparisonRejection.TerminalInput);
        var turns = state.TurnBattle.Turns;
        var actorTurn = turns.Units.Single(u => u.Id == actor.UnitId);
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actor.UnitId || actorTurn.ActionTaken)
            return Reject(ThrowStoneAiBaselineComparisonRejection.UnavailableAction);

        // This pure query certifies the ability/grant/setup without requiring range from the original tile:
        // a move-first candidate can enter range later. Its fresh projection validates the actual cast origin.
        var ability = TargetedAbilityBattleSimulator.ValidateCommand(state,
            new(candidate.ActorId, candidate.AbilityId, candidate.TargetId), candidateProfile.Ability);
        if (ability is not (AttackCommandRejection.None or AttackCommandRejection.OutOfRange))
            return Reject(ThrowStoneAiBaselineComparisonRejection.UnsupportedProfile, ability);
        var movement = FlatOrdinaryMovement.Evaluate(state, actor.UnitId, candidateProfile.Movement);
        if (!movement.Accepted) return Reject(ThrowStoneAiBaselineComparisonRejection.UnsupportedProfile);
        if (!movement.Outcome!.AiOrigins.Contains(baselineDestination))
            return Reject(ThrowStoneAiBaselineComparisonRejection.InvalidBaselineDestination);

        var baseline = AiMovementSimulation.Evaluate(state, actor.UnitId, target.UnitId, baselineDestination,
            candidateProfile.Units, decisionRng);
        if (baseline.Rejection != AiSimulationClockRejection.None)
            return Reject(baseline.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                ? ThrowStoneAiBaselineComparisonRejection.RandomDrawCountExhausted
                : ThrowStoneAiBaselineComparisonRejection.TickCountExhausted);

        // No hypothetical unit state leaks between evaluations. Only their actual private RNG is carried.
        var evaluation = ThrowStoneAiCandidateScoring.Evaluate(state, candidate, candidateProfile, scoreProfile, baseline.RngState);
        if (evaluation.Disposition == AiScoringDisposition.Rejected)
            return new(false, new(ThrowStoneAiBaselineComparisonStage.Candidate,
                ThrowStoneAiBaselineComparisonRejection.CandidateRejected,
                evaluation.Projection?.FormulaRejection ?? AttackCommandRejection.None,
                evaluation.Rejection, evaluation.Projection?.Rejection ?? ThrowStoneAiCandidateRejection.None), null, decisionRng);
        AiRankOrder? rank = evaluation.Score is { } score ? AiCandidateRank.Compare(score, baseline.Outcome!.Score) : null;
        return new(true, null, new(baseline.Outcome!, evaluation, rank), evaluation.RngState);
    }
}
