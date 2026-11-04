using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public enum AccumulateAiEffect { None, Useful, Adverse }
/// <summary>Explicit current entry mask and ordinary retail flags, not inferred from grants or synthetic AI metadata.</summary>
public sealed record AccumulateAiUsefulnessProfile(bool UsableRetailAlliedMapStatEntry = false,
    bool ActorActive = false, bool OpponentActive = false);
public sealed record AccumulateAiUsefulnessTarget(BattleUnitId UnitId, AccumulateAiEffect Effect, uint? Raw);
public sealed record AccumulateAiUsefulnessOutcome(ImmutableArray<AccumulateAiUsefulnessTarget> Targets, bool SelfUseful);
public enum AccumulateAiUsefulnessRejection { None, UnsupportedEntry, InvalidCandidate, RandomDrawCountExhausted }
public sealed record AccumulateAiUsefulnessResult(bool Accepted, AccumulateAiUsefulnessRejection Rejection,
    AccumulateAiCandidateRejection CandidateRejection, AccumulateAiUsefulnessOutcome? Outcome, RandomState RngState);

/// <summary>Source-order usefulness filtering for one certified ordinary Accumulate entry, outside candidate/origin scoring.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-229/231. No once-per-turn cache or command authority.</remarks>
public static class AccumulateAiUsefulness
{
    public static AccumulateAiUsefulnessResult Evaluate(AttackBattleSnapshot state, AccumulateAiCandidate candidate,
        AccumulateAiCandidateProfile profile, AccumulateAiUsefulnessProfile entry, RandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(entry);
        if (!entry.UsableRetailAlliedMapStatEntry)
            return new(false, AccumulateAiUsefulnessRejection.UnsupportedEntry, AccumulateAiCandidateRejection.None, null, decisionRng);
        var invalid = AccumulateAiCandidateProjection.Validate(state, candidate, profile);
        if (invalid != AccumulateAiCandidateRejection.None)
            return new(false, AccumulateAiUsefulnessRejection.InvalidCandidate, invalid, null, decisionRng);
        return FilterFresh(state, candidate.ActorId, entry, decisionRng);
    }

    // An owning caller supplies only freshly admitted state and the source entry's current mask.
    internal static AccumulateAiUsefulnessResult FilterFresh(AttackBattleSnapshot state, BattleUnitId actorId,
        AccumulateAiUsefulnessProfile entry, RandomState decisionRng)
    {
        var targets = ImmutableArray.CreateBuilder<AccumulateAiUsefulnessTarget>();
        bool selfUseful = false; var rng = decisionRng;
        foreach (var unit in state.TurnBattle.Turns.Units.OrderBy(u => u.TieKey))
        {
            bool actor = unit.Id == actorId;
            if (!(actor ? entry.ActorActive : entry.OpponentActive)) continue;
            if (!actor) { targets.Add(new(unit.Id, AccumulateAiEffect.Adverse, null)); continue; }
            uint raw;
            try { var draw = new RandomStateGenerator().Next(rng); raw = draw.Value; rng = draw.State; }
            catch (OverflowException)
            { return new(false, AccumulateAiUsefulnessRejection.RandomDrawCountExhausted, AccumulateAiCandidateRejection.None, null, decisionRng); }
            selfUseful = (raw & 1) == 0;
            targets.Add(new(unit.Id, selfUseful ? AccumulateAiEffect.Useful : AccumulateAiEffect.None, raw));
        }
        return new(true, AccumulateAiUsefulnessRejection.None, AccumulateAiCandidateRejection.None,
            new(targets.ToImmutable(), selfUseful), rng);
    }
}
