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
internal static class AccumulateAiMapEntry
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
        var evaluation = AccumulateAiEntryEvaluation.Evaluate(state, turn.ActorId, turn.TargetId, request.Preparation.AbilityId,
            request.Destination, configuration.Accumulate, configuration.Scoring, common.TargetActive, baseline.RngState);
        if (evaluation.Failure is not null) return Reject(evaluation.Failure);
        var entry = evaluation.Outcome!;
        var comparison = entry.BestCandidateIndex is { } index
            ? AiCandidateRank.Compare(entry.Candidates[index].Score!, baseline.Outcome!.Score) : (AiRankOrder?)null;
        return new(true, null, new(preparation.Outcome, baseline.Outcome!, entry.Usefulness, entry.Candidates,
            entry.BestCandidateIndex, comparison), evaluation.RngState);
    }

    private static AccumulateAiCandidateRejection ClockReason(AiSimulationClockRejection reason) =>
        reason == AiSimulationClockRejection.RandomDrawCountExhausted
            ? AccumulateAiCandidateRejection.RandomDrawCountExhausted : AccumulateAiCandidateRejection.TickCountExhausted;
}
