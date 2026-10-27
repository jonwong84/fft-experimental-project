using System.Collections.Immutable;

namespace Zodiac.Framework;

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
    TilePosition Destination, int TurnEndCt, ImmutableArray<SwordAiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks,
    ImmutableArray<SwordAiUpkeepPick> UpkeepPicks, SwordAiCandidateScore Score);

/// <summary>Rejected queries return the incoming decision RNG and no partial outcome.</summary>
public sealed record SwordAiMovementBaselineResult(bool Accepted, SwordAiMovementBaselineRejection Rejection,
    SwordAiMovementBaselineOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh move-only simulation/absolute scoring of one certified original baseline tile.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-135/136.
/// This isolated evaluation consumes one upkeep draw, not the full original scenario search's RNG schedule.
/// No weapon formula, scenario generation, cached-outcome input, ranking, lottery or runtime activation.
/// </remarks>
public static class SwordAiMovementBaseline
{
    public static SwordAiMovementBaselineResult Evaluate(AttackBattleSnapshot state, SwordAiMovementBaselineRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiMovementBaselineProfile baselineProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(baselineProfile);
        SwordAiMovementBaselineResult Reject(SwordAiMovementBaselineRejection reason) => new(false, reason, null, decisionRng);
        if (!SupportsProfile(baselineProfile))
            return Reject(SwordAiMovementBaselineRejection.UnsupportedBaselineProfile);
        return EvaluateFreshTile(state, request, candidateProfile, scoreProfile, decisionRng);
    }

    internal static bool SupportsProfile(SwordAiMovementBaselineProfile profile) =>
        profile.OriginalScenarioZeroDestination && profile.FreshSourceScoreCell && profile.NormalScoring && profile.CanonicalCoordinates;

    // The composition owns source-mask/context admission. Non-D0 cells do not assert they are scenario-0 destinations.
    internal static SwordAiMovementBaselineResult EvaluateFreshTile(AttackBattleSnapshot state, SwordAiMovementBaselineRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile, FftRandomState decisionRng)
    {
        SwordAiMovementBaselineResult Reject(SwordAiMovementBaselineRejection reason) => new(false, reason, null, decisionRng);
        if (!scoreProfile.Supports(state, candidateProfile))
            return Reject(SwordAiMovementBaselineRejection.UnsupportedScoreProfile);

        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == request.ActorId);
        if (actor is null) return Reject(SwordAiMovementBaselineRejection.UnknownActor);
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == request.TargetId);
        if (target is null) return Reject(SwordAiMovementBaselineRejection.UnknownTarget);
        if (actor.UnitId == target.UnitId) return Reject(SwordAiMovementBaselineRejection.UnsupportedProfile);
        if (state.Combatants.Any(u => u.Hp == 0)) return Reject(SwordAiMovementBaselineRejection.TerminalInput);
        var turns = state.TurnBattle.Turns;
        var actorTurn = turns.Units.Single(u => u.Id == actor.UnitId);
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actor.UnitId || actorTurn.ActionTaken)
            return Reject(SwordAiMovementBaselineRejection.UnavailableAction);
        try { candidateProfile.Sword.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return Reject(SwordAiMovementBaselineRejection.UnsupportedProfile); }
        var movement = FlatOrdinaryMovement.Evaluate(state, actor.UnitId, candidateProfile.Movement);
        if (!movement.Accepted) return Reject(SwordAiMovementBaselineRejection.UnsupportedProfile);
        if (!movement.Outcome!.AiOrigins.Contains(request.Destination))
            return Reject(SwordAiMovementBaselineRejection.InvalidDestination);

        var battle = state.TurnBattle.Battle;
        battle.TryGetUnit(actor.UnitId, out var body);
        bool moved = actorTurn.MovementTaken || request.Destination != body!.Position;
        int turnEndCt = Math.Min(60, actorTurn.Ct + 20 + (moved ? 0 : 20)); // ACT remains unused.
        var units = turns.Units.OrderBy(u => u.TieKey).Select(turn =>
        {
            var combat = state.Combatants.Single(u => u.UnitId == turn.Id);
            battle.TryGetUnit(turn.Id, out var unitBody);
            bool isActor = turn.Id == actor.UnitId;
            return new SwordAiProjectedUnit(turn.Id, isActor ? request.Destination : unitBody!.Position, combat.Facing,
                combat.Hp, combat.MaxHp, combat.Status, candidateProfile.Units.Single(u => u.UnitId == turn.Id).DeathCounter,
                turn.Speed, turn.TieKey, isActor ? turnEndCt : turn.Ct,
                isActor ? moved : turn.MovementTaken, turn.ActionTaken);
        }).ToArray();

        var clockRejection = SwordAiSimulationClock.Advance(units, turns.Ticks, decisionRng, out var clock);
        if (clockRejection != SwordAiCandidateRejection.None)
            return Reject(clockRejection == SwordAiCandidateRejection.RandomDrawCountExhausted
                ? SwordAiMovementBaselineRejection.RandomDrawCountExhausted : SwordAiMovementBaselineRejection.TickCountExhausted);
        var score = SwordAiCandidateScoring.ScoreFreshUnits(clock!.Units, target.UnitId, 100);
        // Negating into the source u16 tile grid and back to s16 preserves this closed -160..160 score.
        return new(true, SwordAiMovementBaselineRejection.None,
            new(actor.UnitId, target.UnitId, request.Destination, turnEndCt, clock.Units,
                clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks, score), clock.RngState);
    }
}
