using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Supplied source scenario destinations, not precomputed scores or a claim to derive D0/D1.</summary>
public sealed record AccumulateAiMapEntryRequest(AccumulateAiPreparationRequest Preparation,
    TilePosition BaselineDestination, TilePosition Destination, bool SourceScenarioDestinations = false);
public enum AccumulateAiMapEntryStage { Admission, Preparation, Movement, Usefulness, Candidate }
public sealed record AccumulateAiMapEntryFailure(AccumulateAiMapEntryStage Stage, int? CandidateIndex = null,
    AccumulateAiPreparationFailure? Preparation = null, AccumulateAiUsefulnessRejection Usefulness = AccumulateAiUsefulnessRejection.None,
    AccumulateAiCandidateRejection Projection = AccumulateAiCandidateRejection.None);
/// <summary>Fresh isolated entry diagnostics. No three-action list, lottery, command or live RNG adoption.</summary>
public sealed record AccumulateAiMapEntryOutcome(AccumulateAiPreparationOutcome Preparation,
    AiMovementBaselineOutcome Baseline, AccumulateAiUsefulnessOutcome Usefulness,
    ImmutableArray<AccumulateAiScoringResult> Candidates, int? BestCandidateIndex, AiRankOrder? BaselineComparison)
{
    public bool BeatsMovementBaseline => BaselineComparison == AiRankOrder.Above;
}
public sealed record AccumulateAiMapEntryResult(bool Accepted, AccumulateAiMapEntryFailure? Failure,
    AccumulateAiMapEntryOutcome? Outcome, RandomState RngState);

/// <summary>Own fresh preparation, one move baseline, one usefulness filter and the map-target origin orders.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-229/232. This isolated query omits earlier/later
/// ability evaluations and full movement scoring. It cannot certify the RNG or plan of an entire three-action decision.</remarks>
public static class AccumulateAiMapEntry
{
    public static AccumulateAiMapEntryResult Evaluate(AttackBattleSnapshot state, AccumulateAiMapEntryRequest request,
        AccumulateAiPreparationConfiguration configuration, RandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(request);
        AccumulateAiPreparation.ValidateArguments(state, request.Preparation, configuration);
        AccumulateAiMapEntryResult Reject(AccumulateAiMapEntryFailure failure) => new(false, failure, null, decisionRng);
        if (!request.SourceScenarioDestinations) return Reject(new(AccumulateAiMapEntryStage.Admission));
        var preparation = AccumulateAiPreparation.Evaluate(state, request.Preparation, configuration, decisionRng);
        if (!preparation.Accepted) return Reject(new(AccumulateAiMapEntryStage.Preparation, Preparation: preparation.Failure));
        var common = preparation.Outcome!.Common;
        if (!common.ReachableOrigins.Contains(request.BaselineDestination) || !common.ReachableOrigins.Contains(request.Destination))
            return Reject(new(AccumulateAiMapEntryStage.Admission));
        var turn = request.Preparation.Turn;
        var baseline = AiMovementSimulation.Evaluate(state, turn.ActorId, turn.TargetId, request.BaselineDestination,
            configuration.Accumulate.Units, preparation.RngState);
        if (baseline.Rejection != AiSimulationClockRejection.None)
            return Reject(new(AccumulateAiMapEntryStage.Movement, Projection: ClockReason(baseline.Rejection)));
        // Filtering belongs to the entry, once, before either origin. Never feed a public cached gate to scoring.
        var usefulness = AccumulateAiUsefulness.FilterFresh(state, turn.ActorId, new(true, true, common.TargetActive), baseline.RngState);
        if (!usefulness.Accepted) return Reject(new(AccumulateAiMapEntryStage.Usefulness, Usefulness: usefulness.Rejection));
        var candidates = ImmutableArray.CreateBuilder<AccumulateAiScoringResult>();
        var rng = usefulness.RngState;
        int? best = null;
        if (usefulness.Outcome!.SelfUseful)
        {
            int originCount = request.Destination == common.Origin ? 1 : 2;
            for (int i = 0; i < originCount; i++)
            {
                var candidate = new AccumulateAiCandidate(turn.ActorId, request.Preparation.AbilityId, turn.TargetId,
                    request.Destination, ActBeforeMove: i == 0, SelfActiveAndUseful: true);
                var result = AccumulateAiCandidateScoring.Evaluate(state, candidate, configuration.Accumulate, configuration.Scoring, rng);
                if (result.Disposition != AiScoringDisposition.Scored)
                    return Reject(new(AccumulateAiMapEntryStage.Candidate, i, Projection: result.Projection?.Rejection ?? AccumulateAiCandidateRejection.UnsupportedProfile));
                // Both orders have the same final tile/proximity. Complete rank ties retain ACT-first.
                if (best is null || AiCandidateRank.Compare(result.Score!, candidates[best.Value].Score!) == AiRankOrder.Above)
                    best = i;
                candidates.Add(result); rng = result.RngState;
            }
        }
        var comparison = best is { } index ? AiCandidateRank.Compare(candidates[index].Score!, baseline.Outcome!.Score) : (AiRankOrder?)null;
        return new(true, null, new(preparation.Outcome, baseline.Outcome!, usefulness.Outcome, candidates.ToImmutable(), best, comparison), rng);
    }

    private static AccumulateAiCandidateRejection ClockReason(AiSimulationClockRejection reason) =>
        reason == AiSimulationClockRejection.RandomDrawCountExhausted
            ? AccumulateAiCandidateRejection.RandomDrawCountExhausted : AccumulateAiCandidateRejection.TickCountExhausted;
}
