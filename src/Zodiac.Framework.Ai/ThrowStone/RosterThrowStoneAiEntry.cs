using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record RosterThrowStoneAiEntryRequest(RosterAiRefreshRequest Preparation, int OccurrenceIndex);
/// <summary>Unrepresented retail instant direct useful-enemy and consume-evaluated-aim policy. Geometry/grants/keys are owned, not certified here.</summary>
public sealed record RosterThrowStoneAiEntryProfile(bool OriginalInstantDirectEnemyPolicy = false);
public enum RosterThrowStoneAiEntryStage { Admission, Prefix, Priority, Candidate }
public sealed record RosterThrowStoneAiEntryFailure(RosterThrowStoneAiEntryStage Stage, RosterAiGeneralFailure? Prefix = null,
    RosterThrowStoneAiCandidateRejection CandidateReason = RosterThrowStoneAiCandidateRejection.None, TilePosition? MissingTile = null,
    int? CandidateIndex = null, bool MissingSpriteHeights = false);
public sealed record RosterThrowStoneAiScannedCandidate(TilePosition Origin, TilePosition TargetPanel,
    ThrowStoneAiCandidate Candidate, RosterThrowStoneAiCandidateResult Evaluation, byte Proximity);
public sealed record RosterThrowStoneAiAimVisit(TilePosition Origin, BattleUnitId TargetId, RosterAiDirectTrajectoryOutcome Trajectory,
    int? CandidateIndex, int RemainingAimCount, RandomState RngState);
public sealed record RosterThrowStoneAiEntryOutcome(RosterAiGeneralScenarioOutcome Scenarios, AiPreparedAbility Entry,
    ImmutableArray<AiCastOriginPriority> RankedOrigins, ImmutableArray<BattleUnitId> AimTargets, ImmutableArray<TilePosition> EvaluatedOrigins,
    ImmutableArray<RosterThrowStoneAiAimVisit> Visits, ImmutableArray<RosterThrowStoneAiScannedCandidate> Candidates,
    int? BestCandidateIndex, AiRankOrder? BaselineComparison, int RemainingOriginCount, int RemainingAimCount)
{
    public bool Skipped => !Entry.Usable;
    public bool BeatsMovementBaseline => BaselineComparison == AiRankOrder.Above;
}
public sealed record RosterThrowStoneAiEntryResult(bool Accepted, RosterThrowStoneAiEntryFailure? Failure,
    RosterThrowStoneAiEntryOutcome? Outcome, RandomState RngState, AiPriorityWorkspace Workspace);

