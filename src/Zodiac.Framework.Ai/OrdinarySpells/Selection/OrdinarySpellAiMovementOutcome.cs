using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Move-only diagnostics; scenario masks may overlap and an unwritten priority remains unknown.</summary>
public sealed record OrdinarySpellAiMovementOutcome(OrdinarySpellAiConsidered Ability, AiTargetRoster Targets,
    AiRosterNearestOutcome Focus, OrdinarySpellAiClockOutcome RefreshClock, FlatMovementOutcome Geometry,
    ImmutableArray<TilePosition> ScoreMask, ImmutableArray<TilePosition> Scenario1, ImmutableArray<TilePosition> Scenario2,
    ImmutableArray<OrdinarySpellAiScoredMovement> Movement, ImmutableArray<AiMovementCellProvenance> Cells,
    TilePosition BaselineDestination, TilePosition ActFirstDestination, OrdinarySpellAiMovementRank BaselineRank,
    int TargetDistance, int TargetReach, int EffectiveReach, AiReachAdjustment? ReachAdjustment,
    int? BaselineDistanceLimit, bool FilterUsedReachableFallback, bool DestinationUsedDistanceFallback, bool OutOfReach);
