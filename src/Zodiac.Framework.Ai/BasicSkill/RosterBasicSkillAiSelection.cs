using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Actual effect bindings plus remaining source policy/default facts; only entered actions require their bindings.</summary>
public sealed record RosterBasicSkillAiProfile(BoundedSwordWeapon? Weapon = null, RosterAccumulateAiProfile? Accumulate = null,
    TargetedAbilityBattleProfile? ThrowStone = null, RosterAiProjectileHeights? Heights = null,
    bool OrdinaryAttackTilePolicy = false, bool OriginalInstantDirectEnemyPolicy = false,
    bool NoDefendSupport = false, bool FreshOrdinarySelectionDefaults = false,
    DashAbilityBattleProfile? Dash = null, bool OriginalInstantEnemyDashPolicy = false);
public sealed record RosterBasicSkillAiRankedAction(int OccurrenceIndex, AiHumanAbilityKind Kind, TilePosition Destination,
    bool ActBeforeMove, BattleUnitId? TargetId, AssetId? AbilityId, AiCandidateScore Score, byte Proximity);
public sealed record RosterBasicSkillAiEntryOutcome(BasicSkillAiPreparedOccurrence Entry, RandomState EntryRng, RandomState ExitRng,
    RosterSwordAiAttackOutcome? Attack = null, RosterAccumulateAiEntryOutcome? Accumulate = null,
    RosterThrowStoneAiEntryOutcome? ThrowStone = null, RosterBasicSkillAiRankedAction? Best = null, int? InsertionIndex = null, RosterDashAiEntryOutcome? Dash = null);
public enum RosterBasicSkillAiSelectionStage { Admission, Prefix, Entry, Lottery }
public sealed record RosterBasicSkillAiSelectionFailure(RosterBasicSkillAiSelectionStage Stage, RosterAiGeneralFailure? Prefix = null,
    int? OccurrenceIndex = null, RosterSwordAiAttackFailure? Attack = null, RosterAccumulateAiEntryFailure? Accumulate = null,
    RosterThrowStoneAiEntryFailure? ThrowStone = null, int? LotteryIndex = null, RosterDashAiEntryFailure? Dash = null);
public sealed record RosterBasicSkillAiSelectedPlan(BattleUnitId ActorId, TilePosition Destination, RosterBasicSkillAiRankedAction? Action)
{
    public bool ActBeforeMove => Action?.ActBeforeMove ?? false;
    public bool ChooseFacingAtWait => true;
}
public sealed record RosterBasicSkillAiSelectionOutcome(RosterAiGeneralScenarioOutcome Scenarios,
    ImmutableArray<RosterBasicSkillAiEntryOutcome> Entries, ImmutableArray<RosterBasicSkillAiRankedAction?> RankedActions,
    RandomState LotteryEntryRng, ImmutableArray<AiLotteryRoll> Lottery, int? SelectedRankedIndex,
    AiLotteryReason Reason, RosterBasicSkillAiSelectedPlan Plan);
public sealed record RosterBasicSkillAiSelectionResult(bool Accepted, RosterBasicSkillAiSelectionFailure? Failure,
    RosterBasicSkillAiSelectionOutcome? Outcome, RandomState RngState, AiPriorityWorkspace Workspace);

