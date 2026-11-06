using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Initial aim and current-fact readiness hypothesis. Movement opportunities are supplied at this explicit boundary.</summary>
public sealed record OrdinarySpellAiReadyRequest(BattleUnitId ActorId, int PrimarySlot, TilePosition Aim,
    ImmutableHashSet<BattleUnitId> HadMoveOpportunity, bool FollowUnit = true);
public enum OrdinarySpellAiReadyDisposition { Rejected, NotValuable, Scored }
public sealed record OrdinarySpellAiReadyOutcome(OrdinarySpellAiConsidered Ability,
    ImmutableArray<OrdinarySpellAiUsefulTarget> Usefulness, ImmutableArray<MagicCastTargetResult> Frames,
    OrdinarySpellAiAccountingOutcome Accounting, ImmutableArray<OrdinarySpellAiUnit> Units, OrdinarySpellAiScore? Score);
public sealed record OrdinarySpellAiReadyResult(OrdinarySpellAiReadyDisposition Disposition, string? Rejection,
    OrdinarySpellAiReadyOutcome? Outcome, RandomState RngState)
{
    public bool Accepted => Disposition != OrdinarySpellAiReadyDisposition.Rejected;
}

/// <summary>Owns fresh ready-hit calculations at one initial aim; never a full delayed candidate or command authority.</summary>
public static class OrdinarySpellAiReadyQuery
{
    public static OrdinarySpellAiReadyResult Evaluate(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiReadyRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(request);
        OrdinarySpellAiReadyResult Reject(string reason) => new(OrdinarySpellAiReadyDisposition.Rejected, reason, null, rng);
        if (request.HadMoveOpportunity is null || request.HadMoveOpportunity.Any(id => !state.Units.Any(u => u.Id == id)))
            return Reject("Explicit current-roster movement opportunities are required.");
        try
        {
            var work = new OrdinarySpellAiWork(state, catalog, request.ActorId, request.PrimarySlot, request.FollowUnit);
            if (!work.Usefulness.Any(t => t.Effect == OrdinarySpellAiEffect.Useful))
                return new(OrdinarySpellAiReadyDisposition.NotValuable, null,
                    new(work.Ability, work.Usefulness, [], new([], 0, 0, false), work.Units, null), rng);
            var aim = work.Capture(request.Aim, request.FollowUnit);
            var hit = OrdinarySpellAiHitWork.Resolve(work, work.Units, aim, request.HadMoveOpportunity, rng);
            var score = hit.Accounting.Valuable ? work.Score(hit.Units, hit.Accounting.HitPercent, hit.Accounting.PriorityDelta) : null;
            return new(score is null ? OrdinarySpellAiReadyDisposition.NotValuable : OrdinarySpellAiReadyDisposition.Scored,
                null, new(work.Ability, work.Usefulness, hit.Frames, hit.Accounting, hit.Units, score), hit.RngState);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ready-hit context: " + e.Message);
        }
    }
}
