using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented source facts; none are inferred from IDs or a supplied destination.</summary>
/// <remarks>Ordinary policy excludes stay-near, reflected/ranged/crystal overrides and special search modes.</remarks>
public sealed record SwordAiMovementScenarioProfile(bool FocusedOpponent = false,
    bool OrdinarySwordReach = false, bool OrdinaryScenarioPolicy = false);

public enum SwordAiMovementScenarioStage { Admission, Movement, ReachAdjustment }
public enum SwordAiMovementScenarioRejection { UnsupportedProfile, AdmissionRejected, OutOfReach, EvaluationRejected, WrongBranch, RandomDrawCountExhausted }
public sealed record SwordAiMovementScenarioFailure(SwordAiMovementScenarioRejection Reason,
    SwordAiMovementScenarioStage Stage, int? Index = null,
    SwordAiMovementBaselineRejection MovementReason = SwordAiMovementBaselineRejection.None);
public sealed record SwordAiMovementTileOrdering(TilePosition Tile, byte TargetDistance, byte Proximity, int PackedScore);
public sealed record SwordAiMovementScenarioOutcome(SwordAiCompositionScenario Scenario,
    ImmutableArray<SwordAiScoredMovementTile> Movement, int BaselineIndex,
    ImmutableArray<SwordAiMovementTileOrdering> Ordering, int TargetDistance, int TargetReach, bool BaselineUsedDistanceFallback);
/// <summary>Failure publishes no partial scenario/evaluations and returns the incoming decision RNG.</summary>
public sealed record SwordAiMovementScenarioResult(bool Accepted, SwordAiMovementScenarioFailure? Failure,
    SwordAiMovementScenarioOutcome? Outcome, FftRandomState RngState);

/// <summary>Original masked team class, independent of control, actor identity or opponent relation.</summary>
public enum SwordAiScenarioTeam { Unknown, Blue, NonBlue }
public sealed record SwordAiReachAdjustment(uint Raw, int Modulo, int Before, int After)
{
    public bool Halved => Modulo == 0;
}
public sealed record SwordAiOutOfReachScenarioOutcome(SwordAiMovementScenarioOutcome MovementScenario,
    SwordAiScenarioTeam Team, int EffectiveReach, SwordAiReachAdjustment? ReachAdjustment, bool AttackUsedDistanceFallback);
