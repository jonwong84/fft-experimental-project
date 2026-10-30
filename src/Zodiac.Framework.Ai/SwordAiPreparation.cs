using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Source facts absent from live state, required before deriving the closed NonBlue preparation.</summary>
/// <remarks>
/// Both initialized human lists contain only range-one ordinary sword Attack, unusable Frog Attack and a terminator.
/// Weapon summaries have no extra effects, trajectory, area, MP, CT or random-use behavior.
/// CleanActorDelayCache certifies no stale cached DeadWithoutReraise bit before refresh captured the actor delay.
/// NoSpecialBehavior excludes configured special decision behavior, not the supported Critical HP status.
/// </remarks>
public sealed record SwordAiPreparationProfile(bool OrdinaryAttackOnlyLists = false,
    bool NoSpecialBehavior = false, bool CleanActorDelayCache = false);
public enum SwordAiPreparationRejection { None, UnsupportedProfile, UnsupportedTeam, RefreshRejected }

/// <summary>Fresh refresh diagnostics followed by restored-state ordinary policy preparation.</summary>
/// <remarks>Policies are post-team-update; Refresh retains its earlier pre-team-update policy diagnostics.</remarks>
public sealed record SwordAiPreparationOutcome(SwordAiRefreshOutcome Refresh,
    ImmutableArray<SwordAiRefreshPolicy> Policies, TilePosition Origin, TilePosition FocusPosition,
    int ActorDelay, int TargetDelay, int EnemyAbilityReach, int AllyAbilityReach, byte FoeProximity,
    bool RetreatSupportAvailable, bool SingleEnemyRemaining, bool TargetActive,
    bool ManualAutobattle, int RandomUseThreshold)
{
    /// <summary>Owned restored-state origins for unmoved entries. The legacy already-moved entry leaves this empty.</summary>
    public ImmutableArray<TilePosition> ReachableOrigins { get; init; } = [];
    /// <summary>Ordinary Attack usability after preparation; post-ACT clears it after computing reach.</summary>
    public bool ActorAttackUsable { get; init; } = true;
}

/// <summary>Failure returns original RNG with no partial refresh/preparation output.</summary>
public sealed record SwordAiPreparationResult(bool Accepted, SwordAiPreparationRejection Rejection,
    SwordAiRefreshRejection RefreshRejection, SwordAiPreparationOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh pure refresh and ordinary NonBlue preparation, without selection or command authority.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-160/161/171/172/176/179. Preparation adds no draws after refresh.</remarks>
public static class SwordAiPreparation
{
    public static SwordAiPreparationResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team, decisionRng, alreadyMoved: true);

    /// <summary>Own fresh both-unused refresh and full ordinary reachability, then derive policy on restored live state.</summary>
    public static SwordAiPreparationResult EvaluateUnmoved(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team, decisionRng, alreadyMoved: false);

    /// <summary>Own fresh post-ACT refresh and derive reachability/policy from actual restored state, with Attack unusable.</summary>
    public static SwordAiPreparationResult EvaluateAfterAct(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team, FftRandomState decisionRng)
        => EvaluateCore(state, request, candidateProfile, scoreProfile, refreshProfile, preparationProfile, team, decisionRng,
            alreadyMoved: false, alreadyActed: true);

    private static SwordAiPreparationResult EvaluateCore(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        SwordAiPreparationProfile preparationProfile, SwordAiScenarioTeam team, FftRandomState decisionRng,
        bool alreadyMoved, bool alreadyActed = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateProfile);
        ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile);
        ArgumentNullException.ThrowIfNull(preparationProfile);
        SwordAiPreparationResult Reject(SwordAiPreparationRejection reason,
            SwordAiRefreshRejection refreshReason = SwordAiRefreshRejection.None) => new(false, reason, refreshReason, null, decisionRng);
        if (!preparationProfile.OrdinaryAttackOnlyLists || !preparationProfile.NoSpecialBehavior || !preparationProfile.CleanActorDelayCache)
            return Reject(SwordAiPreparationRejection.UnsupportedProfile);
        if (team != SwordAiScenarioTeam.NonBlue) return Reject(SwordAiPreparationRejection.UnsupportedTeam);
        // The subsequent scenario query uses raw Move+1; avoid the original byte reach's wrap/capped-spread mismatch.
        if (state.TurnBattle.Battle.TryGetUnit(request.ActorId, out var actor) && actor!.Move > 124)
            return Reject(SwordAiPreparationRejection.UnsupportedProfile);

        var refresh = alreadyActed
            ? SwordAiRefresh.EvaluateAfterAct(state, request, candidateProfile, scoreProfile, refreshProfile, decisionRng)
            : alreadyMoved
            ? SwordAiRefresh.Evaluate(state, request, candidateProfile, scoreProfile, refreshProfile, decisionRng)
            : SwordAiRefresh.EvaluateUnmoved(state, request, candidateProfile, scoreProfile, refreshProfile, decisionRng);
        if (!refresh.Accepted) return Reject(SwordAiPreparationRejection.RefreshRejected, refresh.Rejection);
        var restored = DeriveRestoredState(state, request, candidateProfile, refresh.Outcome!, alreadyMoved);
        if (restored is null) return Reject(SwordAiPreparationRejection.UnsupportedProfile);
        return new(true, SwordAiPreparationRejection.None, SwordAiRefreshRejection.None, restored, refresh.RngState);
    }

    // Shared by fresh owning queries whose minimum enemy ability range is one. No list assertion is manufactured here.
    internal static SwordAiPreparationOutcome? DeriveRestoredState(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile candidateProfile, SwordAiRefreshOutcome refresh, bool alreadyMoved)
    {
        state.TurnBattle.Battle.TryGetUnit(request.ActorId, out var actor);
        ImmutableArray<TilePosition> origins = [];
        if (!alreadyMoved)
        {
            // TurnBattleState enforces HasMoved == MovementTaken. Never use refresh's hypothetical clock units.
            var geometry = FlatOrdinaryMovement.Evaluate(state, request.ActorId, candidateProfile.Movement);
            if (!geometry.Accepted) return null;
            origins = geometry.Outcome!.AiOrigins;
        }
        var turns = state.TurnBattle.Turns.Units;
        static int Delay(TurnUnitState unit) => (Math.Max(0, 100 - unit.Ct) + unit.Speed - 1) / unit.Speed;
        var actorTurn = turns.Single(u => u.Id == request.ActorId);
        int actorDelay = Delay(actorTurn);
        int targetDelay = Delay(turns.Single(u => u.Id == request.TargetId));
        var battle = state.TurnBattle.Battle;
        battle.TryGetUnit(request.TargetId, out var target);
        var origin = actor!.Position;
        var focus = target!.Position;
        int distance = Math.Abs(focus.X - origin.X) + Math.Abs(focus.Y - origin.Y);
        byte proximity = targetDelay <= actorDelay ? (byte)Math.Max(0, 10 - distance) : (byte)0;
        // One ally cannot provide retreat support. NonBlue sets the actor's policy unless already set.
        var policies = refresh.RefreshedUnits.Select(u => new SwordAiRefreshPolicy(u.UnitId,
            u.ConserveCt, u.UnitId == request.ActorId || u.TeamSupportUnavailable)).ToImmutableArray();
        return new(refresh, policies, origin, focus, actorDelay, targetDelay, actor.Move + 1, actor.Move,
            proximity, RetreatSupportAvailable: false, SingleEnemyRemaining: false, TargetActive: true,
            ManualAutobattle: true, RandomUseThreshold: 0)
            { ReachableOrigins = origins, ActorAttackUsable = !actorTurn.ActionTaken };
    }
}
