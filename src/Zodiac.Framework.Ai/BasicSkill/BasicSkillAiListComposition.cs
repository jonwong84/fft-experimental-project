using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record BasicSkillAiListFailure(BasicSkillAiDerivedStage Stage, BasicSkillAiScenarioFailure? Scenarios = null,
    BasicSkillAiFailure? Entry = null, int? OccurrenceIndex = null, int? LotteryIndex = null);
public sealed record BasicSkillAiListCandidate(int OccurrenceIndex, BasicSkillAiAction Action);
public sealed record BasicSkillAiListEntryOutcome(BasicSkillAiPreparedOccurrence Entry, RandomState EntryRng, RandomState ExitRng,
    SwordAiScanOutcome? Attack = null, AccumulateAiEntryOutcome? Accumulate = null, ThrowStoneAiScanOutcome? ThrowStone = null,
    int? InsertionIndex = null);
public sealed record BasicSkillAiListCompositionOutcome(BasicSkillAiListScenarioOutcome Scenarios,
    ImmutableArray<AiCastOriginPriority> CastOrigins, ImmutableArray<BasicSkillAiListEntryOutcome> Entries,
    ImmutableArray<BasicSkillAiListCandidate?> RankedActions, AiHumanAbilityKind? LastEnteredKind);
public sealed record BasicSkillAiListCompositionResult(bool Accepted, BasicSkillAiListFailure? Failure,
    BasicSkillAiListCompositionOutcome? Outcome, RandomState RngState, AiPriorityWorkspace Workspace);

