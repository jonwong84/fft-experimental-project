using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Remaining original-context facts; masks, destinations and fresh baseline provenance are derived.</summary>
public sealed record SwordAiDerivedSelectionProfile(bool TargetActive, bool NormalScoring = false,
    bool CanonicalCoordinates = false, bool OrdinaryAttackTilePolicy = false, bool TargetSelectionMatchesBaseline = false,
    bool FreshAbilityBestReset = false, bool FreshEmptyRankedList = false, bool FirstAttackEntry = false);
public enum SwordAiDerivedSelectionStage { Admission, MovementScenario, Composition, Lottery }
public enum SwordAiDerivedSelectionRejection { UnsupportedProfile, AdmissionRejected, ScenarioRejected, CompositionRejected, SelectionRejected }
public sealed record SwordAiDerivedSelectionFailure(SwordAiDerivedSelectionRejection Reason, SwordAiDerivedSelectionStage Stage,
    SwordAiScanResult? AdmissionFailure = null, SwordAiMovementScenarioFailure? ScenarioFailure = null,
    SwordAiCompositionFailure? CompositionFailure = null, SwordAiSelectionFailure? SelectionFailure = null);
public sealed record SwordAiDerivedSelectionOutcome(SwordAiMovementScenarioOutcome MovementScenario, SwordAiSelectionOutcome Selection);
/// <summary>Failure returns original whole-query RNG with no partial scenario/composition/lottery/plan.</summary>
public sealed record SwordAiDerivedSelectionResult(bool Accepted, SwordAiDerivedSelectionFailure? Failure,
    SwordAiDerivedSelectionOutcome? Outcome, FftRandomState RngState);

public sealed record SwordAiOutOfReachSelectionOutcome(SwordAiOutOfReachScenarioOutcome MovementScenario,
    SwordAiSelectionOutcome Selection);
public sealed record SwordAiOutOfReachSelectionResult(bool Accepted, SwordAiDerivedSelectionFailure? Failure,
    SwordAiOutOfReachSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh explicit focused-sword movement branch through one first/terminal Attack pass and source lottery.</summary>
/// <remarks>
/// Reference 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-144/145/148/149. No supplied masks/destinations,
/// cached-result inputs, live commands/RNG, resolved facing or native activation.
/// </remarks>
public static class SwordAiDerivedSelection
{
    public static SwordAiDerivedSelectionResult Evaluate(AttackBattleSnapshot state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, FftRandomState decisionRng)
        => EvaluateCore(state, weapon, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile,
            derivedProfile, selectionProfile, decisionRng, null).Result;

    /// <summary>Out-of-reach scenario plus one privately owned scan/lottery, retaining post-score team adjustment.</summary>
    public static SwordAiOutOfReachSelectionResult EvaluateOutOfReach(AttackBattleSnapshot state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, SwordAiScenarioTeam team, FftRandomState decisionRng)
    {
        var core = EvaluateCore(state, weapon, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile,
            derivedProfile, selectionProfile, decisionRng, team);
        var result = core.Result;
        return new(result.Accepted, result.Failure, result.Accepted
            ? new(core.OutOfReach!, result.Outcome!.Selection) : null, result.RngState);
    }

    private sealed record CoreResult(SwordAiDerivedSelectionResult Result, SwordAiOutOfReachScenarioOutcome? OutOfReach = null);

    private static CoreResult EvaluateCore(AttackBattleSnapshot state, BoundedSwordWeapon? weapon,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, SwordAiDerivedSelectionProfile derivedProfile,
        SwordAiSelectionProfile selectionProfile, FftRandomState decisionRng, SwordAiScenarioTeam? team)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        ArgumentNullException.ThrowIfNull(derivedProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        CoreResult Reject(SwordAiDerivedSelectionFailure failure) => new(new(false, failure, null, decisionRng));
        if (!derivedProfile.NormalScoring || !derivedProfile.CanonicalCoordinates || !derivedProfile.FreshEmptyRankedList ||
            !derivedProfile.FirstAttackEntry || !SwordAiSelection.SupportsProfile(selectionProfile))
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

        SwordAiMovementScenarioOutcome fresh;
        FftRandomState movementRng;
        SwordAiOutOfReachScenarioOutcome? outOfReach = null;
        if (team is { } sourceTeam)
        {
            var movement = SwordAiMovementScenario.EvaluateOutOfReach(state, candidateProfile, scoreProfile, comparisonProfile,
                scenarioProfile, sourceTeam, decisionRng);
            if (!movement.Accepted)
                return Reject(new(SwordAiDerivedSelectionRejection.ScenarioRejected, SwordAiDerivedSelectionStage.MovementScenario,
                    ScenarioFailure: movement.Failure));
            outOfReach = movement.Outcome!;
            fresh = outOfReach.MovementScenario;
            movementRng = movement.RngState; // After the actual team adjustment, not the last scored cell.
        }
        else
        {
            var movement = SwordAiMovementScenario.Evaluate(state, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, decisionRng);
            if (!movement.Accepted)
                return Reject(new(SwordAiDerivedSelectionRejection.ScenarioRejected, SwordAiDerivedSelectionStage.MovementScenario,
                    ScenarioFailure: movement.Failure));
            fresh = movement.Outcome!;
            movementRng = movement.RngState;
        }
        state.TurnBattle.Battle.TryGetUnit(comparisonProfile.ActorId, out var actor);
        var attackOrigins = fresh.Scenario.MovementOrigins.Append(actor!.Position).Distinct()
            .OrderBy(p => p.Layer).ThenBy(p => p.Y).ThenBy(p => p.X);
        var attackScenario = new SwordAiScanScenario(attackOrigins, fresh.Scenario.AttackDestination);
        var composition = SwordAiComposition.ContinueFromMovement(state, fresh.Movement, fresh.BaselineIndex, attackScenario,
            weapon, candidateProfile, scoreProfile, comparisonProfile, scanProfile, movementRng, decisionRng);
        if (!composition.Accepted)
            return Reject(new(SwordAiDerivedSelectionRejection.CompositionRejected, SwordAiDerivedSelectionStage.Composition,
                CompositionFailure: composition.Failure));
        var selection = SwordAiSelection.SelectFresh(composition, comparisonProfile.ActorId, fresh.Scenario.BaselineDestination, decisionRng);
        if (!selection.Accepted)
            return Reject(new(SwordAiDerivedSelectionRejection.SelectionRejected, SwordAiDerivedSelectionStage.Lottery,
                SelectionFailure: selection.Failure));
        return new(new(true, null, new(fresh, selection.Outcome!), selection.RngState), outOfReach);
    }
}
