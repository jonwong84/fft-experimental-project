using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Caller certification of one original ordinary Attack pass; these are semantic facts, not packed bits.</summary>
/// <remarks>Includes the weapon summary, canonical coordinates and source scenario/target context absent from live state.</remarks>
public sealed record SwordAiScanProfile(bool TargetActive, bool OrdinaryAttackTilePolicy = false,
    bool OriginalScenarioMask = false, bool TargetSelectionMatchesBaseline = false, bool FreshAbilityBestReset = false);

/// <summary>A supplied movement scenario, copied before validation; destinations are logical tiles, not source bit masks.</summary>
public sealed class SwordAiScanScenario
{
    public ImmutableArray<TilePosition> Origins { get; }
    public TilePosition? Destination { get; }

    public SwordAiScanScenario(IEnumerable<TilePosition> origins, TilePosition? destination)
    {
        ArgumentNullException.ThrowIfNull(origins);
        Origins = origins.ToImmutableArray();
        Destination = destination;
    }
}

public enum SwordAiScanDisposition { Rejected, NoCandidates, NoScoredCandidate, Scanned }
public enum SwordAiScanRejection { None, UnsupportedScanProfile, UnsupportedState, InvalidScenario, EvaluationRejected }

public sealed record SwordAiScannedCandidate(TilePosition Origin, TilePosition TargetPanel,
    SwordAiCandidate Candidate, SwordAiScoringResult Evaluation, byte Proximity);
public sealed record SwordAiScanOutcome(ImmutableArray<SwordAiScannedCandidate> Candidates,
    SwordAiComparisonCache Cache, int? RetainedCandidateIndex);

