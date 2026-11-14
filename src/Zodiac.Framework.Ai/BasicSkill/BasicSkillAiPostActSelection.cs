using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Post-ACT policy assertions; no pre-ACT supplied scenarios or action-scan certificates are consumed.</summary>
public sealed record BasicSkillAiPostActConfiguration(AccumulateAiPreparationConfiguration Preparation,
    SwordAiPostActScenarioProfile Scenarios, bool SourceGeneralEntry = false, bool FreshEmptyRankedList = false,
    bool NoDefendSupport = false);
public enum BasicSkillAiPostActStage { Admission, Preparation, Movement, Lottery }
public sealed record BasicSkillAiPostActFailure(BasicSkillAiPostActStage Stage,
    AccumulateAiPreparationFailure? Preparation = null, SwordAiMovementScenarioFailure? Movement = null, int? LotteryIndex = null);
public sealed record BasicSkillAiPostActOutcome(AccumulateAiPreparationOutcome Preparation,
    SwordAiMovementScenarioOutcome MovementScenario, SwordAiOutOfReachScenarioOutcome? OutOfReach,
    RandomState LotteryEntryRng, ImmutableArray<AiLotteryRoll> Lottery, AiLotteryReason Reason, BasicSkillAiSelectedPlan Plan);
/// <summary>Failure discards the whole privately owned refresh/movement/lottery and restores incoming RNG.</summary>
public sealed record BasicSkillAiPostActResult(bool Accepted, BasicSkillAiPostActFailure? Failure,
    BasicSkillAiPostActOutcome? Outcome, RandomState RngState);

/// <summary>Fresh three-entry post-ACT preparation, derived movement scenarios and empty-ranking fallback.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-243–245/247/248. No cached inputs, action scan,
/// usefulness coin, pre-ACT scenario derivation or live command authority.</remarks>
internal static class BasicSkillAiPostActSelection
{
    /// <summary>Derive reach from one freshly prepared actual post-ACT state, then select exactly one movement branch.</summary>
    public static BasicSkillAiPostActResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiPostActConfiguration configuration, RandomState decisionRng) => EvaluateCore(state, request, configuration, decisionRng, null);

    public static BasicSkillAiPostActResult EvaluateInReach(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiPostActConfiguration configuration, RandomState decisionRng) => EvaluateCore(state, request, configuration, decisionRng, false);

    public static BasicSkillAiPostActResult EvaluateOutOfReach(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiPostActConfiguration configuration, RandomState decisionRng) => EvaluateCore(state, request, configuration, decisionRng, true);

    private static BasicSkillAiPostActResult EvaluateCore(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiPostActConfiguration configuration, RandomState decisionRng, bool? outside)
    {
        ValidateArguments(state, request, configuration);
        BasicSkillAiPostActResult Reject(BasicSkillAiPostActFailure failure) => new(false, failure, null, decisionRng);
        var s = configuration.Scenarios;
        if (!configuration.SourceGeneralEntry || !configuration.FreshEmptyRankedList || !configuration.NoDefendSupport ||
            !s.NormalScoring || !s.CanonicalCoordinates || !s.OrdinaryScenarioPolicy)
            return Reject(new(BasicSkillAiPostActStage.Admission));
        var p = configuration.Preparation;
        var preparation = AccumulateAiPreparation.EvaluateAfterAct(state, request, p, decisionRng);
        if (!preparation.Accepted) return Reject(new(BasicSkillAiPostActStage.Preparation, Preparation: preparation.Failure));
        if (outside is null)
        {
            // Pure geometry adds no draw. Branch on prepared reach before the later NonBlue halving roll.
            var geometry = FlatOrdinaryMovement.Evaluate(state, request.Turn.ActorId, p.Attack.Movement).Outcome!;
            var common = preparation.Outcome!.Common;
            int distance = geometry.TargetDistanceRows.Single(r => r.Y == common.Origin.Y).Distances[common.Origin.X];
            outside = distance > common.EnemyAbilityReach;
        }
        // The closed three-entry preparation derives minimum enemy range1. These kernels require no Attack-only list.
        var comparison = new SwordAiComparisonProfile(request.Turn.ActorId, request.Turn.TargetId, true);
        var movementProfile = new SwordAiMovementScenarioProfile(true, true, true);
        SwordAiMovementScenarioResult movement;
        SwordAiOutOfReachScenarioOutcome? reach = null;
        if (outside.Value)
        {
            var selected = SwordAiMovementScenario.EvaluateOutOfReachAfterAct(state, p.Attack, p.Scoring,
                comparison, movementProfile, p.Team, preparation.RngState);
            reach = selected.Outcome;
            movement = new(selected.Accepted, selected.Failure, reach?.MovementScenario, selected.RngState);
        }
        else movement = SwordAiMovementScenario.EvaluateAfterAct(state, p.Attack, p.Scoring, comparison, movementProfile, preparation.RngState);
        if (!movement.Accepted) return Reject(new(BasicSkillAiPostActStage.Movement, Movement: movement.Failure));
        // All three actor usable bits are cleared before the outcome sign-bit guard. No self coin or action scan can run.
        var lottery = AiRankedLottery.RankedSelect(Enumerable.Repeat((byte)0, 8).ToImmutableArray(), movement.RngState);
        if (!lottery.Accepted) return Reject(new(BasicSkillAiPostActStage.Lottery, LotteryIndex: lottery.FailedIndex));
        var fresh = movement.Outcome!;
        var plan = new BasicSkillAiSelectedPlan(request.Turn.ActorId, fresh.Scenario.BaselineDestination, null);
        return new(true, null, new(preparation.Outcome!, fresh, reach, movement.RngState,
            lottery.Outcome!.Rolls, lottery.Outcome.Reason, plan), lottery.RngState);
    }

    internal static void ValidateArguments(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        BasicSkillAiPostActConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(configuration.Scenarios);
        AccumulateAiPreparation.ValidateArguments(state, request, configuration.Preparation);
    }
}
