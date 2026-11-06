using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum RosterDashAiEntryStage { Admission, Prefix, Priority, Candidate }
public sealed record RosterDashAiEntryFailure(RosterDashAiEntryStage Stage, RosterAiGeneralFailure? Prefix = null,
    RosterDashAiCandidateRejection CandidateReason = RosterDashAiCandidateRejection.None, TilePosition? MissingTile = null, int? CandidateIndex = null);
public sealed record RosterDashAiScannedCandidate(TilePosition Origin, DashAiCandidate Candidate, RosterDashAiCandidateResult Evaluation, byte Proximity);
public sealed record RosterDashAiEntryOutcome(RosterAiGeneralScenarioOutcome Scenarios, AiPreparedAbility Entry,
    ImmutableArray<AiCastOriginPriority> RankedOrigins, ImmutableArray<BattleUnitId> AimTargets,
    ImmutableArray<TilePosition> EvaluatedOrigins, ImmutableArray<RosterDashAiScannedCandidate> Candidates,
    int? BestCandidateIndex, AiRankOrder? BaselineComparison, int RemainingOriginCount, int RemainingAimCount)
{
    public bool Skipped => !Entry.Usable;
}
public sealed record RosterDashAiEntryResult(bool Accepted, RosterDashAiEntryFailure? Failure,
    RosterDashAiEntryOutcome? Outcome, RandomState RngState, AiPriorityWorkspace Workspace);

/// <summary>One privately prepared ranked Dash occurrence, range1 union and consumed map-order aims; no trajectory or public cached prefix.</summary>
/// <remarks>ZOD-323; source 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
internal static class RosterDashAiEntry
{
    public static RosterDashAiEntryResult Evaluate(AttackBattleSnapshot state, RosterAiRefreshRequest request, int occurrenceIndex,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile general, RosterAiMovementProfile movement, DashAbilityBattleProfile ability,
        bool originalInstantEnemyPolicy, RandomState rng, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(ability);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map); workspace.ValidateMap(state);
        if (!originalInstantEnemyPolicy) return new(false, new(RosterDashAiEntryStage.Admission), null, rng, workspace);
        var p = RosterAiGeneralScenarios.Evaluate(state, request, simulation, refresh, lists, general, movement, rng);
        if (!p.Accepted) return new(false, new(RosterDashAiEntryStage.Prefix, p.Failure), null, rng, workspace);
        var r = EvaluateFresh(state, request.ActorId, occurrenceIndex, simulation, ability, p.Outcome!, p.RngState, workspace);
        return r.Accepted ? r : r with { RngState = rng, Workspace = workspace };
    }
    internal static RosterDashAiEntryResult EvaluateFresh(AttackBattleSnapshot state, BattleUnitId actor,
        int occurrenceIndex, RosterAiSimulationProfile simulation, DashAbilityBattleProfile ability,
        RosterAiGeneralScenarioOutcome m, RandomState rng, AiPriorityWorkspace workspace)
    {
        RosterDashAiEntryResult Reject(RosterDashAiEntryFailure f) => new(false, f, null, rng, workspace);
        var entry = m.Preparation.Preparation.ActorEntries.FirstOrDefault(e => e.Index == occurrenceIndex);
        if (entry is null || entry.Source.Kind != AiHumanAbilityKind.Dash || entry.Source.AbilityId is not { } id) return Reject(new(RosterDashAiEntryStage.Admission));
        var successor = workspace.Write(m.Movement.Select(t => KeyValuePair.Create(t.Outcome.Destination, t.TilePriority)));
        if (!entry.Usable) return new(true, null, new(m, entry, [], [], [], [], null, null, 0, 0), rng, successor);
        var setup = RosterDashAiCandidate.ValidateAbility(state, actor, id, ability, simulation);
        if (setup != RosterDashAiCandidateRejection.None) return Reject(new(RosterDashAiEntryStage.Admission, CandidateReason: setup));
        var targets = AiRosterCastGeometry.UsefulEnemies(m.Preparation).OrderBy(u => u.Position.Layer).ThenBy(u => u.Position.Y).ThenBy(u => u.Position.X).ToImmutableArray();
        var ranked = AiRosterCastGeometry.BindRanked(m, AiRosterCastGeometry.Origins(m.Scenario1, targets, 1), workspace, out var missing);
        if (missing is not null) return Reject(new(RosterDashAiEntryStage.Priority, MissingTile: missing));
        var remaining = targets.Select(u => u.UnitId).ToHashSet(); var origins = ImmutableArray.CreateBuilder<TilePosition>();
        var candidates = ImmutableArray.CreateBuilder<RosterDashAiScannedCandidate>(); int? best = null; var current = rng;
        var start = m.Preparation.Preparation.Targets.ById[actor].Position;
        foreach (var key in ranked)
        {
            if (remaining.Count == 0) break;
            origins.Add(key.Tile);
            foreach (var target in targets)
            {
                if (!remaining.Contains(target.UnitId) || AiRosterNearest.Distance(key.Tile, target.Position) != 1) continue;
                bool first = key.Tile == start;
                var candidate = new DashAiCandidate(actor, id, target.UnitId, first ? m.ActFirstDestination : key.Tile, first);
                var result = RosterDashAiCandidate.EvaluateFresh(state, candidate, simulation, ability.Abilities[id].Definition, current);
                if (result.Disposition == AiScoringDisposition.Rejected) return Reject(new(RosterDashAiEntryStage.Candidate, CandidateReason: result.Rejection, CandidateIndex: candidates.Count));
                byte proximity = m.Preparation.Proximity.WrittenValues[candidate.Destination];
                if (result.Score is { } score && (best is null || SwordAiCandidateScan.IsHigher(score, proximity, candidates[best.Value].Evaluation.Score!, candidates[best.Value].Proximity))) best = candidates.Count;
                candidates.Add(new(key.Tile, candidate, result, proximity)); current = result.RngState; remaining.Remove(target.UnitId);
            }
        }
        var comparison = best is { } i ? AiCandidateRank.Compare(candidates[i].Evaluation.Score!, m.Movement[m.BaselineIndex].Outcome.Score) : (AiRankOrder?)null;
        return new(true, null, new(m, entry, ranked, targets.Select(u => u.UnitId).ToImmutableArray(), origins.ToImmutable(), candidates.ToImmutable(), best, comparison,
            ranked.Length - origins.Count, remaining.Count), current, successor);
    }
}
