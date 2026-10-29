using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Terminal admitted pair, no other retained/searchable entries, no Defend and fresh defaults are source assertions.</summary>
public sealed record AttackThrowStoneAiSelectionProfile(bool TerminalThrowStoneEntry = false,
    bool NoDefendSupport = false, bool FreshOrdinarySelectionDefaults = false);
public enum AttackThrowStoneAiSelectionStage { Admission, Composition, Lottery }
public enum AttackThrowStoneAiSelectionRejection { UnsupportedProfile, CompositionRejected, RandomDrawCountExhausted }
public enum AttackThrowStoneAiPlanKind { Attack, ThrowStone, MoveOnly }
public sealed record AttackThrowStoneAiSelectedPlan(BattleUnitId ActorId, TilePosition Destination, AttackThrowStoneAiAction? Action)
{
    public AttackThrowStoneAiPlanKind Kind => Action switch
    { AttackThrowStoneAiAttack => AttackThrowStoneAiPlanKind.Attack, AttackThrowStoneAiStone => AttackThrowStoneAiPlanKind.ThrowStone,
        _ => AttackThrowStoneAiPlanKind.MoveOnly };
    public bool ActBeforeMove => Action?.ActBeforeMove ?? false;
    public bool ChooseFacingAtWait => true;
}
public sealed record AttackThrowStoneAiSelectionFailure(AttackThrowStoneAiSelectionStage Stage, AttackThrowStoneAiSelectionRejection Reason,
    int? LotteryIndex = null, AttackThrowStoneAiFailure? CompositionFailure = null);
public sealed record AttackThrowStoneAiSelectionOutcome(AttackThrowStoneAiCompositionOutcome Composition, FftRandomState LotteryEntryRng,
    ImmutableArray<AiLotteryRoll> Lottery, int? SelectedRankedIndex, AiLotteryReason Reason, AttackThrowStoneAiSelectedPlan Plan);
/// <summary>Hypothetical result only. Failure publishes no partial ranks/rolls/plan and restores whole-query RNG.</summary>
public sealed record AttackThrowStoneAiSelectionResult(bool Accepted, AttackThrowStoneAiSelectionFailure? Failure,
    AttackThrowStoneAiSelectionOutcome? Outcome, FftRandomState RngState);

/// <summary>Own fresh pair ranking, then one terminal source lottery and typed hypothetical plan.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-192/194.
/// No cached-rank input, executable command, live choice/RNG adoption, facing resolution or host activation.</remarks>
public static class AttackThrowStoneAiSelection
{
    public static AttackThrowStoneAiSelectionResult Evaluate(AttackBattleSnapshot state, ThrowStoneAiScanRequest request,
        ThrowStoneAiScanScenario scenario, BoundedSwordWeapon? weapon, AttackThrowStoneAiProfile profile,
        AttackThrowStoneAiSelectionProfile selectionProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(selectionProfile);
        if (!selectionProfile.TerminalThrowStoneEntry || !selectionProfile.NoDefendSupport || !selectionProfile.FreshOrdinarySelectionDefaults)
            return new(false, new(AttackThrowStoneAiSelectionStage.Admission, AttackThrowStoneAiSelectionRejection.UnsupportedProfile), null, decisionRng);
        var composition = AttackThrowStoneAiComposition.Evaluate(state, request, scenario, weapon, profile, decisionRng);
        if (!composition.Accepted)
            return new(false, new(AttackThrowStoneAiSelectionStage.Composition, AttackThrowStoneAiSelectionRejection.CompositionRejected,
                CompositionFailure: composition.Failure), null, decisionRng);
        var fresh = composition.Outcome!;
        var lottery = AiRankedLottery.Evaluate(fresh.RankedActions.Select(a => a?.Score.HitPercent ?? (byte)0).ToImmutableArray(), composition.RngState);
        if (!lottery.Accepted)
            return new(false, new(AttackThrowStoneAiSelectionStage.Lottery, AttackThrowStoneAiSelectionRejection.RandomDrawCountExhausted,
                lottery.FailedIndex), null, decisionRng);
        var selected = lottery.Outcome!;
        var action = selected.SelectedIndex is { } index ? fresh.RankedActions[index] : null;
        var plan = new AttackThrowStoneAiSelectedPlan(request.ActorId, action?.Destination ?? scenario.BaselineDestination, action);
        return new(true, null, new(fresh, composition.RngState, selected.Rolls, selected.SelectedIndex, selected.Reason, plan), lottery.RngState);
    }
}
