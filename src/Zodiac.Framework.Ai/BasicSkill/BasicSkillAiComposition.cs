using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Three source entries and a supplied known-score scenario; no generic Basic Skill catalog.</summary>
public sealed record BasicSkillAiProfile(AccumulateAiPreparationConfiguration Preparation,
    SwordAiScanProfile AttackScan, ThrowStoneAiScanProfile ThrowStoneScan, ThrowStoneAiBaselineProfile Baseline,
    bool SourceGeneralEntry = false, bool SourceAttackAccumulateThrowStoneOrder = false, bool FreshEmptyRankedList = false);
public enum BasicSkillAiActionKind { Attack, Accumulate, ThrowStone }
/// <summary>Hypothetical typed payloads only. No public operation consumes them as command authority.</summary>
public abstract record BasicSkillAiAction
{
    private protected BasicSkillAiAction() { }
    public abstract BasicSkillAiActionKind Kind { get; }
    public abstract BattleUnitId ActorId { get; }
    public abstract TilePosition Destination { get; }
    public abstract bool ActBeforeMove { get; }
    public abstract AiCandidateScore Score { get; }
    public abstract byte Proximity { get; }
}
public sealed record BasicSkillAiAttack(SwordAiScannedCandidate Value) : BasicSkillAiAction
{
    public override BasicSkillAiActionKind Kind => BasicSkillAiActionKind.Attack;
    public override BattleUnitId ActorId => Value.Candidate.ActorId;
    public override TilePosition Destination => Value.Candidate.Destination;
    public override bool ActBeforeMove => Value.Candidate.Order == SwordAiCandidateOrder.ActThenMove;
    public override AiCandidateScore Score => Value.Evaluation.Score!;
    public override byte Proximity => Value.Proximity;
}
public sealed record BasicSkillAiAccumulate(AccumulateAiScoringResult Value, byte FoeProximity) : BasicSkillAiAction
{
    public override BasicSkillAiActionKind Kind => BasicSkillAiActionKind.Accumulate;
    public override BattleUnitId ActorId => Value.Projection!.Outcome!.Candidate.ActorId;
    public override TilePosition Destination => Value.Projection!.Outcome!.Candidate.Destination;
    public override bool ActBeforeMove => Value.Projection!.Outcome!.Candidate.ActBeforeMove;
    public override AiCandidateScore Score => Value.Score!;
    public override byte Proximity => FoeProximity;
}
public sealed record BasicSkillAiStone(ThrowStoneAiScannedCandidate Value, byte FoeProximity) : BasicSkillAiAction
{
    public override BasicSkillAiActionKind Kind => BasicSkillAiActionKind.ThrowStone;
    public override BattleUnitId ActorId => Value.Candidate.ActorId;
    public override TilePosition Destination => Value.Candidate.Destination;
    public override bool ActBeforeMove => Value.Candidate.ActBeforeMove;
    public override AiCandidateScore Score => Value.Evaluation.Score!;
    public override byte Proximity => FoeProximity;
}
public enum BasicSkillAiStage { Admission, Preparation, Movement, Attack, Accumulate, ThrowStone }
public sealed record BasicSkillAiFailure(BasicSkillAiStage Stage, int? Index = null,
    AccumulateAiPreparationFailure? Preparation = null, SwordAiScanRejection Attack = SwordAiScanRejection.None,
    AccumulateAiMapEntryFailure? Accumulate = null, ThrowStoneAiScanFailure? ThrowStone = null,
    SwordAiMovementBaselineRejection Movement = SwordAiMovementBaselineRejection.None,
    AiScoringRejection Scoring = AiScoringRejection.None, SwordAiCandidateRejection AttackProjection = SwordAiCandidateRejection.None);
public sealed record BasicSkillAiCompositionOutcome(AccumulateAiPreparationOutcome Preparation,
    ImmutableArray<AiScoredMovementTile> Movement, int BaselineIndex, SwordAiScanOutcome Attack,
    AccumulateAiEntryOutcome Accumulate, ThrowStoneAiScanOutcome ThrowStone, ImmutableArray<BasicSkillAiAction?> RankedActions,
    int? AttackInsertionIndex, int? AccumulateInsertionIndex, int? ThrowStoneInsertionIndex)
{
    public AiMovementBaselineOutcome Baseline => Movement[BaselineIndex].Outcome;
}
/// <summary>Failure returns no partial preparation/prefix/ranks and the incoming whole-query RNG.</summary>
public sealed record BasicSkillAiCompositionResult(bool Accepted, BasicSkillAiFailure? Failure,
    BasicSkillAiCompositionOutcome? Outcome, RandomState RngState);

