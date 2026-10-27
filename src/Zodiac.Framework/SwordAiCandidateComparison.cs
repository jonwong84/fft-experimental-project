using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Caller certification of original fresh action-selection cache provenance, not original packed flags.</summary>
/// <remarks>
/// True asserts initial decision flags, stable relation/targetability and cached actor delay correspond to this
/// original settled state after refresh/reachability, before any candidate simulation. Existing candidate,
/// movement and score certificates still apply. IDs cannot establish these unrepresented original facts.
/// </remarks>
public sealed record SwordAiComparisonProfile(BattleUnitId ActorId, BattleUnitId TargetId, bool FreshDecisionBaseline = false);

public enum SwordAiComparisonDisposition { Rejected, NoScoredCandidate, Compared }
public enum SwordAiComparisonRejection
{
    None, UnsupportedComparisonProfile, InvalidCandidatePair, UnsupportedMovementProfile, InvalidCacheDestination, EvaluationRejected
}
public enum SwordAiComparisonReason { None, Priority, HitPercent, Proximity, Equal, IncumbentOnly, ChallengerOnly, NeitherScored }

public sealed record SwordAiDestinationProximity(TilePosition Destination, byte Value);
public sealed record SwordAiComparisonCache(byte ActorDelay, int TargetDelay, BattleUnitId? EligibleEnemyId,
    ImmutableArray<SwordAiDestinationProximity> Destinations);
public sealed record SwordAiComparedCandidate(SwordAiCandidate Candidate, SwordAiScoringResult Evaluation)
{
    /// <summary>Every candidate has marker 0. The later original chosen marker 255 is outside this query.</summary>
    public byte DecidedMarker => 0;
}
public sealed record SwordAiComparisonOutcome(ImmutableArray<SwordAiComparedCandidate> Candidates,
    SwordAiComparisonCache Cache, int? RetainedCandidateIndex, SwordAiComparisonReason Reason);

