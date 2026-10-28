namespace Zodiac.Framework.Ai;

/// <summary>Remaining policy assertions; fresh cache, focus, active-target and ranking-entry facts are derived.</summary>
public sealed record SwordAiPreparedSelectionProfile(bool NormalScoring = false, bool CanonicalCoordinates = false,
    bool OrdinaryScenarioPolicy = false, bool OrdinaryAttackTilePolicy = false,
    bool TargetSelectionMatchesBaseline = false, bool NoDefendSupport = false);
public enum SwordAiPreparedSelectionStage { Admission, Preparation, Selection }
public enum SwordAiPreparedSelectionRejection { UnsupportedProfile, PreparationRejected, SelectionRejected }

/// <summary>Reason-only diagnostics; no intermediate output or progressed RNG escapes a failure.</summary>
public sealed record SwordAiPreparedSelectionFailure(SwordAiPreparedSelectionRejection Reason, SwordAiPreparedSelectionStage Stage,
    SwordAiPreparationRejection PreparationRejection = SwordAiPreparationRejection.None,
    SwordAiRefreshRejection RefreshRejection = SwordAiRefreshRejection.None,
    SwordAiDerivedSelectionRejection? SelectionRejection = null, SwordAiDerivedSelectionStage? SelectionStage = null);
public sealed record SwordAiPreparedSelectionOutcome(SwordAiPreparationOutcome Preparation, SwordAiDerivedSelectionOutcome Decision)
{
    public SwordAiOutOfReachSelectionOutcome? OutOfReach { get; init; }
}
public sealed record SwordAiPreparedSelectionResult(bool Accepted, SwordAiPreparedSelectionFailure? Failure,
    SwordAiPreparedSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh pure ordinary NonBlue preparation and explicit in/out-of-reach selection on the original live snapshot.</summary>
/// <remarks>
/// Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-160/162/164/171/173.
/// Owns refresh provenance and its retained draw; no cached-result input, command authority or runtime activation.
/// Existing post-prefix execution and its GeneralSelectionEntryContext remain separate.
/// </remarks>
public static class SwordAiPreparedSelection
{
    public static SwordAiPreparedSelectionResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile,
        SwordAiScenarioTeam team, SwordAiPreparedSelectionProfile selectionProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, weapon, candidateProfile, scoreProfile, refreshProfile, preparationProfile,
            team, selectionProfile, decisionRng, outOfReach: false, alreadyMoved: true);

    /// <summary>Explicit fresh out-of-reach branch; preserves the NonBlue post-movement adjustment before fallback.</summary>
    public static SwordAiPreparedSelectionResult EvaluateOutOfReach(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile,
        SwordAiScenarioTeam team, SwordAiPreparedSelectionProfile selectionProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, weapon, candidateProfile, scoreProfile, refreshProfile, preparationProfile,
            team, selectionProfile, decisionRng, outOfReach: true, alreadyMoved: true);

    /// <summary>Own one fresh both-unused preparation and in-reach selection; never adopt the diagnostic clock state.</summary>
    public static SwordAiPreparedSelectionResult EvaluateUnmoved(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile,
        SwordAiScenarioTeam team, SwordAiPreparedSelectionProfile selectionProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, weapon, candidateProfile, scoreProfile, refreshProfile, preparationProfile,
            team, selectionProfile, decisionRng, outOfReach: false, alreadyMoved: false);

    /// <summary>Fresh both-unused preparation and explicit out-of-reach selection, including the post-score team draw.</summary>
    public static SwordAiPreparedSelectionResult EvaluateOutOfReachUnmoved(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile,
        SwordAiScenarioTeam team, SwordAiPreparedSelectionProfile selectionProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, weapon, candidateProfile, scoreProfile, refreshProfile, preparationProfile,
            team, selectionProfile, decisionRng, outOfReach: true, alreadyMoved: false);

    private static SwordAiPreparedSelectionResult EvaluateCore(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, SwordAiPreparationProfile preparationProfile,
        SwordAiScenarioTeam team, SwordAiPreparedSelectionProfile selectionProfile, FftRandomState decisionRng,
        bool outOfReach, bool alreadyMoved)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile);
        ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        SwordAiPreparedSelectionResult Reject(SwordAiPreparedSelectionFailure failure) => new(false, failure, null, decisionRng);
        if (!selectionProfile.NormalScoring || !selectionProfile.CanonicalCoordinates || !selectionProfile.OrdinaryScenarioPolicy ||
            !selectionProfile.OrdinaryAttackTilePolicy || !selectionProfile.TargetSelectionMatchesBaseline || !selectionProfile.NoDefendSupport)
            return Reject(new(SwordAiPreparedSelectionRejection.UnsupportedProfile, SwordAiPreparedSelectionStage.Admission));

        var preparation = alreadyMoved
            ? SwordAiPreparation.Evaluate(state, request, candidateProfile, scoreProfile,
                refreshProfile, preparationProfile, team, decisionRng)
            : SwordAiPreparation.EvaluateUnmoved(state, request, candidateProfile, scoreProfile,
                refreshProfile, preparationProfile, team, decisionRng);
        if (!preparation.Accepted)
            return Reject(new(SwordAiPreparedSelectionRejection.PreparationRejected, SwordAiPreparedSelectionStage.Preparation,
                preparation.Rejection, preparation.RefreshRejection));
        // Only this owned fresh result establishes the cache/focus/reset facts. Never apply its diagnostic clock to live state.
        var ready = preparation.Outcome!;
        var comparison = new SwordAiComparisonProfile(request.ActorId, request.TargetId, FreshDecisionBaseline: true);
        var scenario = new SwordAiMovementScenarioProfile(FocusedOpponent: true, OrdinarySwordReach: true,
            selectionProfile.OrdinaryScenarioPolicy);
        var derived = new SwordAiDerivedSelectionProfile(ready.TargetActive, selectionProfile.NormalScoring,
            selectionProfile.CanonicalCoordinates, selectionProfile.OrdinaryAttackTilePolicy,
            selectionProfile.TargetSelectionMatchesBaseline, FreshAbilityBestReset: true, FreshEmptyRankedList: true, FirstAttackEntry: true);
        var selection = new SwordAiSelectionProfile(TerminalAttackEntry: true, selectionProfile.NoDefendSupport,
            FreshOrdinarySelectionDefaults: true);
        SwordAiDerivedSelectionResult decision;
        SwordAiOutOfReachSelectionOutcome? reachOutcome = null;
        if (outOfReach)
        {
            var result = SwordAiDerivedSelection.EvaluateOutOfReach(state, weapon, candidateProfile, scoreProfile, comparison,
                scenario, derived, selection, team, preparation.RngState);
            reachOutcome = result.Outcome;
            decision = new(result.Accepted, result.Failure, result.Accepted
                ? new(reachOutcome!.MovementScenario.MovementScenario, reachOutcome.Selection) : null, result.RngState);
        }
        else
            decision = SwordAiDerivedSelection.Evaluate(state, weapon, candidateProfile, scoreProfile, comparison,
                scenario, derived, selection, preparation.RngState);
        if (!decision.Accepted)
            return Reject(new(SwordAiPreparedSelectionRejection.SelectionRejected, SwordAiPreparedSelectionStage.Selection,
                SelectionRejection: decision.Failure!.Reason, SelectionStage: decision.Failure.Stage));
        return new(true, null, new(ready, decision.Outcome!) { OutOfReach = reachOutcome }, decision.RngState);
    }
}