/// <summary>Fresh closed-list preparation and one shared supplied-scenario prefix, followed by three source-order entries.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-233/235. No derived scenarios, lottery or command authority.</remarks>
public static class BasicSkillAiComposition
{
    public static BasicSkillAiCompositionResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile, RandomState decisionRng)
    {
        ValidateArguments(state, request, scenario, profile);
        BasicSkillAiCompositionResult Reject(BasicSkillAiFailure failure) => new(false, failure, null, decisionRng);
        if (!profile.SourceGeneralEntry || !profile.SourceAttackAccumulateThrowStoneOrder || !profile.FreshEmptyRankedList ||
            !profile.AttackScan.TargetActive || !profile.ThrowStoneScan.TargetActive)
            return Reject(new(BasicSkillAiStage.Admission));
        var p = profile.Preparation;
        var preparation = AccumulateAiPreparation.Evaluate(state, request, p, decisionRng);
        if (!preparation.Accepted) return Reject(new(BasicSkillAiStage.Preparation, Preparation: preparation.Failure));
        var turn = request.Turn;
        var comparison = new SwordAiComparisonProfile(turn.ActorId, turn.TargetId, profile.ThrowStoneScan.FreshDecisionBaseline);
        var attackScenario = new SwordAiScanScenario(scenario.ScenarioOrigins, scenario.ScenarioOrigins.IsEmpty ? null : scenario.ActFirstDestination);
        var attackAdmission = SwordAiCandidateScan.ValidateInputs(state, attackScenario, request.Weapon, p.Attack, p.Scoring,
            comparison, profile.AttackScan, decisionRng);
        if (attackAdmission is not null) return Reject(AttackFailure(BasicSkillAiStage.Admission, attackAdmission));
        var stoneRequest = new ThrowStoneAiScanRequest(turn.ActorId, request.ThrowStoneAbilityId, turn.TargetId);
        var stoneAdmission = ThrowStoneAiCandidateScan.ValidateInputs(state, stoneRequest, scenario, p.ThrowStone, p.Scoring,
            profile.Baseline, profile.ThrowStoneScan, decisionRng);
        if (stoneAdmission is not null) return Reject(new(BasicSkillAiStage.Admission, ThrowStone: stoneAdmission.Failure));
        var prefix = AiMovementPrefix.Evaluate(state, turn.ActorId, turn.TargetId, scenario.ScoredOrigins,
            scenario.BaselineDestination, p.Accumulate.Units, preparation.RngState);
        if (prefix.Rejection != AiSimulationClockRejection.None)
            return Reject(new(BasicSkillAiStage.Movement, prefix.FailedIndex, Movement: prefix.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                ? SwordAiMovementBaselineRejection.RandomDrawCountExhausted : SwordAiMovementBaselineRejection.TickCountExhausted));
        var fresh = prefix.Outcome!;
        var baseline = fresh.Movement[fresh.BaselineIndex].Outcome.Score;
        var ranked = new BasicSkillAiAction?[8]; // Original ranked capacity; not a managed catalog limit.
        int? Insert(BasicSkillAiAction best) => AiRankedInsertion.Insert(best, baseline, ranked, a => a.Score, a => a.Proximity);
        var attack = SwordAiCandidateScan.EvaluateFresh(state, attackScenario, request.Weapon, p.Attack, p.Scoring,
            comparison, profile.AttackScan, fresh.Proximity, prefix.RngState);
        if (attack.Disposition == SwordAiScanDisposition.Rejected) return Reject(AttackFailure(BasicSkillAiStage.Attack, attack));
        int? attackIndex = attack.Outcome!.RetainedCandidateIndex is { } a ? Insert(new BasicSkillAiAttack(attack.Outcome.Candidates[a])) : null;
        // Actual state never changes. Only RNG and the private ranked slots cross entry boundaries.
        var self = AccumulateAiEntryEvaluation.Evaluate(state, turn.ActorId, turn.TargetId, request.AbilityId,
            scenario.ActFirstDestination, p.Accumulate, p.Scoring, preparation.Outcome!.Common.TargetActive, attack.RngState);
        if (self.Failure is not null) return Reject(new(BasicSkillAiStage.Accumulate, Accumulate: self.Failure));
        int? selfIndex = self.Outcome!.BestCandidateIndex is { } s
            ? Insert(new BasicSkillAiAccumulate(self.Outcome.Candidates[s], fresh.Proximity.Destinations[scenario.ActFirstDestination])) : null;
        var stone = ThrowStoneAiCandidateScan.ContinueFromMovement(state, stoneRequest, scenario, p.ThrowStone, p.Scoring,
            profile.ThrowStoneScan, fresh, self.RngState, decisionRng);
        if (stone.Disposition == ThrowStoneAiScanDisposition.Rejected) return Reject(new(BasicSkillAiStage.ThrowStone, ThrowStone: stone.Failure));
        int? stoneIndex = stone.Outcome!.Candidate is { Evaluation.Score: not null } c
            ? Insert(new BasicSkillAiStone(c, fresh.Proximity.Destinations[c.Candidate.Destination])) : null;
        return new(true, null, new(preparation.Outcome, fresh.Movement, fresh.BaselineIndex, attack.Outcome, self.Outcome,
            stone.Outcome, ranked.ToImmutableArray(), attackIndex, selfIndex, stoneIndex), stone.RngState);
    }

    internal static void ValidateArguments(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        ThrowStoneAiScanScenario scenario, BasicSkillAiProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(scenario);
        AccumulateAiPreparation.ValidateArguments(state, request, profile.Preparation);
        ArgumentNullException.ThrowIfNull(profile.AttackScan); ArgumentNullException.ThrowIfNull(profile.ThrowStoneScan);
        ArgumentNullException.ThrowIfNull(profile.Baseline);
    }

    private static BasicSkillAiFailure AttackFailure(BasicSkillAiStage stage, SwordAiScanResult result) =>
        new(stage, result.FailedCandidateIndex, Attack: result.Rejection, Scoring: result.ScoringRejection, AttackProjection: result.ProjectionRejection);
}
