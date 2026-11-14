using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Actual effect bindings plus remaining source policy/default facts; only entered actions require their bindings.</summary>
public sealed record AiAbilityEvaluationProfile(BoundedSwordWeapon? Weapon = null, RosterAccumulateAiProfile? Accumulate = null,
    TargetedAbilityBattleProfile? ThrowStone = null, RosterAiProjectileHeights? Heights = null,
    bool OrdinaryAttackTilePolicy = false, bool OriginalInstantDirectEnemyPolicy = false,
    bool NoDefendSupport = false, bool FreshOrdinarySelectionDefaults = false,
    DashAbilityBattleProfile? Dash = null, bool OriginalInstantEnemyDashPolicy = false);
public sealed record AiRankedAbility(int OccurrenceIndex, AiHumanAbilityKind Kind, TilePosition Destination,
    bool ActBeforeMove, BattleUnitId? TargetId, AssetId? AbilityId, AiCandidateScore Score, byte Proximity);
public sealed record AiAbilityEvaluation(AiPreparedAbility Entry, RandomState EntryRng, RandomState ExitRng,
    RosterSwordAiAttackOutcome? Attack = null, RosterAccumulateAiEntryOutcome? Accumulate = null,
    RosterThrowStoneAiEntryOutcome? ThrowStone = null, AiRankedAbility? Best = null, int? InsertionIndex = null, RosterDashAiEntryOutcome? Dash = null);
public enum AiDecisionStage { Admission, Prefix, Entry, Lottery }
public sealed record AiDecisionFailure(AiDecisionStage Stage, RosterAiGeneralFailure? Prefix = null,
    int? OccurrenceIndex = null, RosterSwordAiAttackFailure? Attack = null, RosterAccumulateAiEntryFailure? Accumulate = null,
    RosterThrowStoneAiEntryFailure? ThrowStone = null, int? LotteryIndex = null, RosterDashAiEntryFailure? Dash = null);
public sealed record AiActionPlan(BattleUnitId ActorId, TilePosition Destination, AiRankedAbility? Action)
{
    public bool ActBeforeMove => Action?.ActBeforeMove ?? false;
    public bool ChooseFacingAtWait => true;
}
public sealed record AiDecision(RosterAiGeneralScenarioOutcome Scenarios,
    ImmutableArray<AiAbilityEvaluation> Entries, ImmutableArray<AiRankedAbility?> RankedActions,
    RandomState LotteryEntryRng, ImmutableArray<AiLotteryRoll> Lottery, int? SelectedRankedIndex,
    AiLotteryReason Reason, AiActionPlan Plan);
public sealed record AiDecisionResult(bool Accepted, AiDecisionFailure? Failure,
    AiDecision? Outcome, RandomState RngState, AiPriorityWorkspace Workspace);