/// <summary>One fresh whole-roster prefix, supported structural ability list, private ranks and source lottery. No command authority.</summary>
/// <remarks>ZOD-302/303; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Every hypothesis restores state while decision RNG chains.</remarks>
public static class RosterBasicSkillAiSelection
{
    public static RosterBasicSkillAiSelectionResult Evaluate(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists, RosterAiGeneralProfile general,
        RosterAiMovementProfile scoring, RosterBasicSkillAiProfile profile, RandomState rng, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(profile);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map); workspace.ValidateMap(state);
        RosterBasicSkillAiSelectionResult Reject(RosterBasicSkillAiSelectionFailure failure) => new(false, failure, null, rng, workspace);
        if (!profile.NoDefendSupport || !profile.FreshOrdinarySelectionDefaults) return Reject(new(RosterBasicSkillAiSelectionStage.Admission));
        var prefix = RosterAiGeneralScenarios.Evaluate(state, request, simulation, refresh, lists, general, scoring, rng);
        if (!prefix.Accepted) return Reject(new(RosterBasicSkillAiSelectionStage.Prefix, prefix.Failure));
        var m = prefix.Outcome!; var current = prefix.RngState;
        var ranked = new RosterBasicSkillAiRankedAction?[8]; var entries = ImmutableArray.CreateBuilder<RosterBasicSkillAiEntryOutcome>();
        foreach (var entry in m.Preparation.Preparation.ActorEntries)
        {
            var entryRng = current;
            if (!entry.Usable) { entries.Add(new(entry, current, current)); continue; }
            RosterSwordAiAttackOutcome? attack = null; RosterAccumulateAiEntryOutcome? self = null; RosterThrowStoneAiEntryOutcome? stone = null;
            RosterDashAiEntryOutcome? dash = null;
            RosterBasicSkillAiRankedAction? best = null;
            switch (entry.Source.Kind)
            {
                case AiHumanAbilityKind.Attack:
                    if (!profile.OrdinaryAttackTilePolicy) return Reject(new(RosterBasicSkillAiSelectionStage.Entry, OccurrenceIndex: entry.Index));
                    var a = RosterSwordAiAttackEntry.EvaluateFresh(state, request.ActorId, profile.Weapon, simulation, m, current);
                    if (!a.Accepted) return Reject(new(RosterBasicSkillAiSelectionStage.Entry, OccurrenceIndex: entry.Index, Attack: a.Failure));
                    attack = a.Outcome!; current = a.RngState;
                    if (attack.BestCandidateIndex is { } ai)
                    {
                        var c = attack.Candidates[ai]; best = new(entry.Index, entry.Source.Kind, c.Candidate.Destination,
                            c.Candidate.Order == SwordAiCandidateOrder.ActThenMove, c.Candidate.TargetId, null, c.Evaluation.Score!, c.Proximity);
                    }
                    break;
                case AiHumanAbilityKind.Accumulate:
                    if (profile.Accumulate is null) return Reject(new(RosterBasicSkillAiSelectionStage.Entry, OccurrenceIndex: entry.Index));
                    var s = RosterAccumulateAiEntry.EvaluateFresh(state, request.ActorId, entry.Index, simulation, profile.Accumulate, m, current);
                    if (!s.Accepted) return Reject(new(RosterBasicSkillAiSelectionStage.Entry, OccurrenceIndex: entry.Index, Accumulate: s.Failure));
                    self = s.Outcome!; current = s.RngState;
                    if (self.BestCandidateIndex is { } si)
                    {
                        var c = self.Candidates[si]; best = new(entry.Index, entry.Source.Kind, m.ActFirstDestination, c.ActBeforeMove,
                            null, entry.Source.AbilityId, c.Score!, m.Preparation.Proximity.WrittenValues[m.ActFirstDestination]);
                    }
                    break;
                case AiHumanAbilityKind.ThrowStone:
                    if (!profile.OriginalInstantDirectEnemyPolicy || profile.ThrowStone is null || profile.Heights is null)
                        return Reject(new(RosterBasicSkillAiSelectionStage.Entry, OccurrenceIndex: entry.Index));
                    var t = RosterThrowStoneAiEntry.EvaluateFresh(state, request.ActorId, entry.Index, simulation, profile.ThrowStone, profile.Heights, m, current, workspace);
                    if (!t.Accepted) return Reject(new(RosterBasicSkillAiSelectionStage.Entry, OccurrenceIndex: entry.Index, ThrowStone: t.Failure));
                    stone = t.Outcome!; current = t.RngState;
                    if (stone.BestCandidateIndex is { } ti)
                    {
                        var c = stone.Candidates[ti]; best = new(entry.Index, entry.Source.Kind, c.Candidate.Destination, c.Candidate.ActBeforeMove,
                            c.Candidate.TargetId, entry.Source.AbilityId, c.Evaluation.Score!, c.Proximity);
                    }
                    break;
                case AiHumanAbilityKind.Dash:
                    if (!profile.OriginalInstantEnemyDashPolicy || profile.Dash is null)
                        return Reject(new(RosterBasicSkillAiSelectionStage.Entry, OccurrenceIndex: entry.Index));
                    var d = RosterDashAiEntry.EvaluateFresh(state, request.ActorId, entry.Index, simulation, profile.Dash, m, current, workspace);
                    if (!d.Accepted) return Reject(new(RosterBasicSkillAiSelectionStage.Entry, OccurrenceIndex: entry.Index, Dash: d.Failure));
                    dash = d.Outcome!; current = d.RngState;
                    if (dash.BestCandidateIndex is { } di)
                    {
                        var c = dash.Candidates[di]; best = new(entry.Index, entry.Source.Kind, c.Candidate.Destination, c.Candidate.ActBeforeMove,
                            c.Candidate.TargetId, entry.Source.AbilityId, c.Evaluation.Score!, c.Proximity);
                    }
                    break;
                default: throw new InvalidOperationException("An unsupported occurrence passed owned list admission.");
            }
            int? insertion = best is null ? null : AiRankedInsertion.Insert(best, m.Movement[m.BaselineIndex].Outcome.Score,
                ranked, c => c.Score, c => c.Proximity);
            entries.Add(new(entry, entryRng, current, attack, self, stone, best, insertion, dash));
        }
        var lotteryRng = current;
        var lottery = AiRankedLottery.Evaluate(ranked.Select(r => r?.Score.HitPercent ?? (byte)0).ToImmutableArray(), current);
        if (!lottery.Accepted) return Reject(new(RosterBasicSkillAiSelectionStage.Lottery, LotteryIndex: lottery.FailedIndex));
        var l = lottery.Outcome!; var selected = l.SelectedIndex is { } i ? ranked[i] : null;
        var plan = new RosterBasicSkillAiSelectedPlan(request.ActorId, selected?.Destination ?? m.BaselineDestination, selected);
        var successor = workspace.Write(m.Movement.Select(t => KeyValuePair.Create(t.Outcome.Destination, t.TilePriority)));
        return new(true, null, new(m, entries.ToImmutable(), ranked.ToImmutableArray(), lotteryRng, l.Rolls, l.SelectedIndex, l.Reason, plan), lottery.RngState, successor);
    }
}
