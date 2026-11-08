namespace Zodiac.Framework.Ai;

/// <summary>Source ai_best_rank_insert over fresh privately owned ranks; no public cached-rank entry.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-233/234. Null is a fresh reset slot in the admitted range.</remarks>
internal static class AiRankedInsertion
{
    internal static int? AiBestRankInsert<T>(T best, AiCandidateScore baseline, T?[] ranked,
        Func<T, AiCandidateScore> score, Func<T, byte> proximity) where T : class
    {
        var bestScore = score(best);
        if (AiCandidateRank.Compare(bestScore, baseline) != AiRankOrder.Above) return null;
        bool Higher(T? entry) => entry is null || AiCandidateRank.Compare(bestScore, score(entry)) == AiRankOrder.Above ||
            AiCandidateRank.Compare(bestScore, score(entry)) == AiRankOrder.Equal && proximity(best) < proximity(entry);
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