public sealed record SwordAiOutOfReachScenarioResult(bool Accepted, SwordAiMovementScenarioFailure? Failure,
    SwordAiOutOfReachScenarioOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh, pure derivation of explicit in-reach and out-of-reach focused ordinary-sword movement branches.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-141/143/146/147. No Attack, lottery or command execution.</remarks>
public static class SwordAiMovementScenario
{
    public static SwordAiMovementScenarioResult Evaluate(AttackBattleSnapshot state,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiMovementScenarioProfile scenarioProfile, FftRandomState decisionRng)
        => EvaluateCore(state, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, decisionRng, null).Result;

    /// <summary>Explicit out-of-reach branch; Blue skips, NonBlue draws once after movement scoring.</summary>
    public static SwordAiOutOfReachScenarioResult EvaluateOutOfReach(AttackBattleSnapshot state,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiMovementScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng)
        => EvaluateReachCore(state, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, team, decisionRng, false);

    // Only the owned fresh post-ACT preparation wrapper supplies these derived entry facts.
    internal static SwordAiMovementScenarioResult EvaluateAfterAct(AttackBattleSnapshot state,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiMovementScenarioProfile scenarioProfile, FftRandomState decisionRng)
        => EvaluateCore(state, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, decisionRng, null, alreadyActed: true).Result;

    internal static SwordAiOutOfReachScenarioResult EvaluateOutOfReachAfterAct(AttackBattleSnapshot state,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiMovementScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng)
        => EvaluateReachCore(state, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, team, decisionRng, true);

    private static SwordAiOutOfReachScenarioResult EvaluateReachCore(AttackBattleSnapshot state,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiMovementScenarioProfile scenarioProfile,
        SwordAiScenarioTeam team, FftRandomState decisionRng, bool alreadyActed)
    {
        var core = EvaluateCore(state, candidateProfile, scoreProfile, comparisonProfile, scenarioProfile, decisionRng, team, alreadyActed);
        var result = core.Result;
        return new(result.Accepted, result.Failure, result.Accepted
            ? new(result.Outcome!, team, core.EffectiveReach, core.Adjustment, core.AttackFallback) : null, result.RngState);
    }

    private sealed record CoreResult(SwordAiMovementScenarioResult Result, int EffectiveReach = 0,
        SwordAiReachAdjustment? Adjustment = null, bool AttackFallback = false);

    private static CoreResult EvaluateCore(AttackBattleSnapshot state, SwordAiCandidateProfile candidateProfile,
        AiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile,
        SwordAiMovementScenarioProfile scenarioProfile, FftRandomState decisionRng, SwordAiScenarioTeam? team, bool alreadyActed = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(scenarioProfile);
        CoreResult Reject(SwordAiMovementScenarioRejection reason,
            SwordAiMovementScenarioStage stage = SwordAiMovementScenarioStage.Admission, int? index = null,
            SwordAiMovementBaselineRejection movementReason = SwordAiMovementBaselineRejection.None) =>
            new(new(false, new(reason, stage, index, movementReason), null, decisionRng));
        bool outOfReach = team is not null;
        if (outOfReach && team is not (SwordAiScenarioTeam.Blue or SwordAiScenarioTeam.NonBlue))
            return Reject(SwordAiMovementScenarioRejection.UnsupportedProfile);
        if (!scenarioProfile.FocusedOpponent || !scenarioProfile.OrdinarySwordReach || !scenarioProfile.OrdinaryScenarioPolicy ||
            !comparisonProfile.FreshDecisionBaseline)
            return Reject(SwordAiMovementScenarioRejection.UnsupportedProfile);
        var battle = state.TurnBattle.Battle;
        if (!battle.TryGetUnit(comparisonProfile.ActorId, out var actor))
            return Reject(SwordAiMovementScenarioRejection.AdmissionRejected, movementReason: SwordAiMovementBaselineRejection.UnknownActor);
        var invalid = SwordAiMovementBaseline.ValidateInputs(state,
            new(comparisonProfile.ActorId, comparisonProfile.TargetId, actor!.Position), candidateProfile, scoreProfile, alreadyActed);
        if (invalid != SwordAiMovementBaselineRejection.None)
            return Reject(SwordAiMovementScenarioRejection.AdmissionRejected, movementReason: invalid);
        // Source reach adds RAW Move; admit only where it equals the spread's effective Move cap.
        if (actor.Move > 124) return Reject(SwordAiMovementScenarioRejection.UnsupportedProfile);
        var geometry = FlatOrdinaryMovement.Evaluate(state, actor.Id, candidateProfile.Movement).Outcome!;
        byte Distance(TilePosition p) => geometry.TargetDistanceRows.Single(row => row.Y == p.Y).Distances[p.X];
        int distance = Distance(actor.Position), reach = actor.Move + 1;
        if (!outOfReach && distance > reach) return Reject(SwordAiMovementScenarioRejection.OutOfReach);
        if (outOfReach && distance <= reach) return Reject(SwordAiMovementScenarioRejection.WrongBranch);
        int limit = outOfReach ? distance - 1 : actor.Move + distance;
        var origins = geometry.AiOrigins.Where(p => Distance(p) <= limit).ToImmutableArray();
        if (origins.IsEmpty) origins = geometry.AiOrigins; // Original no-match filter copies scenario 0.

        var turns = state.TurnBattle.Turns.Units;
        static int Delay(TurnUnitState unit) => (Math.Max(0, 100 - unit.Ct) + unit.Speed - 1) / unit.Speed;
        bool imminent = Delay(turns.Single(u => u.Id == comparisonProfile.TargetId)) <= Delay(turns.Single(u => u.Id == actor.Id));
        var movement = ImmutableArray.CreateBuilder<SwordAiScoredMovementTile>(origins.Length);
        var ordering = ImmutableArray.CreateBuilder<SwordAiMovementTileOrdering>(origins.Length);
        var rng = decisionRng;
        foreach (var tile in origins) // Existing geometry emits layer/y/x.
        {
            var evaluation = SwordAiMovementBaseline.EvaluateFreshTile(state,
                new(actor.Id, comparisonProfile.TargetId, tile), candidateProfile, scoreProfile, rng, alreadyActed);
            if (!evaluation.Accepted)
                return Reject(SwordAiMovementScenarioRejection.EvaluationRejected, SwordAiMovementScenarioStage.Movement,
                    movement.Count, evaluation.Rejection);
            movement.Add(new(evaluation.Outcome!, evaluation.RngState)); rng = evaluation.RngState;
            byte proximity = imminent ? (byte)Math.Max(0, 10 - Distance(tile)) : (byte)0;
            ushort pattern = unchecked((ushort)-evaluation.Outcome!.Score.Priority);
            ordering.Add(new(tile, Distance(tile), proximity, unchecked((pattern << 16) + proximity)));
        }
        var values = ordering.ToImmutable();
        int effectiveReach = reach;
        SwordAiReachAdjustment? adjustment = null;
        if (team == SwordAiScenarioTeam.NonBlue)
        {
            uint raw;
            try { var draw = new FftBiosRandom().Next(rng); rng = draw.State; raw = draw.Value; }
            catch (OverflowException)
            { return Reject(SwordAiMovementScenarioRejection.RandomDrawCountExhausted, SwordAiMovementScenarioStage.ReachAdjustment); }
            int modulo = (int)(raw % 7);
            if (modulo == 0) effectiveReach /= 2;
            adjustment = new(raw, modulo, reach, effectiveReach);
        }
        bool attackFallback = false, baselineFallback = false;
        TilePosition d0, d1;
        if (outOfReach)
        {
            var within = values.Where(c => c.TargetDistance <= effectiveReach).ToArray();
            attackFallback = within.Length == 0;
            d1 = Select(attackFallback ? values : within, attackFallback);
            d0 = d1; // Source copies D1; it never runs the in-reach D0 chooser here.
        }
        else
        {
            d1 = Select(values, false);
            if (alreadyActed) d0 = d1; // Source ACT-used branch skips the stricter D0 chooser.
            else
            {
                var closer = values.Where(c => c.TargetDistance <= distance - 1).ToArray();
                baselineFallback = closer.Length == 0;
                d0 = Select(baselineFallback ? values : closer, baselineFallback);
            }
        }
        var scenario = new SwordAiCompositionScenario(origins, d0, d1);
        return new(new(true, null, new(scenario, movement.ToImmutable(), origins.IndexOf(d0), values,
            distance, reach, baselineFallback), rng), effectiveReach, adjustment, attackFallback);
    }

    private static TilePosition Select(IEnumerable<SwordAiMovementTileOrdering> cells, bool distanceFirst) =>
        (distanceFirst
            ? cells.OrderBy(c => c.TargetDistance).ThenBy(c => c.PackedScore)
            : cells.OrderBy(c => c.PackedScore).ThenBy(c => c.TargetDistance))
        .ThenByDescending(c => c.Tile.Layer).ThenByDescending(c => c.Tile.Y).ThenByDescending(c => c.Tile.X).First().Tile;
}
