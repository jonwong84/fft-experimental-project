using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>One supplied initial aim at the actor's current origin. No move, target search or retained-plan authority.</summary>
public sealed record OrdinarySpellAiCandidateRequest(BattleUnitId ActorId, int PrimarySlot, TilePosition Aim, bool FollowUnit = true);
public enum OrdinarySpellAiCandidateDisposition { Rejected, NotValuable, Scored, ScoredUnresolved }
public sealed record OrdinarySpellAiCandidateOutcome(OrdinarySpellAiConsidered Ability, byte ActualChargeTime,
    StoredSpellAim? Aim, ImmutableArray<OrdinarySpellAiUsefulTarget> Usefulness, OrdinarySpellAiClockOutcome? Clock,
    RandomState? FormulaEntryRng, ImmutableArray<MagicCastTargetResult> Frames,
    OrdinarySpellAiAccountingOutcome Accounting, ImmutableArray<OrdinarySpellAiUnit> Units, OrdinarySpellAiScore? Score);
public sealed record OrdinarySpellAiCandidateResult(OrdinarySpellAiCandidateDisposition Disposition, string? Rejection,
    OrdinarySpellAiCandidateOutcome? Outcome, RandomState RngState)
{
    public bool Accepted => Disposition != OrdinarySpellAiCandidateDisposition.Rejected;
}

/// <summary>Fresh fixed-origin ordinary spell simulation through source clock/readiness/scoring. No live adoption.</summary>
public static class OrdinarySpellAiCandidateQuery
{
    public static OrdinarySpellAiCandidateResult Evaluate(OrdinaryMagicBattleState state, OrdinarySpellAiCatalog catalog,
        OrdinarySpellAiCandidateRequest request, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(request);
        OrdinarySpellAiCandidateResult Reject(string reason) => new(OrdinarySpellAiCandidateDisposition.Rejected, reason, null, rng);
        if (state.ClockPhase != OrdinaryMagicClockPhase.UnitTurn || state.Units.Length > 21 ||
            state.Units.Any(u => u.Occupant.SourceOrder is < 0 or > 20))
            return Reject("The fixed-origin source clock profile is unavailable.");
        try
        {
            var work = new OrdinarySpellAiWork(state, catalog, request.ActorId, request.PrimarySlot, request.FollowUnit);
            return OrdinarySpellAiActionSimulation.Run(work, request, rng);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell simulation: " + e.Message);
        }
    }
}
