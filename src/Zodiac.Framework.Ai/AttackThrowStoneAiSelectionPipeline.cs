namespace Zodiac.Framework.Ai;

/// <summary>Per-decision actor/target policy, ability identity and equipped weapon; no cached query or authority.</summary>
public sealed record AttackThrowStoneAiSelectionRequest(SwordAiRefreshRequest Turn, AssetId ThrowStoneAbilityId,
    BoundedSwordWeapon? Weapon);

/// <summary>Reusable immutable rules and explicit source assertions for the bounded pair, independent of state/RNG.</summary>
public sealed record AttackThrowStoneAiSelectionConfiguration(SwordAiCandidateProfile Attack,
    ThrowStoneAiCandidateProfile ThrowStone, AiScoreProfile Scoring, SwordAiRefreshProfile Refresh,
    AttackThrowStoneAiPreparationProfile Preparation, AttackThrowStoneAiPreparedSelectionProfile Selection,
    SwordAiScenarioTeam Team);

/// <summary>Shared admission and post-scenario composition/lottery; owning branches retain their fresh scenario rules.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; behavior-preserving ZOD-210 refactor of ZOD-198/202.
/// Only internal owners pass a fresh movement prefix. Public diagnostics cannot be supplied back.</remarks>
internal static class AttackThrowStoneAiSelectionPipeline
{
    internal sealed record Result(AttackThrowStoneAiPreparedSelectionFailure? Failure,
        AttackThrowStoneAiSelectionOutcome? Outcome, FftRandomState RngState)
    {
        public bool Accepted => Failure is null;
    }

    internal static void ValidateInputs(AttackBattleSnapshot state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(request.Turn);
        ArgumentNullException.ThrowIfNull(configuration.Attack);
        ArgumentNullException.ThrowIfNull(configuration.ThrowStone);
        ArgumentNullException.ThrowIfNull(configuration.Scoring);
        ArgumentNullException.ThrowIfNull(configuration.Refresh);
        ArgumentNullException.ThrowIfNull(configuration.Preparation);
        ArgumentNullException.ThrowIfNull(configuration.Selection);
        // A missing weapon is a supported rejection input, as before the refactor.
    }

    internal static AttackThrowStoneAiPreparedSelectionFailure? Admit(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration)
    {
        ValidateInputs(state, request, configuration);
        var profile = configuration.Selection;
        return profile.NormalScoring && profile.CanonicalCoordinates && profile.NoOtherScenarioOverrides &&
            profile.ClearDirectTrajectories && profile.NoDefendSupport ? null :
            new(AttackThrowStoneAiPreparedSelectionStage.Admission, AttackThrowStoneAiPreparedSelectionRejection.UnsupportedProfile);
    }

    internal static Result Continue(AttackBattleSnapshot state, AttackThrowStoneAiSelectionRequest request,
        AttackThrowStoneAiSelectionConfiguration configuration, AttackThrowStoneAiPreparationOutcome preparation,
        ThrowStoneAiScanScenario scenario, AiMovementPrefixOutcome prefix, FftRandomState scenarioRng, FftRandomState queryRng)
    {
        Result Reject(AttackThrowStoneAiPreparedSelectionFailure failure) => new(failure, null, queryRng);
        var profile = CreateCompositionProfile(preparation, configuration);
        var scanRequest = new ThrowStoneAiScanRequest(request.Turn.ActorId, request.ThrowStoneAbilityId, request.Turn.TargetId);
        var invalid = AttackThrowStoneAiComposition.ValidateInputs(state, scanRequest, scenario, request.Weapon, profile, queryRng);
        if (invalid is not null)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Composition, AttackThrowStoneAiPreparedSelectionRejection.CompositionRejected,
                CompositionFailure: invalid.Failure));
        // Reuse the owning branch's prefix and RNG, including its reach draw; never rescore or reconstruct from public diagnostics.
        var composition = AttackThrowStoneAiComposition.ContinueFromMovement(state, scanRequest, scenario, request.Weapon, profile,
            prefix, scenarioRng, queryRng);
        if (!composition.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Composition, AttackThrowStoneAiPreparedSelectionRejection.CompositionRejected,
                CompositionFailure: composition.Failure));
        var selected = AttackThrowStoneAiSelection.SelectFresh(composition, request.Turn.ActorId, scenario.BaselineDestination, queryRng);
        if (!selected.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Selection, AttackThrowStoneAiPreparedSelectionRejection.SelectionRejected,
                SelectionFailure: selected.Failure));
        return new(null, selected.Outcome!, selected.RngState);
    }

    // Derive source policy from private fresh preparation, never caller-populated diagnostics.
    private static AttackThrowStoneAiProfile CreateCompositionProfile(AttackThrowStoneAiPreparationOutcome preparation,
        AttackThrowStoneAiSelectionConfiguration configuration)
    {
        var attack = preparation.ActorEntries[0];
        var stone = preparation.ActorEntries[1];
        bool sourceOrder = attack.Kind == AttackThrowStoneAiEntryKind.Attack && stone.Kind == AttackThrowStoneAiEntryKind.ThrowStone;
        bool attackPolicy = attack.Usable && attack.Range == 1 && attack.Area == 0 && attack.ExhaustiveOriginScan;
        bool stonePolicy = stone.Usable && stone.Range == 4 && stone.Area == 0 && stone.Ct == 0 && stone.MpCost == 0 &&
            !stone.ExhaustiveOriginScan && stone.ConsumeEvaluatedAimTiles;
        bool active = preparation.Common.TargetActive;
        return new(configuration.Attack, configuration.ThrowStone, configuration.Scoring,
            new(active, attackPolicy, true, true, true),
            new(active, true, true, stonePolicy, true, true, true, configuration.Selection.ClearDirectTrajectories),
            new(true, true, configuration.Selection.NormalScoring, configuration.Selection.CanonicalCoordinates), sourceOrder, true);
    }
}
