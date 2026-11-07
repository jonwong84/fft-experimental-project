namespace Zodiac.Framework.Ai;

public enum AiRankOrder { Below, Equal, Above }

/// <summary>Source rank_compare for fresh marker-zero actions; proximity belongs to a later list comparison.</summary>
internal static class AiCandidateRank
{
    internal static AiRankOrder Compare(AiCandidateScore candidate, AiCandidateScore baseline)
        => Compare(candidate.Priority, candidate.HitPercent, baseline.Priority, baseline.HitPercent);

    // Formula families retain their own score contributions; fresh rank keys share this one comparison.
    internal static AiRankOrder Compare(short candidatePriority, byte candidateHit, short baselinePriority, byte baselineHit)
    {
        int order = candidatePriority.CompareTo(baselinePriority);
        if (order == 0) order = candidateHit.CompareTo(baselineHit);
        return order > 0 ? AiRankOrder.Above : order < 0 ? AiRankOrder.Below : AiRankOrder.Equal;
    }
}
