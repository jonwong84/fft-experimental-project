using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One fresh whole-roster prefix, supported structural ability list, private ranks and source lottery. No command authority.</summary>
/// <remarks>ZOD-302/303; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Every hypothesis restores state while decision RNG chains.</remarks>
public static class AiDecisionMaker
{
    public static AiDecisionResult Decide(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        AiDecisionConfiguration configuration, RandomState rng, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration); configuration.Validate();
        var profile = configuration.Abilities;
        workspace ??= AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map); workspace.ValidateMap(state);
        AiDecisionResult Reject(AiDecisionFailure failure) => new(false, failure, null, rng, workspace);
        if (!profile.NoDefendSupport || !profile.FreshOrdinarySelectionDefaults) return Reject(new(AiDecisionStage.Admission));
        var prefix = RosterAiGeneralScenarios.Evaluate(state, request, configuration.Simulation, configuration.Refresh,
            configuration.Lists, configuration.General, configuration.Movement, rng);
        if (!prefix.Accepted) return Reject(new(AiDecisionStage.Prefix, prefix.Failure));
        var m = prefix.Outcome!; var current = prefix.RngState;
        var ranked = new AiRankedAbility?[8]; var entries = ImmutableArray.CreateBuilder<AiAbilityEvaluation>();
        foreach (var entry in m.Preparation.Preparation.ActorEntries)
        {
            var evaluated = AiAbilityEvaluator.Evaluate(state, request.ActorId, entry, configuration, m, current, workspace);
            if (evaluated.Failure is { } failure) return Reject(failure);
            var evaluation = evaluated.Evaluation!; current = evaluation.ExitRng;
            var best = evaluation.Best;
            int? insertion = best is null ? null : AiRankedInsertion.Insert(best, m.Movement[m.BaselineIndex].Outcome.Score,
                ranked, c => c.Score, c => c.Proximity);
            entries.Add(evaluation with { InsertionIndex = insertion });
        }

        var lotteryRng = current;
        var lottery = AiRankedLottery.Evaluate(ranked.Select(r => r?.Score.HitPercent ?? (byte)0).ToImmutableArray(), current);
        if (!lottery.Accepted) return Reject(new(AiDecisionStage.Lottery, LotteryIndex: lottery.FailedIndex));
        var l = lottery.Outcome!; var selected = l.SelectedIndex is { } i ? ranked[i] : null;
        var plan = new AiActionPlan(request.ActorId, selected?.Destination ?? m.BaselineDestination, selected);
        var successor = workspace.Write(m.Movement.Select(t => KeyValuePair.Create(t.Outcome.Destination, t.TilePriority)));
        return new(true, null, new(m, entries.ToImmutable(), ranked.ToImmutableArray(), lotteryRng, l.Rolls, l.SelectedIndex, l.Reason, plan), lottery.RngState, successor);
    }
}
