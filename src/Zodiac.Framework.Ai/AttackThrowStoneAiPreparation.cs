using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Both initialized human lists are ordinary Attack, learned Throw Stone, non-Frog Frog Attack, terminator.</summary>
/// <remarks>Also certifies no other effects/costs/list entries, no special configured behavior and a clean early actor-delay cache.</remarks>
public sealed record AttackThrowStoneAiPreparationProfile(bool BothInitializedAttackThrowStoneLists = false,
    bool NoSpecialBehavior = false, bool CleanActorDelayCache = false);
public enum AttackThrowStoneAiEntryKind { Attack, ThrowStone }
public sealed record AttackThrowStoneAiInitializedStatistics(int EntryCount, int SilenceQuarterShare, int MpQuarterShare,
    int EvadeQuarterShare, int HighestMpCost, int? LowestPaidMpCost);
/// <summary>First-pass counts/ranged facts before the actor-only action-taken usability clear.</summary>
public sealed record AttackThrowStoneAiPreparedUnit(BattleUnitId UnitId, AttackThrowStoneAiInitializedStatistics Initialized,
    int UsableHarmfulAbilities, int UsableHelpfulAbilities, bool HasRangedAbility, bool CanHealAllies);
public sealed record AttackThrowStoneAiPreparedEntry(AttackThrowStoneAiEntryKind Kind, int Range, int Area, int Ct, int MpCost,
    bool Usable, bool ExhaustiveOriginScan, bool ForceConsumeAim, bool ConsumeEvaluatedAimTiles);

/// <summary>Common restored-state diagnostics retain the legacy Sword type name; no Attack-only list assertion is used.</summary>
/// <remarks>No scored masks/D0/D1, candidate scan, plan or general-selection entry certificate is produced.</remarks>
public sealed record AttackThrowStoneAiPreparationOutcome(SwordAiPreparationOutcome Common,
    ImmutableArray<AttackThrowStoneAiPreparedUnit> Units, ImmutableArray<AttackThrowStoneAiPreparedEntry> ActorEntries,
    int EnemyHarmfulAbilityCount, int EnemyPhysicalAbilityCount);
public enum AttackThrowStoneAiPreparationRejection
{
    UnsupportedProfile, UnsupportedTeam, MismatchedProfiles, UnsupportedAttack, AbilityRejected, RefreshRejected, RestoredPolicyRejected
}
public sealed record AttackThrowStoneAiPreparationFailure(AttackThrowStoneAiPreparationRejection Reason,
    AttackCommandRejection AbilityRejection = AttackCommandRejection.None, SwordAiRefreshRejection RefreshRejection = SwordAiRefreshRejection.None);
/// <summary>Failure returns reasons only, no partial preparation, and original whole-query RNG.</summary>
public sealed record AttackThrowStoneAiPreparationResult(bool Accepted, AttackThrowStoneAiPreparationFailure? Failure,
    AttackThrowStoneAiPreparationOutcome? Outcome, FftRandomState RngState);

/// <summary>Fresh NonBlue pair preparation with explicit pre-ACT or post-ACT entry, using restored actual state.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-195/196/209/211.
/// No cached results, ranged D0/scenario derivation, candidate scoring/selection, authority or live RNG adoption.</remarks>
public static class AttackThrowStoneAiPreparation
{
    public static AttackThrowStoneAiPreparationResult Evaluate(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        AssetId throwStoneAbilityId, BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile,
        ThrowStoneAiCandidateProfile throwStoneProfile, AiScoreProfile scoreProfile, SwordAiRefreshProfile refreshProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, SwordAiScenarioTeam team, FftRandomState decisionRng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(attackProfile);
        ArgumentNullException.ThrowIfNull(throwStoneProfile); ArgumentNullException.ThrowIfNull(scoreProfile);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(preparationProfile);
        AttackThrowStoneAiPreparationResult Reject(AttackThrowStoneAiPreparationRejection reason,
            AttackCommandRejection ability = AttackCommandRejection.None) => new(false, new(reason, ability), null, decisionRng);
        var admission = ValidatePreparation(state, request, weapon, attackProfile, throwStoneProfile, preparationProfile, team);
        if (admission is not null) return new(false, admission, null, decisionRng);
        // Only pure command admission is queried. Original-tile range is not a preparation requirement.
        var ability = TargetedAbilityBattleSimulator.ValidateCommand(state,
            new(request.ActorId, throwStoneAbilityId, request.TargetId), throwStoneProfile.Ability);
        if (ability is not (AttackCommandRejection.None or AttackCommandRejection.OutOfRange))
            return Reject(AttackThrowStoneAiPreparationRejection.AbilityRejected, ability);
        if (!throwStoneProfile.Ability.Units.TryGetValue(request.TargetId, out var peer) || !peer.Abilities.Contains(throwStoneAbilityId))
            return Reject(AttackThrowStoneAiPreparationRejection.AbilityRejected, AttackCommandRejection.AbilityNotGranted);

        var refresh = SwordAiRefresh.EvaluateUnmoved(state, request, attackProfile, scoreProfile, refreshProfile, decisionRng);
        return CompletePreparation(state, request, attackProfile, refresh, decisionRng);
    }

