using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Search the prepared ability list, rank each best candidate and select an action or the movement fallback.</summary>
/// <remarks>
/// Supported general_select / ability_list_evaluate path, followed by ranked_select.
/// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, src/battle/ai/turn.c.
/// Movement scenarios are prepared by the caller. Charging/status policies and frame suspension remain outside admission.
/// The decision maker owns atomic rejection and publishes priority knowledge only after selection succeeds.
/// </remarks>
internal static class AiGeneralActionSelection
{
    internal static (AiDecision? Decision, AiDecisionFailure? Failure, RandomState RngState) Select(
        AttackBattleSnapshot state, BattleUnitId actorId, AiDecisionConfiguration configuration,
        RosterAiGeneralScenarioOutcome scenarios, RandomState rng, AiPriorityWorkspace workspace)
    {
        (AiDecision?, AiDecisionFailure?, RandomState) Reject(AiDecisionFailure failure) => (null, failure, rng);
        var current = rng;
        var ranked = new AiRankedAbility?[8];
        var entries = ImmutableArray.CreateBuilder<AiAbilityEvaluation>();
        var baseline = scenarios.Movement[scenarios.BaselineIndex].Outcome.Score;

        foreach (var entry in scenarios.Preparation.Preparation.ActorEntries)
        {
            var evaluated = AiAbilityEvaluator.Evaluate(state, actorId, entry, configuration, scenarios, current, workspace);
            if (evaluated.Failure is { } failure)
            {
                return Reject(failure);
            }

            var evaluation = evaluated.Evaluation!;
            current = evaluation.ExitRng;
            int? insertion = null;
            if (evaluation.Best is { } best)
            {
                insertion = AiRankedInsertion.Insert(best, baseline, ranked, candidate => candidate.Score,
                    candidate => candidate.Proximity);
            }
            entries.Add(evaluation with { InsertionIndex = insertion });
        }

        var lotteryEntryRng = current;
        var lottery = AiRankedLottery.Evaluate(
            ranked.Select(candidate => candidate?.Score.HitPercent ?? (byte)0).ToImmutableArray(), current);
        if (!lottery.Accepted)
        {
            return Reject(new(AiDecisionStage.Lottery, LotteryIndex: lottery.FailedIndex));
        }

        var selected = lottery.Outcome!;
        AiRankedAbility? action = null;
        if (selected.SelectedIndex is { } index)
        {
            action = ranked[index];
        }
        var plan = new AiActionPlan(actorId, action?.Destination ?? scenarios.BaselineDestination, action);
        var decision = new AiDecision(scenarios, entries.ToImmutable(), ranked.ToImmutableArray(), lotteryEntryRng,
            selected.Rolls, selected.SelectedIndex, selected.Reason, plan);
        return (decision, null, lottery.RngState);
    }
}
