using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Remaining original-context facts; masks, destinations and fresh baseline provenance are derived.</summary>
public sealed record SwordAiDerivedSelectionProfile(bool TargetActive, bool NormalScoring = false,
    bool CanonicalCoordinates = false, bool OrdinaryAttackTilePolicy = false, bool TargetSelectionMatchesBaseline = false,
    bool FreshAbilityBestReset = false, bool FreshEmptyRankedList = false);
public enum SwordAiDerivedSelectionStage { Admission, MovementScenario, Composition, Lottery }
public enum SwordAiDerivedSelectionRejection { UnsupportedProfile, AdmissionRejected, ScenarioRejected, CompositionRejected, SelectionRejected }
public sealed record SwordAiDerivedSelectionFailure(SwordAiDerivedSelectionRejection Reason, SwordAiDerivedSelectionStage Stage,
    SwordAiScanResult? AdmissionFailure = null, SwordAiMovementScenarioFailure? ScenarioFailure = null,
    SwordAiCompositionFailure? CompositionFailure = null, SwordAiSelectionFailure? SelectionFailure = null);
public sealed record SwordAiDerivedSelectionOutcome(SwordAiMovementScenarioOutcome MovementScenario, SwordAiSelectionOutcome Selection);
/// <summary>Failure returns original whole-query RNG with no partial scenario/composition/lottery/plan.</summary>
public sealed record SwordAiDerivedSelectionResult(bool Accepted, SwordAiDerivedSelectionFailure? Failure,
    SwordAiDerivedSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh in-reach focused-sword scenario through one terminal Attack pass and source lottery.</summary>
/// <remarks>
/// Reference 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-144/145. No supplied masks/destinations,
/// cached-result inputs, out-of-reach work, live commands/RNG, resolved facing or native activation.
/// </remarks>
public static class SwordAiDerivedSelection
{
    public static SwordAiDerivedSelectionResult Evaluate(AttackBattleSnapshot state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        ArgumentNullException.ThrowIfNull(derivedProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        SwordAiDerivedSelectionResult Reject(SwordAiDerivedSelectionFailure failure) => new(false, failure, null, decisionRng);
        if (!derivedProfile.NormalScoring || !derivedProfile.CanonicalCoordinates || !derivedProfile.FreshEmptyRankedList ||
            !SwordAiSelection.SupportsProfile(selectionProfile))
            return Reject(new(SwordAiDerivedSelectionRejection.UnsupportedProfile, SwordAiDerivedSelectionStage.Admission));
        // Mask provenance is structural in this entry point; all other scan facts remain explicit.
        var scanProfile = new SwordAiScanProfile(derivedProfile.TargetActive, derivedProfile.OrdinaryAttackTilePolicy,
            OriginalScenarioMask: true, derivedProfile.TargetSelectionMatchesBaseline, derivedProfile.FreshAbilityBestReset);
        var geometry = FlatOrdinaryMovement.Evaluate(state, comparisonProfile.ActorId, candidateProfile.Movement);
        var origins = geometry.Outcome?.AiOrigins ?? ImmutableArray<TilePosition>.Empty;
        // Pure provisional admission verifies even inactive/no-Attack weapon/context before movement draws.
        var invalid = SwordAiCandidateScan.ValidateInputs(state, new(origins, origins.IsEmpty ? null : origins[0]), weapon,
            candidateProfile, scoreProfile, comparisonProfile, scanProfile, decisionRng);
        if (invalid is not null)
            return Reject(new(SwordAiDerivedSelectionRejection.AdmissionRejected, SwordAiDerivedSelectionStage.Admission,
                AdmissionFailure: invalid));

        var movement = SwordAiMovementScenario.Evaluate(state, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, decisionRng);
        if (!movement.Accepted)
            return Reject(new(SwordAiDerivedSelectionRejection.ScenarioRejected, SwordAiDerivedSelectionStage.MovementScenario,
                ScenarioFailure: movement.Failure));
        var fresh = movement.Outcome!;
        state.TurnBattle.Battle.TryGetUnit(comparisonProfile.ActorId, out var actor);
        var attackOrigins = fresh.Scenario.MovementOrigins.Append(actor!.Position).Distinct()
            .OrderBy(p => p.Layer).ThenBy(p => p.Y).ThenBy(p => p.X);
        var attackScenario = new SwordAiScanScenario(attackOrigins, fresh.Scenario.AttackDestination);
        var composition = SwordAiComposition.ContinueFromMovement(state, fresh.Movement, fresh.BaselineIndex, attackScenario,
            weapon, candidateProfile, scoreProfile, comparisonProfile, scanProfile, movement.RngState, decisionRng);
        if (!composition.Accepted)
            return Reject(new(SwordAiDerivedSelectionRejection.CompositionRejected, SwordAiDerivedSelectionStage.Composition,
                CompositionFailure: composition.Failure));
        var selection = SwordAiSelection.SelectFresh(composition, comparisonProfile.ActorId, fresh.Scenario.BaselineDestination, decisionRng);
        if (!selection.Accepted)
            return Reject(new(SwordAiDerivedSelectionRejection.SelectionRejected, SwordAiDerivedSelectionStage.Lottery,
                SelectionFailure: selection.Failure));
        return new(true, null, new(fresh, selection.Outcome!), selection.RngState);
    }
}
