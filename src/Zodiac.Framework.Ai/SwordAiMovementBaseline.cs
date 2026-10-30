using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

public sealed record SwordAiMovementBaselineRequest(BattleUnitId ActorId, BattleUnitId TargetId, TilePosition Destination);

/// <summary>Unrepresented source facts require explicit opt-in; IDs and reachability cannot prove them.</summary>
/// <remarks>The supplied destination may differ from the Attack scan's scenario-1 destination.</remarks>
public sealed record SwordAiMovementBaselineProfile(bool OriginalScenarioZeroDestination = false,
    bool FreshSourceScoreCell = false, bool NormalScoring = false, bool CanonicalCoordinates = false);

public enum SwordAiMovementBaselineRejection
{
    None, UnsupportedBaselineProfile, UnsupportedScoreProfile, UnknownActor, UnknownTarget,
    TerminalInput, UnavailableAction, UnsupportedProfile, InvalidDestination,
    RandomDrawCountExhausted, TickCountExhausted
}

/// <summary>Immutable hypothetical data only; no active turn, executable command or live RNG commitment.</summary>
public sealed record SwordAiMovementBaselineOutcome(BattleUnitId ActorId, BattleUnitId TargetId,
    TilePosition Destination, int TurnEndCt, ImmutableArray<AiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks,
    ImmutableArray<AiUpkeepPick> UpkeepPicks, AiCandidateScore Score);

/// <summary>Rejected queries return the incoming decision RNG and no partial outcome.</summary>
public sealed record SwordAiMovementBaselineResult(bool Accepted, SwordAiMovementBaselineRejection Rejection,
    SwordAiMovementBaselineOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh move-only simulation/absolute scoring of one certified original baseline tile.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-135/136/176/180.
/// This isolated evaluation consumes one upkeep draw, not the full original scenario search's RNG schedule.
/// No weapon formula, scenario generation, cached-outcome input, ranking, lottery or runtime activation.
/// </remarks>
public static class SwordAiMovementBaseline
{
    public static SwordAiMovementBaselineResult Evaluate(AttackBattleSnapshot state, SwordAiMovementBaselineRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiMovementBaselineProfile baselineProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, baselineProfile, decisionRng, alreadyActed: false);

    /// <summary>Fresh post-ACT tile score with Move unused; staying refunds Move only, moving refunds neither choice.</summary>
    /// <remarks>Uses actual post-ACT state/RNG, retaining the same source-cell/geometry/scoring certificates.</remarks>
    public static SwordAiMovementBaselineResult EvaluateAfterAct(AttackBattleSnapshot state, SwordAiMovementBaselineRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiMovementBaselineProfile baselineProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, baselineProfile, decisionRng, alreadyActed: true);

    private static SwordAiMovementBaselineResult EvaluateCore(AttackBattleSnapshot state, SwordAiMovementBaselineRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        SwordAiMovementBaselineProfile baselineProfile, FftRandomState decisionRng, bool alreadyActed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(baselineProfile);
        SwordAiMovementBaselineResult Reject(SwordAiMovementBaselineRejection reason) => new(false, reason, null, decisionRng);
        if (!SupportsProfile(baselineProfile))
            return Reject(SwordAiMovementBaselineRejection.UnsupportedBaselineProfile);
        return EvaluateFreshTile(state, request, candidateProfile, scoreProfile, decisionRng, alreadyActed);
    }

    internal static bool SupportsProfile(SwordAiMovementBaselineProfile profile) =>
        profile.OriginalScenarioZeroDestination && profile.FreshSourceScoreCell && profile.NormalScoring && profile.CanonicalCoordinates;

    // Pure admission shared with scenario derivation; does not consume hypothetical ticks or draws.
    internal static SwordAiMovementBaselineRejection ValidateInputs(AttackBattleSnapshot state,
        SwordAiMovementBaselineRequest request, SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile,
        bool alreadyActed = false)
    {
        if (!scoreProfile.Supports(state, candidateProfile))
            return SwordAiMovementBaselineRejection.UnsupportedScoreProfile;

        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == request.ActorId);
        if (actor is null) return SwordAiMovementBaselineRejection.UnknownActor;
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == request.TargetId);
        if (target is null) return SwordAiMovementBaselineRejection.UnknownTarget;
        if (actor.UnitId == target.UnitId) return SwordAiMovementBaselineRejection.UnsupportedProfile;
        if (state.Combatants.Any(u => u.Hp == 0)) return SwordAiMovementBaselineRejection.TerminalInput;
        var turns = state.TurnBattle.Turns;
        var actorTurn = turns.Units.Single(u => u.Id == actor.UnitId);
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actor.UnitId ||
            actorTurn.ActionTaken != alreadyActed || alreadyActed && actorTurn.MovementTaken)
            return SwordAiMovementBaselineRejection.UnavailableAction;
        try { candidateProfile.Sword.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return SwordAiMovementBaselineRejection.UnsupportedProfile; }
        var movement = FlatOrdinaryMovement.Evaluate(state, actor.UnitId, candidateProfile.Movement);
        if (!movement.Accepted) return SwordAiMovementBaselineRejection.UnsupportedProfile;
        if (!movement.Outcome!.AiOrigins.Contains(request.Destination))
            return SwordAiMovementBaselineRejection.InvalidDestination;
        return SwordAiMovementBaselineRejection.None;
    }

    // The composition owns source-mask/context admission. Non-D0 cells do not assert they are scenario-0 destinations.
    internal static SwordAiMovementBaselineResult EvaluateFreshTile(AttackBattleSnapshot state, SwordAiMovementBaselineRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, FftRandomState decisionRng,
        bool alreadyActed = false)
    {
        SwordAiMovementBaselineResult Reject(SwordAiMovementBaselineRejection reason) => new(false, reason, null, decisionRng);
        var invalid = ValidateInputs(state, request, candidateProfile, scoreProfile, alreadyActed);
        if (invalid != SwordAiMovementBaselineRejection.None) return Reject(invalid);
        var result = AiMovementSimulation.Evaluate(state, request.ActorId, request.TargetId,
            request.Destination, candidateProfile.Units, decisionRng);
        if (result.Rejection != AiSimulationClockRejection.None)
            return Reject(result.Rejection == AiSimulationClockRejection.RandomDrawCountExhausted
                ? SwordAiMovementBaselineRejection.RandomDrawCountExhausted : SwordAiMovementBaselineRejection.TickCountExhausted);
        var outcome = result.Outcome!;
        return new(true, SwordAiMovementBaselineRejection.None,
            new(outcome.ActorId, outcome.TargetId, outcome.Destination, outcome.TurnEndCt, outcome.Units,
                outcome.MoveOpportunityUnitId, outcome.ElapsedTicks, outcome.Ticks, outcome.UpkeepPicks, outcome.Score), result.RngState);
    }
}
