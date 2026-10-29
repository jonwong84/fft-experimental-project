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
    public static AttackThrowStoneAiPreparedSelectionResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiPreparedSelectionProfile selectionProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(attackProfile);
        ArgumentNullException.ThrowIfNull(throwStoneProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        AttackThrowStoneAiPreparedSelectionResult Reject(AttackThrowStoneAiPreparedSelectionFailure failure) => new(false, failure, null, decisionRng);
        if (!selectionProfile.NormalScoring || !selectionProfile.CanonicalCoordinates || !selectionProfile.NoOtherScenarioOverrides ||
            !selectionProfile.ClearDirectTrajectories || !selectionProfile.NoDefendSupport)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Admission, AttackThrowStoneAiPreparedSelectionRejection.UnsupportedProfile));
        var scenario = AttackThrowStoneAiMovementScenario.EvaluateFresh(state, request, throwStoneAbilityId, weapon, attackProfile,
            throwStoneProfile, scoreProfile, refreshProfile, preparationProfile,
            new(selectionProfile.NormalScoring, selectionProfile.CanonicalCoordinates, selectionProfile.NoOtherScenarioOverrides), team, decisionRng);
        if (!scenario.Result.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Scenario, AttackThrowStoneAiPreparedSelectionRejection.ScenarioRejected,
                ScenarioFailure: scenario.Result.Failure));
        var fresh = scenario.Result.Outcome!;
        var profile = CreateCompositionProfile(fresh.Preparation, attackProfile, throwStoneProfile, scoreProfile, selectionProfile);
        var scanRequest = new ThrowStoneAiScanRequest(request.ActorId, throwStoneAbilityId, request.TargetId);
        var invalid = AttackThrowStoneAiComposition.ValidateInputs(state, scanRequest, fresh.Scenario, weapon, profile, decisionRng);
        if (invalid is not null)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Composition, AttackThrowStoneAiPreparedSelectionRejection.CompositionRejected,
                CompositionFailure: invalid.Failure));
        var composition = AttackThrowStoneAiComposition.ContinueFromMovement(state, scanRequest, fresh.Scenario, weapon, profile,
            scenario.Prefix!, scenario.Result.RngState, decisionRng);
        if (!composition.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Composition, AttackThrowStoneAiPreparedSelectionRejection.CompositionRejected,
                CompositionFailure: composition.Failure));
        var selected = AttackThrowStoneAiSelection.SelectFresh(composition, request.ActorId, fresh.Scenario.BaselineDestination, decisionRng);
        if (!selected.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Selection, AttackThrowStoneAiPreparedSelectionRejection.SelectionRejected,
                SelectionFailure: selected.Failure));
        return new(true, null, new(fresh, selected.Outcome!), selected.RngState);
    }

    // Both owning branches derive source policy from their private fresh preparation, never from caller-populated diagnostics.
    internal static AttackThrowStoneAiProfile CreateCompositionProfile(AttackThrowStoneAiPreparationOutcome preparation,
        SwordAiCandidateProfile attackProfile, ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile,
        AttackThrowStoneAiPreparedSelectionProfile selectionProfile)
    {
        var entries = preparation.ActorEntries;
        var attack = entries[0]; var stone = entries[1];
        bool sourceOrder = attack.Kind == AttackThrowStoneAiEntryKind.Attack && stone.Kind == AttackThrowStoneAiEntryKind.ThrowStone;
        bool attackPolicy = attack.Usable && attack.Range == 1 && attack.Area == 0 && attack.ExhaustiveOriginScan;
        bool stonePolicy = stone.Usable && stone.Range == 4 && stone.Area == 0 && stone.Ct == 0 && stone.MpCost == 0 &&
            !stone.ExhaustiveOriginScan && stone.ConsumeEvaluatedAimTiles;
        bool active = preparation.Common.TargetActive;
        return new(attackProfile, throwStoneProfile, scoreProfile,
            new(active, attackPolicy, true, true, true),
            new(active, true, true, stonePolicy, true, true, true, selectionProfile.ClearDirectTrajectories),
            new(true, true, selectionProfile.NormalScoring, selectionProfile.CanonicalCoordinates), sourceOrder, true);
    }
}
