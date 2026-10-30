namespace Zodiac.Framework.Ai;

/// <summary>Remaining source facts absent from fresh preparation/scenario data; all require explicit opt-in.</summary>
public sealed record AttackThrowStoneAiPreparedSelectionProfile(bool NormalScoring = false, bool CanonicalCoordinates = false,
    bool NoOtherScenarioOverrides = false, bool ClearDirectTrajectories = false, bool NoDefendSupport = false);
public enum AttackThrowStoneAiPreparedSelectionStage { Admission, Scenario, Composition, Selection, Provenance }
public enum AttackThrowStoneAiPreparedSelectionRejection { UnsupportedProfile, ScenarioRejected, CompositionRejected, SelectionRejected, UnwrittenCastPriority }
public sealed record AttackThrowStoneAiPreparedSelectionFailure(AttackThrowStoneAiPreparedSelectionStage Stage,
    AttackThrowStoneAiPreparedSelectionRejection Reason, AttackThrowStoneAiScenarioFailure? ScenarioFailure = null,
    AttackThrowStoneAiFailure? CompositionFailure = null, AttackThrowStoneAiSelectionFailure? SelectionFailure = null);
public sealed record AttackThrowStoneAiPreparedSelectionOutcome(AttackThrowStoneAiMovementScenarioOutcome MovementScenario,
    AttackThrowStoneAiSelectionOutcome Selection);
/// <summary>Failure publishes no partial preparation/masks/ranks/plan and restores original whole-query RNG.</summary>
public sealed record AttackThrowStoneAiPreparedSelectionResult(bool Accepted, AttackThrowStoneAiPreparedSelectionFailure? Failure,
    AttackThrowStoneAiPreparedSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh both-unused NonBlue in-reach pair decision, owning preparation, scenarios, both scans and lottery exactly once.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-195/198.
/// Hypothetical only: no cached inputs, out-of-reach branch, command/session authority or live RNG adoption.</remarks>
public static class AttackThrowStoneAiPreparedSelection
{
    public static AttackThrowStoneAiPreparedSelectionResult Evaluate(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration, FftRandomState decisionRng)
    {
        AttackThrowStoneAiPreparedSelectionResult Reject(AttackThrowStoneAiPreparedSelectionFailure failure) => new(false, failure, null, decisionRng);
        var admission = AttackThrowStoneAiSelectionPipeline.Admit(state, request, configuration);
        if (admission is not null) return Reject(admission);
        var policy = configuration.Selection;
        var scenario = AttackThrowStoneAiMovementScenario.EvaluateFresh(state, request.Turn, request.ThrowStoneAbilityId, request.Weapon,
            configuration.Attack, configuration.ThrowStone, configuration.Scoring, configuration.Refresh, configuration.Preparation,
            new(policy.NormalScoring, policy.CanonicalCoordinates, policy.NoOtherScenarioOverrides), configuration.Team, decisionRng);
        if (!scenario.Result.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Scenario, AttackThrowStoneAiPreparedSelectionRejection.ScenarioRejected,
                ScenarioFailure: scenario.Result.Failure));
        var fresh = scenario.Result.Outcome!;
        var selected = AttackThrowStoneAiSelectionPipeline.Continue(state, request, configuration, fresh.Preparation,
            fresh.Scenario, scenario.Prefix!, scenario.Result.RngState, decisionRng);
        if (!selected.Accepted) return Reject(selected.Failure!);
        return new(true, null, new(fresh, selected.Outcome!), selected.RngState);
    }
}
