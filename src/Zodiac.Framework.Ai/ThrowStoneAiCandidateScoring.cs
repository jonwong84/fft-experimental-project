namespace Zodiac.Framework.Ai;

public sealed record ThrowStoneAiScoringResult(AiScoringDisposition Disposition, AiScoringRejection Rejection,
    ThrowStoneAiCandidateResult? Projection, AiCandidateScore? Score, FftRandomState RngState);

/// <summary>Own one fresh Throw Stone candidate and apply shared absolute source scoring without additional RNG.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-185/188. No cached outcome, comparison, scan or authority.</remarks>
public static class ThrowStoneAiCandidateScoring
{
    public static ThrowStoneAiScoringResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiCandidate candidate,
        ThrowStoneAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidateProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        if (!scoreProfile.Supports(state, candidateProfile.Units.Select(u => u.UnitId)))
            return new(AiScoringDisposition.Rejected, AiScoringRejection.UnsupportedScoreProfile, null, null, decisionRng);
        var projection = ThrowStoneAiCandidateProjection.Evaluate(state, candidate, candidateProfile, decisionRng);
        if (projection.Disposition == ThrowStoneAiCandidateDisposition.Rejected)
            return new(AiScoringDisposition.Rejected, AiScoringRejection.ProjectionRejected, projection, null, projection.RngState);
        if (projection.Disposition == ThrowStoneAiCandidateDisposition.NotValuable)
            return new(AiScoringDisposition.NotValuable, AiScoringRejection.None, projection, null, projection.RngState);
        var outcome = projection.Outcome!;
        return new(AiScoringDisposition.Scored, AiScoringRejection.None, projection,
            AiCandidateScoring.ScoreFreshUnits(outcome.Units, outcome.TargetId, (byte)outcome.HitPercent), projection.RngState);
    }
}
