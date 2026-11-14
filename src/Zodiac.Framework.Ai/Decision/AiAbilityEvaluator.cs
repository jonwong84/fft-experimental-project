using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Private effect-family dispatch after one owning fresh prefix. Search rules stay with their ability evaluators.</summary>
internal static class AiAbilityEvaluator
{
    /// <summary>The admitted ai_ability_outcome_evaluate: search one occurrence and insert its best result.</summary>
    internal static (AiDecisionFailure? Failure, AiAbilityEvaluation? Evaluation) AiAbilityOutcomeEvaluate(AttackBattleSnapshot state,
        BattleUnitId actorId, AiPreparedAbility entry, AiDecisionConfiguration configuration,
        RosterAiGeneralScenarioOutcome m, RandomState rng, AiPriorityWorkspace workspace, AiRankedAbility?[] ranked)
    {
        if (!entry.Usable) return (null, new(entry, rng, rng));
        (AiDecisionFailure?, AiAbilityEvaluation?) Reject(AiDecisionFailure failure) => (failure, null);
        var profile = configuration.Abilities; var simulation = configuration.Simulation; var current = rng;
        RosterSwordAiAttackOutcome? attack = null; RosterAccumulateAiEntryOutcome? self = null; RosterThrowStoneAiEntryOutcome? stone = null;
        RosterDashAiEntryOutcome? dash = null;
        AiRankedAbility? best = null;
        switch (entry.Source.Kind)
        {
            case AiHumanAbilityKind.Attack:
                if (!profile.OrdinaryAttackTilePolicy) return Reject(new(AiDecisionStage.Entry, OccurrenceIndex: entry.Index));
                var a = RosterSwordAiAttackEntry.EvaluateFresh(state, actorId, profile.Weapon, simulation, m, current);
                if (!a.Accepted) return Reject(new(AiDecisionStage.Entry, OccurrenceIndex: entry.Index, Attack: a.Failure));
                attack = a.Outcome!; current = a.RngState;
                if (attack.BestCandidateIndex is { } ai)
                {
                    var c = attack.Candidates[ai]; best = new(entry.Index, entry.Source.Kind, c.Candidate.Destination,
                        c.Candidate.Order == SwordAiCandidateOrder.ActThenMove, c.Candidate.TargetId, null, c.Evaluation.Score!, c.Proximity);
                }
                break;
            case AiHumanAbilityKind.Accumulate:
                if (profile.Accumulate is null) return Reject(new(AiDecisionStage.Entry, OccurrenceIndex: entry.Index));
                var s = RosterAccumulateAiEntry.EvaluateFresh(state, actorId, entry.Index, simulation, profile.Accumulate, m, current);
                if (!s.Accepted) return Reject(new(AiDecisionStage.Entry, OccurrenceIndex: entry.Index, Accumulate: s.Failure));
                self = s.Outcome!; current = s.RngState;
                if (self.BestCandidateIndex is { } si)
                {
                    var c = self.Candidates[si]; best = new(entry.Index, entry.Source.Kind, m.ActFirstDestination, c.ActBeforeMove,
                        null, entry.Source.AbilityId, c.Score!, m.Preparation.Proximity.WrittenValues[m.ActFirstDestination]);
                }
                break;
            case AiHumanAbilityKind.ThrowStone:
                if (!profile.OriginalInstantDirectEnemyPolicy || profile.ThrowStone is null || profile.Heights is null)
                    return Reject(new(AiDecisionStage.Entry, OccurrenceIndex: entry.Index));
                var t = RosterThrowStoneAiEntry.EvaluateFresh(state, actorId, entry.Index, simulation, profile.ThrowStone, profile.Heights, m, current, workspace);
                if (!t.Accepted) return Reject(new(AiDecisionStage.Entry, OccurrenceIndex: entry.Index, ThrowStone: t.Failure));
                stone = t.Outcome!; current = t.RngState;
                if (stone.BestCandidateIndex is { } ti)
                {
                    var c = stone.Candidates[ti]; best = new(entry.Index, entry.Source.Kind, c.Candidate.Destination, c.Candidate.ActBeforeMove,
                        c.Candidate.TargetId, entry.Source.AbilityId, c.Evaluation.Score!, c.Proximity);
                }
                break;
            case AiHumanAbilityKind.Dash:
                if (!profile.OriginalInstantEnemyDashPolicy || profile.Dash is null)
                    return Reject(new(AiDecisionStage.Entry, OccurrenceIndex: entry.Index));
                var d = RosterDashAiEntry.EvaluateFresh(state, actorId, entry.Index, simulation, profile.Dash, m, current, workspace);
                if (!d.Accepted) return Reject(new(AiDecisionStage.Entry, OccurrenceIndex: entry.Index, Dash: d.Failure));
                dash = d.Outcome!; current = d.RngState;
                if (dash.BestCandidateIndex is { } di)
                {
                    var c = dash.Candidates[di]; best = new(entry.Index, entry.Source.Kind, c.Candidate.Destination, c.Candidate.ActBeforeMove,
                        c.Candidate.TargetId, entry.Source.AbilityId, c.Evaluation.Score!, c.Proximity);
                }
                break;
            default: throw new InvalidOperationException("An unsupported occurrence passed owned list admission.");
        }
        int? insertion = null;
        if (best is not null)
        {
            insertion = AiRankedInsertion.AiBestRankInsert(best, m.Movement[m.BaselineIndex].Outcome.Score,
                ranked, candidate => candidate.Score, candidate => candidate.Proximity);
        }
        return (null, new(entry, rng, current, attack, self, stone, best, insertion, dash));
    }
}
