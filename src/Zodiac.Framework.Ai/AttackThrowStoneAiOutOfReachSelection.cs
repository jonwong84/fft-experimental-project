using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Full source masks/provenance plus the scan-equivalent fresh-origin mask and a hypothetical selection.</summary>
public sealed record AttackThrowStoneAiOutOfReachSelectionOutcome(AttackThrowStoneAiOutOfReachScenarioOutcome MovementScenario,
    ImmutableArray<TilePosition> ScanOrigins, AttackThrowStoneAiSelectionOutcome Selection);
/// <summary>Failure publishes reasons only and restores whole-query RNG, including after private masks/ranks/rolls.</summary>
public sealed record AttackThrowStoneAiOutOfReachSelectionResult(bool Accepted, AttackThrowStoneAiPreparedSelectionFailure? Failure,
    AttackThrowStoneAiOutOfReachSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Own fresh NonBlue out-of-reach pair selection only when every cast key is fresh or proved unread.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-199/200/202.
/// No prior-memory inputs, actor rescore, cached results, command authority or live RNG adoption.</remarks>
public static class AttackThrowStoneAiOutOfReachSelection
{
    public static AttackThrowStoneAiOutOfReachSelectionResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, AttackThrowStoneAiPreparedSelectionProfile selectionProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(attackProfile);
        ArgumentNullException.ThrowIfNull(throwStoneProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(preparationProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        AttackThrowStoneAiOutOfReachSelectionResult Reject(AttackThrowStoneAiPreparedSelectionFailure failure) => new(false, failure, null, decisionRng);
        if (!selectionProfile.NormalScoring || !selectionProfile.CanonicalCoordinates || !selectionProfile.NoOtherScenarioOverrides ||
            !selectionProfile.ClearDirectTrajectories || !selectionProfile.NoDefendSupport)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Admission, AttackThrowStoneAiPreparedSelectionRejection.UnsupportedProfile));
        var scenario = AttackThrowStoneAiOutOfReachScenario.EvaluateFresh(state, request, throwStoneAbilityId, weapon, attackProfile,
            throwStoneProfile, scoreProfile, refreshProfile, preparationProfile,
            new(selectionProfile.NormalScoring, selectionProfile.CanonicalCoordinates, selectionProfile.NoOtherScenarioOverrides), team, decisionRng);
        if (!scenario.Result.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Scenario, AttackThrowStoneAiPreparedSelectionRejection.ScenarioRejected,
                ScenarioFailure: scenario.Result.Failure));
        var fresh = scenario.Result.Outcome!;
        var focus = fresh.Preparation.Common.FocusPosition;
        int Distance(TilePosition tile) => Math.Abs(tile.X - focus.X) + Math.Abs(tile.Y - focus.Y);
        int range = fresh.Preparation.ActorEntries.Single(e => e.Kind == AttackThrowStoneAiEntryKind.ThrowStone).Range;
        // ai_cast_aim_tiles_build intersects the non-arc range4 diamond BEFORE origin_next_take reads any key.
        // Only the final scenario's unwritten cells matter; out-of-range cells cannot cast either range4 Stone or range1 Attack.
        if (fresh.Cells.Any(c => !c.PriorityWritten && fresh.Scenario1.Contains(c.Tile) && Distance(c.Tile) <= range))
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Provenance, AttackThrowStoneAiPreparedSelectionRejection.UnwrittenCastPriority));
        var written = fresh.ScoreMask.ToHashSet();
        var scanOrigins = fresh.Scenario1.Where(written.Contains).ToImmutableArray();
        var scanScenario = new ThrowStoneAiScanScenario(fresh.ScoreMask, scanOrigins, fresh.BaselineDestination, fresh.ActFirstDestination);
        var profile = AttackThrowStoneAiPreparedSelection.CreateCompositionProfile(fresh.Preparation,
            attackProfile, throwStoneProfile, scoreProfile, selectionProfile);
        var scanRequest = new ThrowStoneAiScanRequest(request.ActorId, throwStoneAbilityId, request.TargetId);
        var invalid = AttackThrowStoneAiComposition.ValidateInputs(state, scanRequest, scanScenario, weapon, profile, decisionRng);
        if (invalid is not null)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Composition, AttackThrowStoneAiPreparedSelectionRejection.CompositionRejected,
                CompositionFailure: invalid.Failure));
        // The reach draw is already included in scenario RNG; the private prefix is reused, never reconstructed from public diagnostics.
        var composition = AttackThrowStoneAiComposition.ContinueFromMovement(state, scanRequest, scanScenario, weapon, profile,
            scenario.Prefix!, scenario.Result.RngState, decisionRng);
        if (!composition.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Composition, AttackThrowStoneAiPreparedSelectionRejection.CompositionRejected,
                CompositionFailure: composition.Failure));
        var selected = AttackThrowStoneAiSelection.SelectFresh(composition, request.ActorId, fresh.BaselineDestination, decisionRng);
        if (!selected.Accepted)
            return Reject(new(AttackThrowStoneAiPreparedSelectionStage.Selection, AttackThrowStoneAiPreparedSelectionRejection.SelectionRejected,
                SelectionFailure: selected.Failure));
        return new(true, null, new(fresh, scanOrigins, selected.Outcome!), selected.RngState);
    }
}
