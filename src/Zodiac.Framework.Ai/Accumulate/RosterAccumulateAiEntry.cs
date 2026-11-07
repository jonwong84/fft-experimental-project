using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record RosterAccumulateAiEntryRequest(RosterAiRefreshRequest Preparation, int OccurrenceIndex);
public sealed record RosterAccumulateAiProjection(int PhysicalAttackBefore, int PhysicalAttackAfter, int TurnEndCt,
    ImmutableArray<AiProjectedUnit> Units, BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks,
    ImmutableArray<AiUpkeepPick> UpkeepPicks);
public sealed record RosterAccumulateAiCandidate(bool ActBeforeMove, AiScoringDisposition Disposition,
    RosterAccumulateAiProjection? Projection, AiCandidateScore? Score, RandomState RngState);
public enum RosterAccumulateAiEntryStage { Admission, Prefix, Usefulness, Candidate }
public sealed record RosterAccumulateAiEntryFailure(RosterAccumulateAiEntryStage Stage,
    RosterAiGeneralFailure? Prefix = null, RosterAccumulateAiRejection Reason = RosterAccumulateAiRejection.None, int? CandidateIndex = null);
public sealed record RosterAccumulateAiEntryOutcome(RosterAiGeneralScenarioOutcome Scenarios, AiPreparedAbility Entry,
    AccumulateAiUsefulnessOutcome? Usefulness, ImmutableArray<RosterAccumulateAiCandidate> Candidates,
    int? BestCandidateIndex, AiRankOrder? BaselineComparison)
{
    public bool Skipped => !Entry.Usable;
    public bool BeatsMovementBaseline => BaselineComparison == AiRankOrder.Above;
}
public sealed record RosterAccumulateAiEntryResult(bool Accepted, RosterAccumulateAiEntryFailure? Failure,
    RosterAccumulateAiEntryOutcome? Outcome, RandomState RngState);

/// <summary>Isolated selected self occurrence after one owned fresh general prefix. No preceding/later entry stream, ranked list, lottery or command.</summary>
/// <remarks>ZOD-290/293; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Other allies' usefulness cannot certify a valuable self hit.</remarks>
internal static class RosterAccumulateAiEntry
{
    public static RosterAccumulateAiEntryResult Evaluate(AttackBattleSnapshot state, RosterAccumulateAiEntryRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile general, RosterAiMovementProfile scoring, RosterAccumulateAiProfile ability, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Preparation); ArgumentNullException.ThrowIfNull(ability);
        RosterAccumulateAiEntryResult Reject(RosterAccumulateAiEntryFailure failure) => new(false, failure, null, rng);
        var prepared = RosterAiGeneralScenarios.Evaluate(state, request.Preparation, simulation, refresh, lists, general, scoring, rng);
        if (!prepared.Accepted) return Reject(new(RosterAccumulateAiEntryStage.Prefix, prepared.Failure));
        var result = EvaluateFresh(state, request.Preparation.ActorId, request.OccurrenceIndex, simulation, ability, prepared.Outcome!, prepared.RngState);
        return result.Accepted ? result : result with { RngState = rng };
    }

    internal static RosterAccumulateAiEntryResult EvaluateFresh(AttackBattleSnapshot state, BattleUnitId actorId, int occurrenceIndex,
        RosterAiSimulationProfile simulation, RosterAccumulateAiProfile ability, RosterAiGeneralScenarioOutcome m, RandomState rng)
    {
        RosterAccumulateAiEntryResult Reject(RosterAccumulateAiEntryFailure failure) => new(false, failure, null, rng);
        var entry = m.Preparation.Preparation.ActorEntries.FirstOrDefault(e => e.Index == occurrenceIndex);
        if (entry is null || entry.Source.Kind != AiHumanAbilityKind.Accumulate || entry.Source.AbilityId is not { } abilityId)
            return Reject(new(RosterAccumulateAiEntryStage.Admission));
        if (!entry.Usable) return new(true, null, new(m, entry, null, [], null, null), rng);
        var usefulness = RosterAccumulateAiUsefulness.Evaluate(state,
            new(actorId, abilityId, m.Preparation.ActiveTargets.SelectedUnits), simulation, ability, rng);
        if (!usefulness.Accepted) return Reject(new(RosterAccumulateAiEntryStage.Usefulness, Reason: usefulness.Rejection));
        var candidates = ImmutableArray.CreateBuilder<RosterAccumulateAiCandidate>(); var current = usefulness.RngState; int? best = null;
        if (usefulness.Outcome!.Targets.Any(u => u.Effect == AccumulateAiEffect.Useful))
        {
            var origin = m.Preparation.Preparation.Targets.ById[actorId].Position;
            var destination = m.ActFirstDestination; int count = origin == destination ? 1 : 2;
            int team = simulation.Battle.Units[actorId].StableTeam;
            var enemies = simulation.Battle.Members.Where(u => u.StableTeam != team).Select(u => u.UnitId).ToImmutableHashSet();
            for (int i = 0; i < count; i++)
            {
                // Another useful ally admits the pass but an unuseful actor fails immediate hit accounting before the clock.
                if (!usefulness.Outcome.SelfUseful) { candidates.Add(new(i == 0, AiScoringDisposition.NotValuable, null, null, current)); continue; }
                var effect = AiSelfStatProjection.Evaluate(state, actorId, destination, simulation.Units, current); var projection = effect.Projection;
                if (projection.Rejection != AiSimulationClockRejection.None)
                    return Reject(new(RosterAccumulateAiEntryStage.Candidate, Reason: projection.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                        ? RosterAccumulateAiRejection.RandomDrawCountExhausted : RosterAccumulateAiRejection.TickCountExhausted, CandidateIndex: i));
                var clock = projection.Clock!;
                var outcome = new RosterAccumulateAiProjection(effect.Before, effect.After, projection.TurnEndCt, clock.Units,
                    clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks);
                var score = AiCandidateScoring.ScoreFreshUnits(clock.Units, enemies, 100, initialPriority: 3);
                if (best is null || AiCandidateRank.Compare(score, candidates[best.Value].Score!) == AiRankOrder.Above) best = i;
                current = clock.RngState; candidates.Add(new(i == 0, AiScoringDisposition.Scored, outcome, score, current));
            }
        }
        var comparison = best is { } index ? AiCandidateRank.Compare(candidates[index].Score!, m.Movement[m.BaselineIndex].Outcome.Score) : (AiRankOrder?)null;
        return new(true, null, new(m, entry, usefulness.Outcome, candidates.ToImmutable(), best, comparison), current);
    }
}
