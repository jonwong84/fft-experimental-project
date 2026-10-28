using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Facts absent from the live snapshot; all require explicit source-context certification.</summary>
public sealed record SwordAiSelectionProfile(bool TerminalAttackEntry = false, bool NoDefendSupport = false,
    bool FreshOrdinarySelectionDefaults = false);

public enum SwordAiSelectionStage { Admission, Composition, Lottery }
public enum SwordAiSelectionRejection { UnsupportedProfile, CompositionRejected, RandomDrawCountExhausted }
public enum SwordAiSelectionReason { FirstRoll, AllRollsFailedUseFirst, MovementFallback }
public enum SwordAiSelectedPlanKind { Attack, MoveOnly }
/// <summary>Semantic deferred hint, not the original packed direction ordinal or a resolved facing.</summary>
public enum SwordAiWaitFacingHint { ChooseAtWait }

public sealed record SwordAiSelectionFailure(SwordAiSelectionRejection Reason, SwordAiSelectionStage Stage,
    int? LotteryIndex = null, SwordAiCompositionFailure? CompositionFailure = null);
public sealed record SwordAiLotteryRoll(int Slot, uint Raw, int Modulo, int HitPercent, bool Passed);

/// <summary>Hypothetical plan only. No command receipt, decided marker, projected damage application or live choice consumption.</summary>
public sealed record SwordAiSelectedPlan(SwordAiSelectedPlanKind Kind, BattleUnitId ActorId, TilePosition Destination,
    SwordAiScannedCandidate? Attack)
{
    public SwordAiWaitFacingHint WaitFacingHint => SwordAiWaitFacingHint.ChooseAtWait;
    public bool ActBeforeMove => Attack?.Candidate.Order == SwordAiCandidateOrder.ActThenMove;
}

public sealed record SwordAiSelectionOutcome(SwordAiCompositionOutcome Composition, FftRandomState LotteryEntryRng,
    ImmutableArray<SwordAiLotteryRoll> Lottery, int? SelectedRankedIndex, SwordAiSelectionReason Reason, SwordAiSelectedPlan Plan);
/// <summary>Failures return the incoming whole-query RNG and no partial composition, lottery or plan.</summary>
public sealed record SwordAiSelectionResult(bool Accepted, SwordAiSelectionFailure? Failure,
    SwordAiSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh ordinary Attack/MoveOnly selection after one privately owned movement/Attack composition.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-139/140.
/// Requires terminal Attack, no Defend support and fresh ordinary defaults. No cached-result input,
/// general ability search, command/facing execution, live RNG commitment or native activation.
/// </remarks>
public static class SwordAiSelection
{
    public static SwordAiSelectionResult Evaluate(AttackBattleSnapshot state, SwordAiCompositionScenario scenario,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiMovementBaselineProfile baselineProfile,
        SwordAiScanProfile scanProfile, SwordAiCompositionProfile compositionProfile,
        SwordAiSelectionProfile selectionProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(baselineProfile);
        ArgumentNullException.ThrowIfNull(scanProfile);
        ArgumentNullException.ThrowIfNull(compositionProfile);
        ArgumentNullException.ThrowIfNull(selectionProfile);
        SwordAiSelectionResult Reject(SwordAiSelectionFailure failure) => new(false, failure, null, decisionRng);
        if (!SupportsProfile(selectionProfile))
            return Reject(new(SwordAiSelectionRejection.UnsupportedProfile, SwordAiSelectionStage.Admission));

        // Sole fresh composition call: public cached ranked results are never accepted as input.
        var composition = SwordAiComposition.Evaluate(state, scenario, weapon, candidateProfile, scoreProfile,
            comparisonProfile, baselineProfile, scanProfile, compositionProfile, decisionRng);
        if (!composition.Accepted)
            return Reject(new(SwordAiSelectionRejection.CompositionRejected, SwordAiSelectionStage.Composition,
                CompositionFailure: composition.Failure));

        return SelectFresh(composition, comparisonProfile.ActorId, scenario.BaselineDestination, decisionRng);
    }

    internal static bool SupportsProfile(SwordAiSelectionProfile profile) =>
        profile.TerminalAttackEntry && profile.NoDefendSupport && profile.FreshOrdinarySelectionDefaults;

    // Only fresh privately owned composition reaches this tail; no public cached-result entry.
    internal static SwordAiSelectionResult SelectFresh(SwordAiCompositionResult composition,
        BattleUnitId actorId, TilePosition baselineDestination, FftRandomState queryRng)
    {
        SwordAiSelectionResult Reject(SwordAiSelectionFailure failure) => new(false, failure, null, queryRng);
        var fresh = composition.Outcome!;
        var rng = composition.RngState;
        var random = new FftBiosRandom();
        var rolls = ImmutableArray.CreateBuilder<SwordAiLotteryRoll>();
        SwordAiSelectionResult Complete(SwordAiScannedCandidate? attack, int? index, SwordAiSelectionReason reason)
        {
            var plan = attack is null
                ? new SwordAiSelectedPlan(SwordAiSelectedPlanKind.MoveOnly, actorId, baselineDestination, null)
                : new SwordAiSelectedPlan(SwordAiSelectedPlanKind.Attack, attack.Candidate.ActorId, attack.Candidate.Destination, attack);
            return new(true, null, new(fresh, composition.RngState, rolls.ToImmutable(), index, reason, plan), rng);
        }

        // The composition owns eight slots, with zero/one real action at slot 0 in this admitted domain.
        for (int i = 0; i < fresh.RankedActions.Length; i++)
        {
            var entry = fresh.RankedActions[i];
            int chance = entry?.Evaluation.Score!.HitPercent ?? 0;
            uint raw;
            try { var draw = random.Next(rng); rng = draw.State; raw = draw.Value; }
            catch (OverflowException)
            { return Reject(new(SwordAiSelectionRejection.RandomDrawCountExhausted, SwordAiSelectionStage.Lottery, i)); }
            int modulo = (int)(raw % 100);
            bool passed = modulo < chance;
            rolls.Add(new(i, raw, modulo, chance, passed)); // Zero-hit slots consume real draws too.
            if (passed) return Complete(entry!, i, SwordAiSelectionReason.FirstRoll);
        }
        // A positive-hit first entry survives all failed rolls; movement fallback is only for an empty list.
        return fresh.RankedActions[0] is { } first
            ? Complete(first, 0, SwordAiSelectionReason.AllRollsFailedUseFirst)
            : Complete(null, null, SwordAiSelectionReason.MovementFallback);
    }
}
