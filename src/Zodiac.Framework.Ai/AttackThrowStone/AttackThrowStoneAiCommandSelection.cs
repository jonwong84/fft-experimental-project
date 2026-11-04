using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Observational decision data; neither branch grants authority or accepts a cached plan as input.</summary>
public abstract record AttackThrowStoneAiCommandSelection(bool OutOfReach)
{
    public abstract AttackThrowStoneAiSelectedPlan Plan { get; }
    public sealed record BeforeAct(bool OutOfReach, AttackThrowStoneAiSelectionOutcome Selection)
        : AttackThrowStoneAiCommandSelection(OutOfReach)
    { public override AttackThrowStoneAiSelectedPlan Plan => Selection.Plan; }
    public sealed record AfterAct(bool OutOfReach, AttackThrowStoneAiPostActSelectionOutcome Selection)
        : AttackThrowStoneAiCommandSelection(OutOfReach)
    { public override AttackThrowStoneAiSelectedPlan Plan => Selection.Plan; }
}

/// <summary>Own one preparation, derive source reach, then continue only that fresh scenario/selection.</summary>
internal static class AttackThrowStoneAiCommandSelectionPipeline
{
    internal sealed record Result(AttackThrowStoneAiCommandFailure? Failure,
        AttackThrowStoneAiCommandSelection? Outcome, RandomState RngState);

    internal static Result Evaluate(AttackBattleSnapshot state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, RandomState rng, bool afterAct)
        => afterAct ? AfterAct(state, request, configuration, rng) : BeforeAct(state, request, configuration, rng);

    private static Result BeforeAct(AttackBattleSnapshot state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, RandomState rng)
    {
        Result Reject(AttackThrowStoneAiPreparedSelectionFailure failure) =>
            new(new(AttackThrowStoneAiCommandRejection.SelectionRejected, BeforeActFailure: failure), null, rng);
        var admission = AttackThrowStoneAiSelectionPipeline.Admit(state, request, configuration);
        if (admission is not null) return Reject(admission);
        var prepared = AttackThrowStoneAiPreparation.Evaluate(state, request.Turn, request.ThrowStoneAbilityId, request.Weapon,
            configuration.Attack, configuration.ThrowStone, configuration.Scoring, configuration.Refresh,
            configuration.Preparation, configuration.Team, rng);
        if (!prepared.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Scenario, AttackThrowStoneAiPreparedSelectionRejection.ScenarioRejected,
                ScenarioFailure: new(AttackThrowStoneAiScenarioStage.Preparation, AttackThrowStoneAiScenarioRejection.PreparationRejected, prepared.Failure)));
        bool outside = IsOutOfReach(state, request, configuration, prepared.Outcome!);
        if (outside)
        {
            var scenario = AttackThrowStoneAiOutOfReachScenario.ContinueFromPreparation(state, request.Turn,
                configuration.Attack, configuration.ThrowStone, prepared, rng);
            var selected = AttackThrowStoneAiOutOfReachSelection.CompleteFresh(state, request, configuration, scenario, rng);
            return selected.Accepted ? new(null, new AttackThrowStoneAiCommandSelection.BeforeAct(true, selected.Outcome!.Selection), selected.RngState)
                : Reject(selected.Failure!);
        }
        else
        {
            var scenario = AttackThrowStoneAiMovementScenario.ContinueFromPreparation(state, request.Turn,
                configuration.Attack, configuration.ThrowStone, prepared, rng);
            var selected = AttackThrowStoneAiPreparedSelection.CompleteFresh(state, request, configuration, scenario, rng);
            return selected.Accepted ? new(null, new AttackThrowStoneAiCommandSelection.BeforeAct(false, selected.Outcome!.Selection), selected.RngState)
                : Reject(selected.Failure!);
        }
    }

    private static Result AfterAct(AttackBattleSnapshot state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, RandomState rng)
    {
        Result Reject(AttackThrowStoneAiPostActSelectionFailure failure) =>
            new(new(AttackThrowStoneAiCommandRejection.SelectionRejected, AfterActFailure: failure), null, rng);
        if (!configuration.Selection.NoDefendSupport)
            return Reject(new(AttackThrowStoneAiPostActSelectionRejection.UnsupportedProfile));
        var policy = configuration.Selection;
        if (!policy.NormalScoring || !policy.CanonicalCoordinates || !policy.NoOtherScenarioOverrides)
            return Reject(new(AttackThrowStoneAiPostActSelectionRejection.ScenariosRejected,
                new(AttackThrowStoneAiScenarioStage.Admission, AttackThrowStoneAiScenarioRejection.UnsupportedProfile)));
        var prepared = AttackThrowStoneAiPreparation.EvaluateAfterAct(state, request, configuration, rng);
        if (!prepared.Accepted)
            return Reject(new(AttackThrowStoneAiPostActSelectionRejection.ScenariosRejected,
                new(AttackThrowStoneAiScenarioStage.Preparation, AttackThrowStoneAiScenarioRejection.PreparationRejected, prepared.Failure)));
        bool outside = IsOutOfReach(state, request, configuration, prepared.Outcome!);
        var scenario = AttackThrowStoneAiPostActScenarios.ContinueFromPreparation(state, request, configuration, prepared, rng, outside);
        var selected = AttackThrowStoneAiPostActSelection.CompleteFresh(request, scenario, rng);
        return selected.Accepted ? new(null, new AttackThrowStoneAiCommandSelection.AfterAct(outside, selected.Outcome!), selected.RngState)
            : Reject(selected.Failure!);
    }

    private static bool IsOutOfReach(AttackBattleSnapshot state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, AttackThrowStoneAiPreparationOutcome preparation)
    {
        // Pure shared geometry: no refresh/scoring/draw. Branch on prepared reach BEFORE the later NonBlue halving draw.
        var geometry = FlatOrdinaryMovement.Evaluate(state, request.Turn.ActorId, configuration.Attack.Movement).Outcome!;
        var origin = preparation.Common.Origin;
        int distance = geometry.TargetDistanceRows.Single(r => r.Y == origin.Y).Distances[origin.X];
        return distance > preparation.Common.EnemyAbilityReach;
    }
}