/// <summary>Failure has no partial payload and returns the original pair RNG, including a late second failure.</summary>
/// <remarks>Pair rollback is a managed unsupported/exhaustion policy, not original C search rewind behavior.</remarks>
public sealed record SwordAiComparisonResult(SwordAiComparisonDisposition Disposition, SwordAiComparisonRejection Rejection,
    int? FailedCandidateIndex, SwordAiScoringRejection ScoringRejection, SwordAiCandidateRejection ProjectionRejection,
    SwordAiComparisonOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure opt-in comparison of an incumbent and challenger, freshly scored on the same original state.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-128/129.
/// No cached-result/cache input, enumeration, ranked list, lottery, command receipt or authoritative RNG commit.
/// </remarks>
public static class SwordAiCandidateComparison
{
    public static SwordAiComparisonResult Evaluate(AttackBattleSnapshot state, SwordAiCandidate incumbent,
        SwordAiCandidate challenger, BoundedSwordWeapon? weapon, SwordAiCandidateProfile candidateProfile,
        SwordAiScoreProfile scoreProfile, SwordAiComparisonProfile comparisonProfile, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(incumbent);
        ArgumentNullException.ThrowIfNull(challenger);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(comparisonProfile);
        SwordAiComparisonResult Reject(SwordAiComparisonRejection reason, int? index = null,
            SwordAiScoringRejection scoreReason = SwordAiScoringRejection.None,
            SwordAiCandidateRejection projectionReason = SwordAiCandidateRejection.None) =>
            new(SwordAiComparisonDisposition.Rejected, reason, index, scoreReason, projectionReason, null, decisionRng);

        if (!comparisonProfile.FreshDecisionBaseline || string.IsNullOrWhiteSpace(comparisonProfile.ActorId.Value) ||
            string.IsNullOrWhiteSpace(comparisonProfile.TargetId.Value) || comparisonProfile.ActorId == comparisonProfile.TargetId ||
            comparisonProfile.ActorId != incumbent.ActorId || comparisonProfile.TargetId != incumbent.TargetId)
            return Reject(SwordAiComparisonRejection.UnsupportedComparisonProfile);
        if (incumbent.ActorId != challenger.ActorId || incumbent.TargetId != challenger.TargetId ||
            !state.Combatants.Select(u => u.UnitId).ToHashSet().SetEquals([incumbent.ActorId, incumbent.TargetId]))
            return Reject(SwordAiComparisonRejection.InvalidCandidatePair);

        // Membership is derived from the original living geometry. Missing/unwritten source cells are never zero.
        var movement = FlatOrdinaryMovement.Evaluate(state, incumbent.ActorId, candidateProfile.Movement);
        if (!movement.Accepted) return Reject(SwordAiComparisonRejection.UnsupportedMovementProfile);
        SwordAiCandidate[] candidates = [incumbent, challenger];
        for (int i = 0; i < candidates.Length; i++)
            if (!movement.Outcome!.AiOrigins.Contains(candidates[i].Destination))
                return Reject(SwordAiComparisonRejection.InvalidCacheDestination, i);

        var turns = state.TurnBattle.Turns.Units;
        var actorTurn = turns.Single(u => u.Id == incumbent.ActorId);
        var targetTurn = turns.Single(u => u.Id == incumbent.TargetId);
        // Ordinary living None/Critical delays lie in 0..100, so source actor u8 assignment cannot truncate.
        static int Delay(TurnUnitState u) => (Math.Max(0, 100 - u.Ct) + u.Speed - 1) / u.Speed;
        byte actorDelay = (byte)Delay(actorTurn);
        int targetDelay = Delay(targetTurn);
        bool eligible = targetDelay <= actorDelay;
        state.TurnBattle.Battle.TryGetUnit(incumbent.TargetId, out var targetBody);
        var targetPosition = targetBody!.Position;
        byte Proximity(TilePosition destination) => eligible
            ? (byte)Math.Max(0, 10 - Math.Abs(targetPosition.X - destination.X) - Math.Abs(targetPosition.Y - destination.Y))
            : (byte)0;
        // Private provisional values become publishable only after both fresh scores validate all other certificates.
        var cache = new SwordAiComparisonCache(actorDelay, targetDelay, eligible ? incumbent.TargetId : null,
            [new(incumbent.Destination, Proximity(incumbent.Destination)), new(challenger.Destination, Proximity(challenger.Destination))]);
        var evaluated = ImmutableArray.CreateBuilder<SwordAiComparedCandidate>(2);
        var rng = decisionRng;
        foreach (var candidate in candidates)
        {
            // Exactly one fresh score per candidate, both on original state; only decision RNG is carried.
            var result = SwordAiCandidateScoring.Evaluate(state, candidate, weapon, candidateProfile, scoreProfile, rng);
            if (result.Disposition == SwordAiScoringDisposition.Rejected)
                return Reject(SwordAiComparisonRejection.EvaluationRejected, evaluated.Count, result.Rejection,
                    result.Projection?.Rejection ?? SwordAiCandidateRejection.None);
            evaluated.Add(new(candidate, result));
            rng = result.RngState; // Supported discard and NotValuable never refund their draws.
        }

        var first = evaluated[0].Evaluation.Score;
        var second = evaluated[1].Evaluation.Score;
        int? retained;
        SwordAiComparisonReason reason;
        if (first is null && second is null) { retained = null; reason = SwordAiComparisonReason.NeitherScored; }
        else if (first is null) { retained = 1; reason = SwordAiComparisonReason.ChallengerOnly; }
        else if (second is null) { retained = 0; reason = SwordAiComparisonReason.IncumbentOnly; }
        else
        {
            var comparison = Compare(first.Priority, first.HitPercent, cache.Destinations[0].Value,
                second.Priority, second.HitPercent, cache.Destinations[1].Value);
            retained = comparison.ChallengerIsHigher ? 1 : 0;
            reason = comparison.Reason;
        }
        return new(retained is null ? SwordAiComparisonDisposition.NoScoredCandidate : SwordAiComparisonDisposition.Compared,
            SwordAiComparisonRejection.None, null, SwordAiScoringRejection.None, SwordAiCandidateRejection.None,
            new(evaluated.ToImmutable(), cache, retained, reason), rng);
    }

    // Both candidate markers are 0; greater signed priority, greater hit, then strictly lower proximity wins.
    private static (bool ChallengerIsHigher, SwordAiComparisonReason Reason) Compare(short firstPriority, byte firstHit,
        byte firstProximity, short secondPriority, byte secondHit, byte secondProximity)
    {
        if (secondPriority != firstPriority) return (secondPriority > firstPriority, SwordAiComparisonReason.Priority);
        if (secondHit != firstHit) return (secondHit > firstHit, SwordAiComparisonReason.HitPercent);
        if (secondProximity != firstProximity) return (secondProximity < firstProximity, SwordAiComparisonReason.Proximity);
        return (false, SwordAiComparisonReason.Equal);
    }
}
