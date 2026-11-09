using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One supplied initial aim at the actor's current origin. No move, target search or retained-plan authority.</summary>
public sealed record DefaultAbilityAiCandidateRequest(BattleUnitId ActorId, int PrimarySlot, TilePosition Aim, bool FollowUnit = true);
public enum DefaultAbilityAiCandidateDisposition { Rejected, NotValuable, Scored, ScoredUnresolved }
public sealed record DefaultAbilityAiCandidateOutcome(DefaultAbilityAiConsidered Ability, byte ActualChargeTime,
    StoredSpellAim? Aim, ImmutableArray<DefaultAbilityAiUsefulTarget> Usefulness, DefaultAbilityAiClockOutcome? Clock,
    RandomState? FormulaEntryRng, ImmutableArray<MagicCastTargetResult> Frames,
    DefaultAbilityAiAccountingOutcome Accounting, ImmutableArray<DefaultAbilityAiUnit> Units, DefaultAbilityAiScore? Score);
public sealed record DefaultAbilityAiCandidateResult(DefaultAbilityAiCandidateDisposition Disposition, string? Rejection,
    DefaultAbilityAiCandidateOutcome? Outcome, RandomState RngState)
{
    public bool Accepted => Disposition != DefaultAbilityAiCandidateDisposition.Rejected;
}

/// <summary>Fresh fixed-origin ordinary spell simulation through source clock/readiness/scoring. No live adoption.</summary>
public static class DefaultAbilityAiCandidateQuery
{
    public static DefaultAbilityAiCandidateResult Evaluate(DefaultAbilityBattleState state, DefaultAbilityAiCatalog catalog,
        DefaultAbilityAiCandidateRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(request);
        DefaultAbilityAiCandidateResult Reject(string reason) => new(DefaultAbilityAiCandidateDisposition.Rejected, reason, null, rng);
        if (state.ClockPhase != DefaultAbilityClockPhase.UnitTurn || state.Units.Length > 21 ||
            state.Units.Any(u => u.Occupant.SourceOrder is < 0 or > 20))
            return Reject("The fixed-origin source clock profile is unavailable.");
        try
        {
            var work = new DefaultAbilityAiWork(state, catalog, request.ActorId, request.PrimarySlot, request.FollowUnit);
            return DefaultAbilityAiActionSimulation.AiActionSimulateAndScore(work, request, rng);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell simulation: " + e.Message);
        }
    }
}
