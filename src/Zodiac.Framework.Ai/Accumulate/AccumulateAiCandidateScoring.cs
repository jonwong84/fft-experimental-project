using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record AccumulateAiScoringResult(AiScoringDisposition Disposition, AiScoringRejection Rejection,
    AccumulateAiCandidateResult? Projection, AiCandidateScore? Score, RandomState RngState);

/// <summary>Fresh self projection and source allied-stat-hit credit followed by absolute scoring; no cached result inputs.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-229/230. PA99 retains +3/hit100.</remarks>
public static class AccumulateAiCandidateScoring
{
    public static AccumulateAiScoringResult Evaluate(AttackBattleSnapshot state, AccumulateAiCandidate candidate,
        AccumulateAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, RandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidateProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        if (!scoreProfile.Supports(state, candidateProfile.Units.Select(u => u.UnitId)))
            return new(AiScoringDisposition.Rejected, AiScoringRejection.UnsupportedScoreProfile, null, null, decisionRng);
        var projection = AccumulateAiCandidateProjection.Evaluate(state, candidate, candidateProfile, decisionRng);
        if (!projection.Accepted)
            return new(AiScoringDisposition.Rejected, AiScoringRejection.ProjectionRejected, projection, null, decisionRng);
        return new(AiScoringDisposition.Scored, AiScoringRejection.None, projection,
            AiCandidateScoring.ScoreFreshUnits(projection.Outcome!.Units, candidate.OpponentId, 100, initialPriority: 3), projection.RngState);
    }
}
