using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Post-ACT movement diagnostics: D0=D1, full source masks/provenance and an optional post-score reach adjustment.</summary>
public sealed record AttackThrowStoneAiPostActScenarioOutcome(AttackThrowStoneAiPreparationOutcome Preparation,
    ImmutableArray<TilePosition> ScoreMask, ImmutableArray<TilePosition> Scenario1, ImmutableArray<TilePosition> Scenario2,
    ImmutableArray<AiScoredMovementTile> Movement, ImmutableArray<AiMovementCellProvenance> Cells,
    TilePosition Destination, int BaselineIndex, int TargetDistance, int TargetReach, int EffectiveReach,
    SwordAiReachAdjustment? ReachAdjustment, bool FilterUsedReachableFallback, bool DestinationUsedDistanceFallback)
{
    public TilePosition D0 => Destination;
    public TilePosition D1 => Destination;
}
/// <summary>Failure hides the entire owned preparation/scoring prefix and restores incoming query RNG.</summary>
public sealed record AttackThrowStoneAiPostActScenarioResult(bool Accepted, AttackThrowStoneAiScenarioFailure? Failure,
    AttackThrowStoneAiPostActScenarioOutcome? Outcome, FftRandomState RngState);

/// <summary>Own fresh pair post-ACT preparation and movement-only scenarios through shared source kernels.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-209/211/213.
/// No Attack-only certificate, cached prefix, action scan, lottery, live RNG adoption or command authority.</remarks>
public static class AttackThrowStoneAiPostActScenarios
{
    public static AttackThrowStoneAiPostActScenarioResult EvaluateInReach(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration, FftRandomState decisionRng) =>
        EvaluateCore(state, request, configuration, decisionRng, outOfReach: false);

    public static AttackThrowStoneAiPostActScenarioResult EvaluateOutOfReach(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration, FftRandomState decisionRng) =>
        EvaluateCore(state, request, configuration, decisionRng, outOfReach: true);

    private static AttackThrowStoneAiPostActScenarioResult EvaluateCore(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration, FftRandomState decisionRng, bool outOfReach)
    {
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state, request, configuration);
        AttackThrowStoneAiPostActScenarioResult Reject(AttackThrowStoneAiScenarioFailure failure) => new(false, failure, null, decisionRng);
        var policy = configuration.Selection;
        if (!policy.NormalScoring || !policy.CanonicalCoordinates || !policy.NoOtherScenarioOverrides)
            return Reject(new(AttackThrowStoneAiScenarioStage.Admission, AttackThrowStoneAiScenarioRejection.UnsupportedProfile));
        var preparation = AttackThrowStoneAiPreparation.EvaluateAfterAct(state, request, configuration, decisionRng);
        if (outOfReach)
        {
            var result = AttackThrowStoneAiOutOfReachScenario.ContinueFromPreparation(state, request.Turn,
                configuration.Attack, configuration.ThrowStone, preparation, decisionRng).Result;
            if (!result.Accepted) return Reject(result.Failure!);
            var fresh = result.Outcome!;
            return new(true, null, new(fresh.Preparation, fresh.ScoreMask, fresh.Scenario1, fresh.Scenario2, fresh.Movement, fresh.Cells,
                fresh.BaselineDestination, fresh.BaselineIndex, fresh.TargetDistance, fresh.TargetReach, fresh.EffectiveReach,
                fresh.ReachAdjustment, fresh.FilterUsedReachableFallback, fresh.DestinationUsedDistanceFallback), result.RngState);
        }
        else
        {
            var result = AttackThrowStoneAiMovementScenario.ContinueFromPreparation(state, request.Turn,
                configuration.Attack, configuration.ThrowStone, preparation, decisionRng).Result;
            if (!result.Accepted) return Reject(result.Failure!);
            var fresh = result.Outcome!;
            // The admitted flat in-reach filter covers all reachable cells; every diagnostic priority is freshly written.
            var cells = fresh.Movement.Select(m => new AiMovementCellProvenance(m.Outcome.Destination, m.Proximity, m.TilePriority)).ToImmutableArray();
            return new(true, null, new(fresh.Preparation, fresh.Scenario.ScoredOrigins, fresh.Scenario.ScenarioOrigins, [], fresh.Movement, cells,
                fresh.Scenario.BaselineDestination, fresh.BaselineIndex, fresh.TargetDistance, fresh.TargetReach, fresh.TargetReach,
                null, false, false), result.RngState);
        }
    }
}