/// <summary>One fresh general prefix and isolated ranked Stone occurrence with computed all-occupant trajectories. No full-list rank, lottery or command.</summary>
/// <remarks>ZOD-301; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Blocked aims remain; clear evaluated aims consume even at zero accuracy.</remarks>
internal static class RosterThrowStoneAiEntry
{
    public static RosterThrowStoneAiEntryResult Evaluate(AttackBattleSnapshot state, RosterThrowStoneAiEntryRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists, RosterAiGeneralProfile general,
        RosterAiMovementProfile scoring, TargetedAbilityBattleProfile ability, RosterAiProjectileHeights heights,
        RosterThrowStoneAiEntryProfile profile, RandomState rng, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Preparation);
        ArgumentNullException.ThrowIfNull(ability); ArgumentNullException.ThrowIfNull(heights); ArgumentNullException.ThrowIfNull(profile);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map); workspace.ValidateMap(state);
        RosterThrowStoneAiEntryResult Reject(RosterThrowStoneAiEntryFailure failure) => new(false, failure, null, rng, workspace);
        if (!profile.OriginalInstantDirectEnemyPolicy) return Reject(new(RosterThrowStoneAiEntryStage.Admission));
        var prefix = RosterAiGeneralScenarios.Evaluate(state, request.Preparation, simulation, refresh, lists, general, scoring, rng);
        if (!prefix.Accepted) return Reject(new(RosterThrowStoneAiEntryStage.Prefix, prefix.Failure));
        var result = EvaluateFresh(state, request.Preparation.ActorId, request.OccurrenceIndex, simulation, ability, heights, prefix.Outcome!, prefix.RngState, workspace);
        return result.Accepted ? result : result with { RngState = rng, Workspace = workspace };
    }

    internal static RosterThrowStoneAiEntryResult EvaluateFresh(AttackBattleSnapshot state, BattleUnitId actorId, int occurrenceIndex,
        RosterAiSimulationProfile simulation, TargetedAbilityBattleProfile ability, RosterAiProjectileHeights heights,
        RosterAiGeneralScenarioOutcome m, RandomState rng, AiPriorityWorkspace workspace)
    {
        RosterThrowStoneAiEntryResult Reject(RosterThrowStoneAiEntryFailure failure) => new(false, failure, null, rng, workspace);
        var entry = m.Preparation.Preparation.ActorEntries.FirstOrDefault(e => e.Index == occurrenceIndex);
        if (entry is null || entry.Source.Kind != AiHumanAbilityKind.ThrowStone || entry.Source.AbilityId is not { } abilityId)
            return Reject(new(RosterThrowStoneAiEntryStage.Admission));
        var successor = workspace.Write(m.Movement.Select(t => KeyValuePair.Create(t.Outcome.Destination, t.TilePriority)));
        if (!entry.Usable) return new(true, null, new(m, entry, [], [], [], [], [], null, null, 0, 0), rng, successor);
        var setup = RosterThrowStoneAiCandidate.ValidateAbility(state, actorId, abilityId, ability, simulation);
        if (setup != RosterThrowStoneAiCandidateRejection.None) return Reject(new(RosterThrowStoneAiEntryStage.Admission, CandidateReason: setup));
        if (!RosterAiDirectTrajectory.HasCompleteHeights(simulation, heights)) return Reject(new(RosterThrowStoneAiEntryStage.Admission, MissingSpriteHeights: true));
        var targets = AiRosterCastGeometry.UsefulEnemies(m.Preparation).OrderBy(u => u.Position.Layer).ThenBy(u => u.Position.Y).ThenBy(u => u.Position.X).ToImmutableArray();
        var origins = AiRosterCastGeometry.Origins(m.Scenario1, targets, 4);
        var ranked = AiRosterCastGeometry.BindRanked(m, origins, workspace, out var missing);
        if (missing is not null) return Reject(new(RosterThrowStoneAiEntryStage.Priority, MissingTile: missing));
        var remaining = targets.Select(u => u.UnitId).ToHashSet();
        var visitedOrigins = ImmutableArray.CreateBuilder<TilePosition>(); var visits = ImmutableArray.CreateBuilder<RosterThrowStoneAiAimVisit>();
        var candidates = ImmutableArray.CreateBuilder<RosterThrowStoneAiScannedCandidate>(); int? best = null; var current = rng;
        var actorOrigin = m.Preparation.Preparation.Targets.ById[actorId].Position;
        foreach (var key in ranked)
        {
            if (remaining.Count == 0) break;
            var tile = key.Tile; visitedOrigins.Add(tile);
            foreach (var target in targets)
            {
                if (!remaining.Contains(target.UnitId) || AiRosterNearest.Distance(tile, target.Position) is < 1 or > 4) continue;
                var trajectory = RosterAiDirectTrajectory.EvaluateAdmitted(state, new(actorId, target.UnitId, tile), simulation, heights);
                if (!trajectory.Clear) { visits.Add(new(tile, target.UnitId, trajectory, null, remaining.Count, current)); continue; }
                bool first = tile == actorOrigin;
                var candidate = new ThrowStoneAiCandidate(actorId, abilityId, target.UnitId, first ? m.ActFirstDestination : tile, first);
                var evaluation = RosterThrowStoneAiCandidate.EvaluateFresh(state, candidate, simulation, current);
                if (evaluation.Disposition == AiScoringDisposition.Rejected)
                    return Reject(new(RosterThrowStoneAiEntryStage.Candidate, CandidateReason: evaluation.Rejection, CandidateIndex: candidates.Count));
                byte proximity = m.Preparation.Proximity.WrittenValues[candidate.Destination];
                if (evaluation.Score is { } score && (best is null || SwordAiCandidateScan.IsHigher(score, proximity,
                    candidates[best.Value].Evaluation.Score!, candidates[best.Value].Proximity))) best = candidates.Count;
                remaining.Remove(target.UnitId); current = evaluation.RngState;
                visits.Add(new(tile, target.UnitId, trajectory, candidates.Count, remaining.Count, current));
                candidates.Add(new(tile, target.Position, candidate, evaluation, proximity));
            }
        }
        var comparison = best is { } index ? AiCandidateRank.Compare(candidates[index].Evaluation.Score!, m.Movement[m.BaselineIndex].Outcome.Score) : (AiRankOrder?)null;
        var outcome = new RosterThrowStoneAiEntryOutcome(m, entry, ranked, targets.Select(u => u.UnitId).ToImmutableArray(), visitedOrigins.ToImmutable(), visits.ToImmutable(),
            candidates.ToImmutable(), best, comparison, ranked.Length - visitedOrigins.Count, remaining.Count);
        return new(true, null, outcome, current, successor);
    }
}
