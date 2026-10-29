using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Known cached policy values, retained by refresh rather than inferred from unit IDs.</summary>
public sealed record SwordAiRefreshPolicy(BattleUnitId UnitId, bool ConserveCt, bool TeamSupportUnavailable);
public sealed record SwordAiRefreshRequest(BattleUnitId ActorId, BattleUnitId TargetId,
    ImmutableArray<SwordAiRefreshPolicy> Policies);

/// <summary>Unrepresented source entry/cache facts require explicit certification.</summary>
public sealed record SwordAiRefreshProfile(bool FreshRefreshEntry = false, bool BothUnitsTargetable = false,
    bool ZeroCachedMpAbilityModifiers = false);

public enum SwordAiRefreshRejection
{
    None, UnsupportedRefreshProfile, UnsupportedScoreProfile, UnknownActor, UnknownTarget,
    InvalidPolicies, TerminalInput, UnavailableAction, UnsupportedProfile,
    RandomDrawCountExhausted, TickCountExhausted
}

/// <summary>Only refresh-owned facts and the two retained policies; other source decision flags are outside this record.</summary>
public sealed record SwordAiRefreshedUnit(BattleUnitId UnitId, bool Enemy, bool Considered,
    bool TransientDead, bool TransientPetrified, bool TransientJump,
    bool HpBelowHalf, bool HpBelowThreeQuarters, bool MpConstrained, bool Untargetable,
    bool DeadWithoutReraise, bool DeadWithReraise, bool ConserveCt, bool TeamSupportUnavailable);

/// <summary>Hypothetical diagnostics, not an executable state, score or general-selection entry certificate.</summary>
public sealed record SwordAiRefreshOutcome(BattleUnitId ActorId, BattleUnitId TargetId,
    ImmutableArray<SwordAiRefreshedUnit> RefreshedUnits, int TurnEndCt,
    ImmutableArray<SwordAiProjectedUnit> Units, BattleUnitId MoveOpportunityUnitId,
    ulong ElapsedTicks, ulong Ticks, ImmutableArray<SwordAiUpkeepPick> UpkeepPicks);

/// <summary>Rejected queries return the incoming RNG and no partial flags or clock result.</summary>
public sealed record SwordAiRefreshResult(bool Accepted, SwordAiRefreshRejection Rejection,
    SwordAiRefreshOutcome? Outcome, FftRandomState RngState);

