using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Unrepresented original ordinary Attack tile/exhaustive policy; other entry/reset/mask facts are derived by the owner.</summary>
public sealed record RosterSwordAiAttackProfile(bool OrdinaryAttackTilePolicy = false);
public sealed record RosterSwordAiScannedCandidate(TilePosition Origin, TilePosition TargetPanel,
    SwordAiCandidate Candidate, RosterSwordAiCandidateResult Evaluation, byte Proximity);
public enum RosterSwordAiAttackStage { Admission, Prefix, Candidate }
public sealed record RosterSwordAiAttackFailure(RosterSwordAiAttackStage Stage, RosterAiGeneralFailure? Prefix = null,
    RosterSwordAiCandidateRejection CandidateReason = RosterSwordAiCandidateRejection.None, int? CandidateIndex = null);
public sealed record RosterSwordAiAttackOutcome(RosterAiGeneralScenarioOutcome Scenarios, AiPreparedAbility Entry,
    ImmutableArray<TilePosition> CastOrigins, ImmutableArray<BattleUnitId> AimTargets,
    ImmutableArray<RosterSwordAiScannedCandidate> Candidates, int? BestCandidateIndex, AiRankOrder? BaselineComparison)
{
    public bool Skipped => !Entry.Usable;
    public bool BeatsMovementBaseline => BaselineComparison == AiRankOrder.Above;
}
public sealed record RosterSwordAiAttackResult(bool Accepted, RosterSwordAiAttackFailure? Failure,
    RosterSwordAiAttackOutcome? Outcome, RandomState RngState);

/// <summary>One fresh general prefix and exhaustive ordinary Attack occurrence across all active useful enemies. No ranked array, lottery or command.</summary>
/// <remarks>ZOD-296; decomp 11464943bbdbd2a0af022d90cb7f10455cf911df. Origins and target panels use map order; complete ties keep first.</remarks>
internal static class RosterSwordAiAttackEntry
{
    public static RosterSwordAiAttackResult Evaluate(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile general, RosterAiMovementProfile scoring, BoundedSwordWeapon? weapon,
        RosterSwordAiAttackProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.OrdinaryAttackTilePolicy) return new(false, new(RosterSwordAiAttackStage.Admission), null, rng);
        var prefix = RosterAiGeneralScenarios.Evaluate(state, request, simulation, refresh, lists, general, scoring, rng);
        if (!prefix.Accepted) return new(false, new(RosterSwordAiAttackStage.Prefix, prefix.Failure), null, rng);
        var result = EvaluateFresh(state, request.ActorId, weapon, simulation, prefix.Outcome!, prefix.RngState);
        return result.Accepted ? result : result with { RngState = rng };
    }

    internal static RosterSwordAiAttackResult EvaluateFresh(AttackBattleSnapshot state, BattleUnitId actorId, BoundedSwordWeapon? weapon,
        RosterAiSimulationProfile simulation, RosterAiGeneralScenarioOutcome m, RandomState rng)
    {
        RosterSwordAiAttackResult Reject(RosterSwordAiAttackFailure failure) => new(false, failure, null, rng);
        var entry = m.Preparation.Preparation.ActorEntries[0];
        if (!entry.Usable) return new(true, null, new(m, entry, [], [], [], null, null), rng);
        var actor = state.Combatants.Single(u => u.UnitId == actorId);
        if (!SwordAiFormulaProjection.WeaponFits(actor, weapon))
            return Reject(new(RosterSwordAiAttackStage.Admission, CandidateReason: RosterSwordAiCandidateRejection.UnsupportedProfile));
        var targets = AiRosterCastGeometry.UsefulEnemies(m.Preparation).OrderBy(u => u.Position.Layer)
            .ThenBy(u => u.Position.Y).ThenBy(u => u.Position.X).ToImmutableArray();
        var origins = AiRosterCastGeometry.Origins(m.Scenario1, targets, 1);
        var origin = m.Preparation.Preparation.Targets.ById[actorId].Position;
        var candidates = ImmutableArray.CreateBuilder<RosterSwordAiScannedCandidate>();var current = rng;int? best = null;
        foreach (var tile in origins)
        foreach (var target in targets)
        {
            if (tile.Layer != target.Position.Layer || AiRosterNearest.Distance(tile, target.Position) != 1) continue;
            bool actFirst = tile == origin;
            var candidate = new SwordAiCandidate(actorId, target.UnitId, actFirst ? m.ActFirstDestination : tile,
                actFirst ? SwordAiCandidateOrder.ActThenMove : SwordAiCandidateOrder.MoveThenAct);
            var evaluation = RosterSwordAiCandidate.EvaluateFresh(state, candidate, weapon, simulation, current);
            if (evaluation.Disposition == AiScoringDisposition.Rejected)
                return Reject(new(RosterSwordAiAttackStage.Candidate, CandidateReason: evaluation.Rejection, CandidateIndex: candidates.Count));
            byte proximity = m.Preparation.Proximity.WrittenValues[candidate.Destination];
            if (evaluation.Score is { } score && (best is null || SwordAiCandidateScan.IsHigher(score, proximity,
                candidates[best.Value].Evaluation.Score!, candidates[best.Value].Proximity))) best = candidates.Count;
            candidates.Add(new(tile, target.Position, candidate, evaluation, proximity)); current = evaluation.RngState;
        }
        var comparison = best is { } index ? AiCandidateRank.Compare(candidates[index].Evaluation.Score!, m.Movement[m.BaselineIndex].Outcome.Score) : (AiRankOrder?)null;
        return new(true, null, new(m, entry, origins, targets.Select(u => u.UnitId).ToImmutableArray(), candidates.ToImmutable(), best, comparison), current);
    }
}
