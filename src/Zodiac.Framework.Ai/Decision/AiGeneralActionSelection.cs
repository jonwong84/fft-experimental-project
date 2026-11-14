using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Build movement scenarios, search the prepared abilities and select a ranked action or movement fallback.</summary>
/// <remarks>
/// Supported general_select / ability_list_evaluate path, followed by ranked_select.
/// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, src/battle/ai/turn.c.
/// Fresh roster preparation precedes this routine; scenarios are built here before abilities and the lottery.
/// Charging/status policies and frame suspension remain outside admission.
/// The decision maker owns atomic rejection and publishes priority knowledge only after selection succeeds.
/// </remarks>
internal static class AiGeneralActionSelection
{
    internal static (AiDecision? Decision, AiDecisionFailure? Failure, RandomState RngState) GeneralSelect(
        AttackBattleSnapshot state, RosterAiRefreshRequest request, AiDecisionConfiguration configuration,
        RosterAiGeneralPreparationOutcome preparation, RandomState rng, AiPriorityWorkspace workspace)
    {
        (AiDecision?, AiDecisionFailure?, RandomState) Reject(AiDecisionFailure failure) => (null, failure, rng);
        var built = RosterAiGeneralScenarios.ScenariosBuild(state, request, configuration.Simulation,
            configuration.Lists, preparation, rng);
        if (!built.Accepted)
        {
            return Reject(new(AiDecisionStage.Prefix, built.Failure));
        }

        var scenarios = built.Outcome!;
        var ranked = new AiRankedAbility?[8];
        var abilities = AbilityListEvaluate(state, request.ActorId, configuration, scenarios, ranked, built.RngState, workspace);
        if (abilities.Failure is { } failure)
        {
            return Reject(failure);
        }

        var lotteryEntryRng = abilities.RngState;
        var lottery = AiRankedLottery.RankedSelect(
            ranked.Select(candidate => candidate?.Score.HitPercent ?? (byte)0).ToImmutableArray(), lotteryEntryRng);
        if (!lottery.Accepted)
        {
            return Reject(new(AiDecisionStage.Lottery, LotteryIndex: lottery.FailedIndex));
        }

        var selected = lottery.Outcome!;
        AiActionPlan plan;
        if (selected.SelectedIndex is { } index)
        {
            var action = ranked[index]!;
            plan = new(request.ActorId, action.Destination, action);
        }
        else
        {
            plan = FallbackSelect(request.ActorId, scenarios);
        }
        var decision = new AiDecision(scenarios, abilities.Entries, ranked.ToImmutableArray(), lotteryEntryRng,
            selected.Rolls, selected.SelectedIndex, selected.Reason, plan);
        return (decision, null, lottery.RngState);
    }

    /// <summary>ability_list_evaluate: visit prepared occurrences in order, retaining one decision RNG.</summary>
    private static (ImmutableArray<AiAbilityEvaluation> Entries, AiDecisionFailure? Failure, RandomState RngState)
        AbilityListEvaluate(AttackBattleSnapshot state, BattleUnitId actorId, AiDecisionConfiguration configuration,
            RosterAiGeneralScenarioOutcome scenarios, AiRankedAbility?[] ranked, RandomState rng, AiPriorityWorkspace workspace)
    {
        var entries = ImmutableArray.CreateBuilder<AiAbilityEvaluation>();
        var current = rng;
        foreach (var entry in scenarios.Preparation.Preparation.ActorEntries)
        {
            var evaluated = AiAbilityEvaluator.AiAbilityOutcomeEvaluate(state, actorId, entry, configuration,
                scenarios, current, workspace, ranked);
            if (evaluated.Failure is { } failure)
            {
                return ([], failure, rng);
            }

            var evaluation = evaluated.Evaluation!;
            current = evaluation.ExitRng;
            entries.Add(evaluation);
        }
        return (entries.ToImmutable(), null, current);
    }

    /// <summary>fallback_select: select the prepared movement baseline; Defend support is excluded by admission.</summary>
    private static AiActionPlan FallbackSelect(BattleUnitId actorId, RosterAiGeneralScenarioOutcome scenarios) =>
        new(actorId, scenarios.BaselineDestination, null);
}