/// <summary>Pure fresh refresh for two living ordinary opponents, with explicit turn-choice entry modes.</summary>
/// <remarks>
/// Source: decomp 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-156/159/171/172/176/178.
/// Act-first/skillset-zero skips movement and action simulation. One private clock call consumes one upkeep draw.
/// No command commitment, scoring, earlier delay-cache derivation or later ability/team policy initialization.
/// </remarks>
public static class SwordAiRefresh
{
    public static SwordAiRefreshResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, decisionRng, alreadyMoved: true);

    /// <summary>Fresh refresh with Move and ACT unused; the hypothetical turn refunds both choices before capping CT.</summary>
    public static SwordAiRefreshResult EvaluateUnmoved(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, decisionRng, alreadyMoved: false);

    /// <summary>Fresh admitted post-ACT refresh with Move unused; only the unused-Move refund applies.</summary>
    /// <remarks>ACT used alone does not establish FreshRefreshEntry or prove the source no-progress guard passed.</remarks>
    public static SwordAiRefreshResult EvaluateAfterAct(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, decisionRng, alreadyMoved: false, alreadyActed: true);

    private static SwordAiRefreshResult EvaluateCore(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiScoreProfile scoreProfile,
        SwordAiRefreshProfile refreshProfile, FftRandomState decisionRng, bool alreadyMoved, bool alreadyActed = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile);
        SwordAiRefreshResult Reject(SwordAiRefreshRejection reason) => new(false, reason, null, decisionRng);
        if (!refreshProfile.FreshRefreshEntry || !refreshProfile.BothUnitsTargetable || !refreshProfile.ZeroCachedMpAbilityModifiers)
            return Reject(SwordAiRefreshRejection.UnsupportedRefreshProfile);
        if (!scoreProfile.Supports(state, candidateProfile))
            return Reject(SwordAiRefreshRejection.UnsupportedScoreProfile);
        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == request.ActorId);
        if (actor is null) return Reject(SwordAiRefreshRejection.UnknownActor);
        var target = state.Combatants.FirstOrDefault(u => u.UnitId == request.TargetId);
        if (target is null) return Reject(SwordAiRefreshRejection.UnknownTarget);
        if (actor.UnitId == target.UnitId) return Reject(SwordAiRefreshRejection.UnsupportedProfile);
        if (request.Policies.IsDefault || request.Policies.Length != 2 || request.Policies.Any(p => p is null) ||
            !request.Policies.Select(p => p.UnitId).ToHashSet().SetEquals([actor.UnitId, target.UnitId]))
            return Reject(SwordAiRefreshRejection.InvalidPolicies);
        if (state.Combatants.Any(u => u.Hp == 0)) return Reject(SwordAiRefreshRejection.TerminalInput);
        var turns = state.TurnBattle.Turns;
        var actorTurn = turns.Units.Single(u => u.Id == actor.UnitId);
        if (turns.Phase != TurnCyclePhase.UnitTurn || turns.ActiveUnitId != actor.UnitId ||
            actorTurn.MovementTaken != alreadyMoved || actorTurn.ActionTaken != alreadyActed)
            return Reject(SwordAiRefreshRejection.UnavailableAction);
        try { candidateProfile.Sword.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        { return Reject(SwordAiRefreshRejection.UnsupportedProfile); }
        if (!candidateProfile.Movement.Supports(state)) return Reject(SwordAiRefreshRejection.UnsupportedProfile);

        // Source turn_end adds each unused-choice refund in u16, then caps before storing byte CT.
        int turnEndCt = Math.Min(60, actorTurn.Ct + (actorTurn.ActionTaken ? 0 : 20) + (actorTurn.MovementTaken ? 0 : 20));
        var battle = state.TurnBattle.Battle;
        var units = turns.Units.OrderBy(u => u.TieKey).Select(turn =>
        {
            var combat = state.Combatants.Single(u => u.UnitId == turn.Id);
            battle.TryGetUnit(turn.Id, out var body);
            return new SwordAiProjectedUnit(turn.Id, body!.Position, combat.Facing,
                combat.Hp, combat.MaxHp, combat.Status, candidateProfile.Units.Single(u => u.UnitId == turn.Id).DeathCounter,
                turn.Speed, turn.TieKey, turn.Id == actor.UnitId ? turnEndCt : turn.Ct,
                turn.MovementTaken, turn.ActionTaken);
        }).ToArray();
        var rejection = SwordAiSimulationClock.Advance(units, turns.Ticks, decisionRng, out var clock);
        if (rejection != SwordAiCandidateRejection.None)
            return Reject(rejection == SwordAiCandidateRejection.RandomDrawCountExhausted
                ? SwordAiRefreshRejection.RandomDrawCountExhausted : SwordAiRefreshRejection.TickCountExhausted);

        // Source rebuilds HP facts before simulation and transient status/Considered after it.
        // In this effect-free living profile HP/status cannot change; do not copy unrelated cached flags.
        var flags = units.Select(unit =>
        {
            var policy = request.Policies.Single(p => p.UnitId == unit.UnitId);
            return new SwordAiRefreshedUnit(unit.UnitId, Enemy: unit.UnitId == target.UnitId, Considered: true,
                TransientDead: false, TransientPetrified: false, TransientJump: false,
                HpBelowHalf: unit.Hp < unit.MaxHp / 2, HpBelowThreeQuarters: unit.Hp * 4 < unit.MaxHp * 3,
                MpConstrained: false, Untargetable: false, DeadWithoutReraise: false, DeadWithReraise: false,
                ConserveCt: policy.ConserveCt, TeamSupportUnavailable: policy.TeamSupportUnavailable);
        }).ToImmutableArray();
        return new(true, SwordAiRefreshRejection.None,
            new(actor.UnitId, target.UnitId, flags, turnEndCt, clock!.Units, clock.MoveOpportunityUnitId,
                clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks), clock.RngState);
    }
}
