namespace Zodiac.Framework.Ai;

public enum AiRankOrder { Below, Equal, Above }

/// <summary>Source rank_compare for fresh marker-zero actions; proximity belongs to a later list comparison.</summary>
internal static class AiCandidateRank
{
    internal static AiRankOrder Compare(AiCandidateScore candidate, AiCandidateScore baseline)
    {
        int order = candidate.Priority.CompareTo(baseline.Priority);
        if (order == 0) order = candidate.HitPercent.CompareTo(baseline.HitPercent);
        return order > 0 ? AiRankOrder.Above : order < 0 ? AiRankOrder.Below : AiRankOrder.Equal;
    }
}