/// <summary>Rejected scans publish no partial payload and return the original query RNG.</summary>
/// <remarks>Atomic unsupported/exhaustion rollback is managed policy, not original C search rewind.</remarks>
public sealed record SwordAiScanResult(SwordAiScanDisposition Disposition, SwordAiScanRejection Rejection,
    int? FailedCandidateIndex, SwordAiScoringRejection ScoringRejection, SwordAiCandidateRejection ProjectionRejection,
    SwordAiScanOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure, synchronous enumeration and fresh scoring of one certified ordinary-sword scenario.</summary>
/// <remarks>
/// Source: fft_decomp 11464943bbdbd2a0af022d90cb7f10455cf911df, ZOD-130/131.
/// Stops before ranked-list insertion. No scenario generation, policy, command authority or live RNG commitment.
/// </remarks>
public static class SwordAiCandidateScan
{
    public static SwordAiScanResult Evaluate(AttackBattleSnapshot state, SwordAiScanScenario scenario,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiComparisonProfile comparisonProfile, SwordAiScanProfile scanProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        ArgumentNullException.ThrowIfNull(scanProfile);
        SwordAiScanResult Reject(SwordAiScanRejection reason, int? index = null,
            SwordAiScoringRejection scoreReason = SwordAiScoringRejection.None,
            SwordAiCandidateRejection projectionReason = SwordAiCandidateRejection.None) =>
            new(SwordAiScanDisposition.Rejected, reason, index, scoreReason, projectionReason, null, decisionRng);

        if (!scanProfile.OrdinaryAttackTilePolicy || !scanProfile.OriginalScenarioMask ||
            !scanProfile.TargetSelectionMatchesBaseline || !scanProfile.FreshAbilityBestReset ||
            !comparisonProfile.FreshDecisionBaseline || string.IsNullOrWhiteSpace(comparisonProfile.ActorId.Value) ||
            string.IsNullOrWhiteSpace(comparisonProfile.TargetId.Value) || comparisonProfile.ActorId == comparisonProfile.TargetId)
            return Reject(SwordAiScanRejection.UnsupportedScanProfile);
        if (!scoreProfile.Supports(state, candidateProfile))
            return Reject(SwordAiScanRejection.UnsupportedScanProfile, scoreReason: SwordAiScoringRejection.UnsupportedScoreProfile);

        // Pure admission even for empty/inactive scans. No hypothetical placement, formula draw or clock work here.
        var actorId = comparisonProfile.ActorId; var targetId = comparisonProfile.TargetId;
        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == actorId);
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == targetId);
        if (actor is null) return Reject(SwordAiScanRejection.UnsupportedState, projectionReason: SwordAiCandidateRejection.UnknownActor);
        if (target is null) return Reject(SwordAiScanRejection.UnsupportedState, projectionReason: SwordAiCandidateRejection.UnknownTarget);
        if (state.Combatants.Any(u => u.Hp == 0))
            return Reject(SwordAiScanRejection.UnsupportedState, projectionReason: SwordAiCandidateRejection.TerminalInput);
        var turns = state.TurnBattle.Turns;
        var actorTurn = turns.Units.Single(u => u.Id == actorId);
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actorId || actorTurn.ActionTaken)
            return Reject(SwordAiScanRejection.UnsupportedState, projectionReason: SwordAiCandidateRejection.UnavailableAction);
        if (weapon is null || actor.RightHandWeaponId != weapon.Id ||
            (2 * Math.Max(1, actor.PhysicalAttack) - 1) * weapon.Power > short.MaxValue)
            return Reject(SwordAiScanRejection.UnsupportedState, projectionReason: SwordAiCandidateRejection.UnsupportedProfile);
        try { candidateProfile.Sword.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return Reject(SwordAiScanRejection.UnsupportedState, projectionReason: SwordAiCandidateRejection.UnsupportedProfile); }
        var movement = FlatOrdinaryMovement.Evaluate(state, actorId, candidateProfile.Movement);
        if (!movement.Accepted)
            return Reject(SwordAiScanRejection.UnsupportedState, projectionReason: SwordAiCandidateRejection.UnsupportedProfile);

        var mask = scenario.Origins.ToHashSet();
        if (mask.Count != scenario.Origins.Length || !mask.IsSubsetOf(movement.Outcome!.AiOrigins) ||
            (mask.Count == 0 ? scenario.Destination is not null : scenario.Destination is not { } destination || !mask.Contains(destination)))
            return Reject(SwordAiScanRejection.InvalidScenario);

        state.TurnBattle.Battle.TryGetUnit(actorId, out var actorBody);
        state.TurnBattle.Battle.TryGetUnit(targetId, out var targetBody);
        var targetPosition = targetBody!.Position;
        // The ordinary profile guarantees living positive-Speed units and delays in 0..100.
        static int Delay(TurnUnitState unit) => (Math.Max(0, 100 - unit.Ct) + unit.Speed - 1) / unit.Speed;
        byte actorDelay = (byte)Delay(actorTurn);
        int targetDelay = Delay(turns.Units.Single(u => u.Id == targetId));
        bool eligible = targetDelay <= actorDelay;
        var proximity = mask.ToDictionary(p => p, p => eligible
            ? (byte)Math.Max(0, 10 - Math.Abs(targetPosition.X - p.X) - Math.Abs(targetPosition.Y - p.Y)) : (byte)0);
        var evaluated = ImmutableArray.CreateBuilder<SwordAiScannedCandidate>();
        var rng = decisionRng;
        int? retained = null; // Equivalent to fresh source reset only within this certified score range.
        if (scanProfile.TargetActive)
        foreach (var origin in mask.OrderBy(p => p.Layer).ThenBy(p => p.Y).ThenBy(p => p.X))
        {
            // Closed range1/AoE0 tile targeting: intersect the scenario with the sole target's actual sword range.
            if (origin.Layer != targetPosition.Layer || Math.Abs(origin.X - targetPosition.X) + Math.Abs(origin.Y - targetPosition.Y) != 1)
                continue;
            bool actFirst = origin == actorBody!.Position;
            var candidate = new SwordAiCandidate(actorId, targetId, actFirst ? scenario.Destination!.Value : origin,
                actFirst ? SwordAiCandidateOrder.ActThenMove : SwordAiCandidateOrder.MoveThenAct);
            var score = SwordAiCandidateScoring.Evaluate(state, candidate, weapon, candidateProfile, scoreProfile, rng);
            if (score.Disposition == SwordAiScoringDisposition.Rejected)
                return Reject(SwordAiScanRejection.EvaluationRejected, evaluated.Count, score.Rejection,
                    score.Projection?.Rejection ?? SwordAiCandidateRejection.None);
            var current = new SwordAiScannedCandidate(origin, targetPosition, candidate, score, proximity[candidate.Destination]);
            evaluated.Add(current);
            rng = score.RngState; // All supported evaluations retain draws, including zero accuracy and losing scores.
            if (score.Score is { } challenger && (retained is null || IsHigher(challenger, current.Proximity,
                evaluated[retained.Value].Evaluation.Score!, evaluated[retained.Value].Proximity)))
                retained = evaluated.Count - 1;
        }

        var candidates = evaluated.ToImmutable();
        var cache = new SwordAiComparisonCache(actorDelay, targetDelay, eligible ? targetId : null,
            candidates.Select(c => new SwordAiDestinationProximity(c.Candidate.Destination, c.Proximity)).ToImmutableArray());
        return new(candidates.IsEmpty ? SwordAiScanDisposition.NoCandidates : retained is null
                ? SwordAiScanDisposition.NoScoredCandidate : SwordAiScanDisposition.Scanned,
            SwordAiScanRejection.None, null, SwordAiScoringRejection.None, SwordAiCandidateRejection.None,
            new(candidates, cache, retained), rng);
    }

    private static bool IsHigher(SwordAiCandidateScore challenger, byte proximity, SwordAiCandidateScore incumbent, byte incumbentProximity)
    {
        if (challenger.Priority != incumbent.Priority) return challenger.Priority > incumbent.Priority;
        if (challenger.HitPercent != incumbent.HitPercent) return challenger.HitPercent > incumbent.HitPercent;
        return proximity < incumbentProximity; // Equality retains the earliest scored candidate.
    }
}
