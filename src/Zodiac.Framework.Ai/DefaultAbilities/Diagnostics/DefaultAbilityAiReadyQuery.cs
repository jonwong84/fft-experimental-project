using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Initial aim and current-fact readiness hypothesis. Movement opportunities are supplied at this explicit boundary.</summary>
public sealed record DefaultAbilityAiReadyRequest(BattleUnitId ActorId, int PrimarySlot, TilePosition Aim,
    ImmutableHashSet<BattleUnitId> HadMoveOpportunity, bool FollowUnit = true);
public enum DefaultAbilityAiReadyDisposition { Rejected, NotValuable, Scored }
public sealed record DefaultAbilityAiReadyOutcome(DefaultAbilityAiConsidered Ability,
    ImmutableArray<DefaultAbilityAiUsefulTarget> Usefulness, ImmutableArray<MagicCastTargetResult> Frames,
    DefaultAbilityAiAccountingOutcome Accounting, ImmutableArray<DefaultAbilityAiUnit> Units, DefaultAbilityAiScore? Score);
public sealed record DefaultAbilityAiReadyResult(DefaultAbilityAiReadyDisposition Disposition, string? Rejection,
    DefaultAbilityAiReadyOutcome? Outcome, RandomState RngState)
{
    public bool Accepted => Disposition != DefaultAbilityAiReadyDisposition.Rejected;
}

/// <summary>Owns fresh ready-hit calculations at one initial aim; never a full delayed candidate or command authority.</summary>
public static class DefaultAbilityAiReadyQuery
{
    public static DefaultAbilityAiReadyResult Evaluate(DefaultAbilityBattleState state, DefaultAbilityAiCatalog catalog,
        DefaultAbilityAiReadyRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(request);
        DefaultAbilityAiReadyResult Reject(string reason) => new(DefaultAbilityAiReadyDisposition.Rejected, reason, null, rng);
        if (request.HadMoveOpportunity is null || request.HadMoveOpportunity.Any(id => !state.Units.Any(u => u.Id == id)))
            return Reject("Explicit current-roster movement opportunities are required.");
        try
        {
            var work = new DefaultAbilityAiWork(state, catalog, request.ActorId, request.PrimarySlot, request.FollowUnit);
            if (!work.Usefulness.Any(t => t.Effect == DefaultAbilityAiEffect.Useful))
                return new(DefaultAbilityAiReadyDisposition.NotValuable, null,
                    new(work.Ability, work.Usefulness, [], new([], 0, 0, false), work.Units, null), rng);
            var aim = work.Capture(request.Aim, request.FollowUnit);
            bool valuable = DefaultAbilityAiHitSimulation.ActionHitsSimulate(work, work.Units, aim, request.HadMoveOpportunity, rng, out var hit);
            var score = valuable ? work.Score(hit.Units, hit.Accounting.HitPercent, hit.Accounting.PriorityDelta) : null;
            return new(score is null ? DefaultAbilityAiReadyDisposition.NotValuable : DefaultAbilityAiReadyDisposition.Scored,
                null, new(work.Ability, work.Usefulness, hit.Frames, hit.Accounting, hit.Units, score), hit.RngState);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ready-hit context: " + e.Message);
        }
    }
}