    /// <summary>Own one fresh ACT-used/Move-unused refresh and pair preparation; no movement scoring or command authority.</summary>
    /// <remarks>Selection assertions are not consumed here. FreshRefreshEntry still certifies the earlier source no-progress guard.</remarks>
    public static AttackThrowStoneAiPreparationResult EvaluateAfterAct(AttackBattleSnapshot state,
        AttackThrowStoneAiSelectionRequest request, AttackThrowStoneAiSelectionConfiguration configuration, FftRandomState decisionRng)
    {
        AttackThrowStoneAiSelectionPipeline.ValidateInputs(state, request, configuration);
        var admission = ValidatePreparation(state, request.Turn, request.Weapon, configuration.Attack, configuration.ThrowStone,
            configuration.Preparation, configuration.Team);
        if (admission is not null) return new(false, admission, null, decisionRng);
        // Content/grants/setup are needed even though executing another ACT is unavailable.
        // Do not ignore ActionAlreadyTaken from ValidateCommand: it returns before these checks.
        var setup = TargetedAbilityBattleSimulator.ValidateAbilitySetup(state, request.Turn.ActorId,
            request.ThrowStoneAbilityId, configuration.ThrowStone.Ability);
        if (setup != AttackCommandRejection.None)
            return new(false, new(AttackThrowStoneAiPreparationRejection.AbilityRejected, setup), null, decisionRng);
        if (!configuration.ThrowStone.Ability.Units.TryGetValue(request.Turn.TargetId, out var peer) ||
            !peer.Abilities.Contains(request.ThrowStoneAbilityId))
            return new(false, new(AttackThrowStoneAiPreparationRejection.AbilityRejected, AttackCommandRejection.AbilityNotGranted), null, decisionRng);
        var refresh = SwordAiRefresh.EvaluateAfterAct(state, request.Turn, configuration.Attack, configuration.Scoring,
            configuration.Refresh, decisionRng);
        return CompletePreparation(state, request.Turn, configuration.Attack, refresh, decisionRng);
    }

    private static AttackThrowStoneAiPreparationFailure? ValidatePreparation(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        BoundedSwordWeapon? weapon, SwordAiCandidateProfile attackProfile, ThrowStoneAiCandidateProfile throwStoneProfile,
        AttackThrowStoneAiPreparationProfile preparationProfile, SwordAiScenarioTeam team)
    {
        if (!preparationProfile.BothInitializedAttackThrowStoneLists || !preparationProfile.NoSpecialBehavior || !preparationProfile.CleanActorDelayCache)
            return new(AttackThrowStoneAiPreparationRejection.UnsupportedProfile);
        if (team != SwordAiScenarioTeam.NonBlue) return new(AttackThrowStoneAiPreparationRejection.UnsupportedTeam);
        if (!AttackThrowStoneAiComposition.ProfilesMatch(attackProfile, throwStoneProfile))
            return new(AttackThrowStoneAiPreparationRejection.MismatchedProfiles);
        if (state.TurnBattle.Battle.TryGetUnit(request.ActorId, out var body) && body!.Move > 124)
            return new(AttackThrowStoneAiPreparationRejection.UnsupportedProfile);
        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == request.ActorId);
        if (actor is null || weapon is null || actor.RightHandWeaponId != weapon.Id ||
            (2 * Math.Max(1, actor.PhysicalAttack) - 1) * weapon.Power > short.MaxValue)
            return new(AttackThrowStoneAiPreparationRejection.UnsupportedAttack);
        return null;
    }

    private static AttackThrowStoneAiPreparationResult CompletePreparation(AttackBattleSnapshot state, SwordAiRefreshRequest request,
        SwordAiCandidateProfile attackProfile, SwordAiRefreshResult refresh, FftRandomState decisionRng)
    {
        if (!refresh.Accepted)
            return new(false, new(AttackThrowStoneAiPreparationRejection.RefreshRejected, RefreshRejection: refresh.Rejection), null, decisionRng);
        var common = SwordAiPreparation.DeriveRestoredState(state, request, attackProfile, refresh.Outcome!, alreadyMoved: false);
        if (common is null) return new(false, new(AttackThrowStoneAiPreparationRejection.RestoredPolicyRejected), null, decisionRng);
        // Initialization includes unusable Frog Attack: two evadeable of three entries, no Silence or paid MP.
        var statistics = new AttackThrowStoneAiInitializedStatistics(3, 0, 0, 2, 0, null);
        var units = state.TurnBattle.Turns.Units.OrderBy(u => u.TieKey).Select(u =>
            new AttackThrowStoneAiPreparedUnit(u.Id, statistics, 2, 0, HasRangedAbility: true, CanHealAllies: false)).ToImmutableArray();
        // These are actor second-pass toggles only; do not invent a refreshed consume-aim policy for the peer.
        bool usable = common.ActorAttackUsable; // Both ordinary entries share the actor's action-taken clear, after reach/counts.
        ImmutableArray<AttackThrowStoneAiPreparedEntry> entries = [
            new(AttackThrowStoneAiEntryKind.Attack, 1, 0, 0, 0, usable, true, false, false),
            new(AttackThrowStoneAiEntryKind.ThrowStone, 4, 0, 0, 0, usable, false, false, true)];
        return new(true, null, new(common, units, entries, EnemyHarmfulAbilityCount: 2, EnemyPhysicalAbilityCount: 2), refresh.RngState);
    }
}
