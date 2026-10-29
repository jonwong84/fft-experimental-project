using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>No score for rejected or NotValuable inputs. A retained projection is fresh from this invocation.</summary>
/// <remarks>The score stage adds no RNG progress; the complete wrapper returns the projection's stream verbatim.</remarks>
public sealed record SwordAiScoringResult(AiScoringDisposition Disposition, AiScoringRejection Rejection,
    SwordAiCandidateResult? Projection, AiCandidateScore? Score, FftRandomState RngState);

/// <summary>Pure opt-in absolute state scoring of one freshly projected ordinary sword candidate.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-126/127.
/// No external/cached outcome entry, enumeration, comparison, ranking, policy, command or live RNG commitment.
/// </remarks>
public static class SwordAiCandidateScoring
{
    public static SwordAiScoringResult Evaluate(AttackBattleSnapshot state, SwordAiCandidate candidate,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        if (!scoreProfile.Supports(state, candidateProfile))
            return new(AiScoringDisposition.Rejected, AiScoringRejection.UnsupportedScoreProfile, null, null, decisionRng);

        // One call site: provenance comes from original inputs, not a publicly constructible outcome record.
        var projection = SwordAiCandidateProjection.Evaluate(state, candidate, weapon, candidateProfile, decisionRng);
        if (projection.Disposition == SwordAiCandidateDisposition.Rejected)
            return new(AiScoringDisposition.Rejected, AiScoringRejection.ProjectionRejected, projection, null, projection.RngState);
        if (projection.Disposition == SwordAiCandidateDisposition.NotValuable)
            return new(AiScoringDisposition.NotValuable, AiScoringRejection.None, projection, null, projection.RngState);

        var outcome = projection.Outcome!;
        return new(AiScoringDisposition.Scored, AiScoringRejection.None, projection,
            AiCandidateScoring.ScoreFreshUnits(outcome.Units, outcome.TargetId, (byte)outcome.HitPercent), projection.RngState);
    }

}
