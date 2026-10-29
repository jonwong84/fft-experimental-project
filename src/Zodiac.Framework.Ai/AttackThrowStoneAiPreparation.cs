using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Both initialized human lists are ordinary Attack, learned Throw Stone, non-Frog Frog Attack, terminator.</summary>
/// <remarks>Also certifies no other effects/costs/list entries, no special configured behavior and a clean early actor-delay cache.</remarks>
public sealed record AttackThrowStoneAiPreparationProfile(bool BothInitializedAttackThrowStoneLists = false,
    bool NoSpecialBehavior = false, bool CleanActorDelayCache = false);
public enum AttackThrowStoneAiEntryKind { Attack, ThrowStone }
public sealed record AttackThrowStoneAiInitializedStatistics(int EntryCount, int SilenceQuarterShare, int MpQuarterShare,
    int EvadeQuarterShare, int HighestMpCost, int? LowestPaidMpCost);
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

/// <summary>Fresh both-unused NonBlue refresh and pair preparation, using original state after the private refresh clock.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-195/196.
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
            AttackCommandRejection ability = AttackCommandRejection.None, SwordAiRefreshRejection refresh = SwordAiRefreshRejection.None) =>
            new(false, new(reason, ability, refresh), null, decisionRng);
        if (!preparationProfile.BothInitializedAttackThrowStoneLists || !preparationProfile.NoSpecialBehavior || !preparationProfile.CleanActorDelayCache)
            return Reject(AttackThrowStoneAiPreparationRejection.UnsupportedProfile);
        if (team != SwordAiScenarioTeam.NonBlue) return Reject(AttackThrowStoneAiPreparationRejection.UnsupportedTeam);
        if (!AttackThrowStoneAiComposition.ProfilesMatch(attackProfile, throwStoneProfile))
            return Reject(AttackThrowStoneAiPreparationRejection.MismatchedProfiles);
        if (state.TurnBattle.Battle.TryGetUnit(request.ActorId, out var body) && body!.Move > 124)
            return Reject(AttackThrowStoneAiPreparationRejection.UnsupportedProfile);
        var actor = state.Combatants.FirstOrDefault(u => u.UnitId == request.ActorId);
        if (actor is null || weapon is null || actor.RightHandWeaponId != weapon.Id ||
            (2 * Math.Max(1, actor.PhysicalAttack) - 1) * weapon.Power > short.MaxValue)
            return Reject(AttackThrowStoneAiPreparationRejection.UnsupportedAttack);
        // Only pure command admission is queried. Original-tile range is not a preparation requirement.
        var ability = TargetedAbilityBattleSimulator.ValidateCommand(state,
            new(request.ActorId, throwStoneAbilityId, request.TargetId), throwStoneProfile.Ability);
        if (ability is not (AttackCommandRejection.None or AttackCommandRejection.OutOfRange))
            return Reject(AttackThrowStoneAiPreparationRejection.AbilityRejected, ability);
        if (!throwStoneProfile.Ability.Units.TryGetValue(request.TargetId, out var peer) || !peer.Abilities.Contains(throwStoneAbilityId))
            return Reject(AttackThrowStoneAiPreparationRejection.AbilityRejected, AttackCommandRejection.AbilityNotGranted);

        var refresh = SwordAiRefresh.EvaluateUnmoved(state, request, attackProfile, scoreProfile, refreshProfile, decisionRng);
        if (!refresh.Accepted) return Reject(AttackThrowStoneAiPreparationRejection.RefreshRejected, refresh: refresh.Rejection);
        var common = SwordAiPreparation.DeriveRestoredState(state, request, attackProfile, refresh.Outcome!, alreadyMoved: false);
        if (common is null) return Reject(AttackThrowStoneAiPreparationRejection.RestoredPolicyRejected);
        // Initialization includes unusable Frog Attack: two evadeable of three entries, no Silence or paid MP.
        var statistics = new AttackThrowStoneAiInitializedStatistics(3, 0, 0, 2, 0, null);
        var units = state.TurnBattle.Turns.Units.OrderBy(u => u.TieKey).Select(u =>
            new AttackThrowStoneAiPreparedUnit(u.Id, statistics, 2, 0, HasRangedAbility: true, CanHealAllies: false)).ToImmutableArray();
        // These are actor second-pass toggles only; do not invent a refreshed consume-aim policy for the peer.
        ImmutableArray<AttackThrowStoneAiPreparedEntry> entries = [
            new(AttackThrowStoneAiEntryKind.Attack, 1, 0, 0, 0, true, true, false, false),
            new(AttackThrowStoneAiEntryKind.ThrowStone, 4, 0, 0, 0, true, false, false, true)];
        return new(true, null, new(common, units, entries, EnemyHarmfulAbilityCount: 2, EnemyPhysicalAbilityCount: 2), refresh.RngState);
    }
}
