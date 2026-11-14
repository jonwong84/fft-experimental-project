using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Diagnostic entry outcome, never accepted as a trusted query input.</summary>
public sealed record AccumulateAiEntryOutcome(AccumulateAiUsefulnessOutcome Usefulness,
    ImmutableArray<AccumulateAiScoringResult> Candidates, int? BestCandidateIndex);
internal sealed record AccumulateAiEntryEvaluationResult(AccumulateAiMapEntryFailure? Failure,
    AccumulateAiEntryOutcome? Outcome, RandomState RngState);

/// <summary>One admitted map-target entry. Its owner supplies actual state and the continuing decision stream.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-233/234. No refresh or movement scoring.</remarks>
internal static class AccumulateAiEntryEvaluation
{
    internal static AccumulateAiEntryEvaluationResult Evaluate(AttackBattleSnapshot state, BattleUnitId actorId,
        BattleUnitId opponentId, AssetId abilityId, TilePosition destination, AccumulateAiCandidateProfile profile,
        AiScoreProfile scores, bool opponentActive, RandomState entryRng)
    {
        var usefulness = AccumulateAiUsefulness.FilterFresh(state, actorId, new(true, true, opponentActive), entryRng);
        if (!usefulness.Accepted)
            return new(new(AccumulateAiMapEntryStage.Usefulness, Usefulness: usefulness.Rejection), null, entryRng);
        var candidates = ImmutableArray.CreateBuilder<AccumulateAiScoringResult>();
        var rng = usefulness.RngState;
        int? best = null;
        if (usefulness.Outcome!.SelfUseful)
        {
            state.TurnBattle.Battle.TryGetUnit(actorId, out var actor);
            int originCount = destination == actor!.Position ? 1 : 2;
            for (int i = 0; i < originCount; i++)
            {
                var candidate = new AccumulateAiCandidate(actorId, abilityId, opponentId, destination,
                    ActBeforeMove: i == 0, SelfActiveAndUseful: true);
                var result = AccumulateAiCandidateScoring.Evaluate(state, candidate, profile, scores, rng);
                if (result.Disposition != AiScoringDisposition.Scored)
                    return new(new(AccumulateAiMapEntryStage.Candidate, i,
                        Projection: result.Projection?.Rejection ?? AccumulateAiCandidateRejection.UnsupportedProfile), null, entryRng);
                // Both orders share final tile/proximity. Complete rank ties retain ACT-first.
                if (best is null || AiCandidateRank.Compare(result.Score!, candidates[best.Value].Score!) == AiRankOrder.Above) best = i;
                candidates.Add(result); rng = result.RngState;
            }
        }
        return new(null, new(usefulness.Outcome, candidates.ToImmutable(), best), rng);
    }
}