/// <summary>Fresh owned list/scenarios followed by source-order occurrence evaluation through existing entry/rank kernels.</summary>
public static class BasicSkillAiListComposition
{
    public static BasicSkillAiListCompositionResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, RandomState rng, AiPriorityWorkspace? workspace = null) => EvaluateCore(state, request, profile, rng, workspace, false);

    internal static BasicSkillAiListCompositionResult EvaluateCore(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiListProfile profile, RandomState rng, AiPriorityWorkspace? workspace, bool afterAct)
    {
        BasicSkillAiListScenarios.ValidateArguments(state, request, profile);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map); workspace.ValidateMap(state);
        BasicSkillAiListCompositionResult Reject(BasicSkillAiListFailure failure) => new(false, failure, null, rng, workspace);
        if (!profile.SourceGeneralEntry || !profile.FreshEmptyRankedList) return Reject(new(BasicSkillAiDerivedStage.Admission));
        var scenarios = BasicSkillAiListScenarios.EvaluateFresh(state, request, profile, rng, afterAct);
        if (!scenarios.Result.Accepted) return Reject(new(BasicSkillAiDerivedStage.Scenarios, Scenarios: scenarios.Result.Failure));
        var ready = scenarios.Result.Outcome!; var m = ready.Movement; var p = profile.Preparation; var turn = request.Turn;
        bool stonePresent = ready.Preparation.ActorEntries.Any(e => e.Usable && e.Source.Kind == AiHumanAbilityKind.ThrowStone);
        if (stonePresent && !profile.ClearDirectTrajectories) return Reject(new(BasicSkillAiDerivedStage.Admission));
        int range = afterAct ? 0 : stonePresent ? 4 : 1;
        int Distance(TilePosition tile) => Math.Abs(tile.X - ready.Preparation.Common.FocusPosition.X) + Math.Abs(tile.Y - ready.Preparation.Common.FocusPosition.Y);
        var cast = ImmutableArray.CreateBuilder<AiCastOriginPriority>();
        foreach (var cell in m.Cells.Where(c => !afterAct && m.Scenario1.Contains(c.Tile) && Distance(c.Tile) <= range))
        {
            short priority;
            if (cell.WrittenPriority is { } fresh) priority = fresh;
            else if (!workspace.KnownPriorities.TryGetValue(cell.Tile, out priority)) return Reject(new(BasicSkillAiDerivedStage.Provenance));
            cast.Add(new(cell.Tile, priority, cell.Proximity, cell.PriorityWritten ? AiCastPriorityProvenance.Fresh : AiCastPriorityProvenance.Retained));
        }
        var keys = cast.ToImmutable(); var written = m.ScoreMask.ToHashSet(); var known = keys.Select(c => c.Tile).ToHashSet();
        var origins = m.Scenario1.Where(t => written.Contains(t) || known.Contains(t)).ToImmutableArray();
        var scenario = new ThrowStoneAiScanScenario(m.ScoreMask, origins, m.BaselineDestination, m.ActFirstDestination);
        var attackScenario = new SwordAiScanScenario(origins, origins.IsEmpty ? null : m.ActFirstDestination);
        var comparison = new SwordAiComparisonProfile(turn.ActorId, turn.TargetId, true);
        var attackProfile = new SwordAiScanProfile(ready.Preparation.Common.TargetActive, true, true, true, true);
        var stoneProfile = new ThrowStoneAiScanProfile(ready.Preparation.Common.TargetActive, true, true, true, true, true, true, profile.ClearDirectTrajectories);
        var stoneRequest = new ThrowStoneAiScanRequest(turn.ActorId, request.ThrowStoneAbilityId, turn.TargetId);
        var prefix = scenarios.Prefix!; var baseline = prefix.Movement[prefix.BaselineIndex].Outcome.Score;
        var ranked = new BasicSkillAiListCandidate?[8]; var entries = ImmutableArray.CreateBuilder<BasicSkillAiListEntryOutcome>();
        var currentRng = scenarios.Result.RngState; AiHumanAbilityKind? last = null;
        int? Insert(int index, BasicSkillAiAction best) => AiRankedInsertion.Insert(new BasicSkillAiListCandidate(index, best), baseline,
            ranked, c => c.Action.Score, c => c.Action.Proximity);
        foreach (var occurrence in ready.Preparation.ActorEntries)
        {
            var entryRng = currentRng;
            if (!occurrence.Usable) { entries.Add(new(occurrence, entryRng, currentRng)); continue; }
            last = occurrence.Source.Kind;
            switch (last)
            {
                case AiHumanAbilityKind.Attack:
                    var admission = SwordAiCandidateScan.ValidateInputs(state, attackScenario, request.Weapon, p.Attack, p.Scoring, comparison, attackProfile, currentRng);
                    var attack = admission ?? SwordAiCandidateScan.EvaluateFresh(state, attackScenario, request.Weapon, p.Attack, p.Scoring, comparison, attackProfile, prefix.Proximity, currentRng);
                    if (attack.Disposition == SwordAiScanDisposition.Rejected)
                        return Reject(new(BasicSkillAiDerivedStage.Composition, Entry: new(BasicSkillAiStage.Attack, Attack: attack.Rejection,
                            Scoring: attack.ScoringRejection, AttackProjection: attack.ProjectionRejection), OccurrenceIndex: occurrence.Index));
                    int? ai = attack.Outcome!.RetainedCandidateIndex is { } a ? Insert(occurrence.Index, new BasicSkillAiAttack(attack.Outcome.Candidates[a])) : null;
                    currentRng = attack.RngState; entries.Add(new(occurrence, entryRng, currentRng, Attack: attack.Outcome, InsertionIndex: ai));
                    break;
                case AiHumanAbilityKind.Accumulate:
                    var self = AccumulateAiEntryEvaluation.Evaluate(state, turn.ActorId, turn.TargetId, request.AbilityId, m.ActFirstDestination,
                        p.Accumulate, p.Scoring, ready.Preparation.Common.TargetActive, currentRng);
                    if (self.Failure is not null) return Reject(new(BasicSkillAiDerivedStage.Composition,
                        Entry: new(BasicSkillAiStage.Accumulate, Accumulate: self.Failure), OccurrenceIndex: occurrence.Index));
                    int? si = self.Outcome!.BestCandidateIndex is { } s ? Insert(occurrence.Index,
                        new BasicSkillAiAccumulate(self.Outcome.Candidates[s], prefix.Proximity.Destinations[m.ActFirstDestination])) : null;
                    currentRng = self.RngState; entries.Add(new(occurrence, entryRng, currentRng, Accumulate: self.Outcome, InsertionIndex: si));
                    break;
                case AiHumanAbilityKind.ThrowStone:
                    var invalid = ThrowStoneAiCandidateScan.ValidateInputs(state, stoneRequest, scenario, p.ThrowStone, p.Scoring,
                        new(true, true, true, true), stoneProfile, currentRng, keys);
                    var stone = invalid ?? ThrowStoneAiCandidateScan.ContinueFromMovement(state, stoneRequest, scenario, p.ThrowStone, p.Scoring,
                        stoneProfile, prefix, currentRng, rng, keys);
                    if (stone.Disposition == ThrowStoneAiScanDisposition.Rejected) return Reject(new(BasicSkillAiDerivedStage.Composition,
                        Entry: new(BasicSkillAiStage.ThrowStone, ThrowStone: stone.Failure), OccurrenceIndex: occurrence.Index));
                    int? ti = stone.Outcome!.Candidate is { Evaluation.Score: not null } c ? Insert(occurrence.Index,
                        new BasicSkillAiStone(c, prefix.Proximity.Destinations[c.Candidate.Destination])) : null;
                    currentRng = stone.RngState; entries.Add(new(occurrence, entryRng, currentRng, ThrowStone: stone.Outcome, InsertionIndex: ti));
                    break;
                default: throw new InvalidOperationException("An unsupported occurrence passed owned list admission.");
            }
        }
        return new(true, null, new(ready, keys, entries.ToImmutable(), ranked.ToImmutableArray(), last), currentRng, workspace.Write(m.Movement));
    }
}
