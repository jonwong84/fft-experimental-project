using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Both source lists are Attack, learned Accumulate, learned Throw Stone, non-Frog Frog Attack, terminator.</summary>
/// <remarks>Certifies ordinary entry flags with no retained Only/Monster Skill restrictions, no extra costs/effects,
/// no special configured behavior and a clean actor-delay cache. BlueAiControlled certifies PLAYER_CONTROLLED clear.</remarks>
public sealed record AccumulateAiPreparationProfile(bool BothInitializedLists = false, bool NoSpecialBehavior = false,
    bool CleanActorDelayCache = false, bool BlueAiControlled = false);
public sealed record AccumulateAiPreparationRequest(SwordAiRefreshRequest Turn, AssetId AbilityId,
    AssetId ThrowStoneAbilityId, BoundedSwordWeapon? Weapon);
public sealed record AccumulateAiPreparationConfiguration(SwordAiCandidateProfile Attack, ThrowStoneAiCandidateProfile ThrowStone,
    AccumulateAiCandidateProfile Accumulate, AiScoreProfile Scoring, SwordAiRefreshProfile Refresh,
    AccumulateAiPreparationProfile Preparation, SwordAiScenarioTeam Team)
{
    /// <summary>Optional owned content-backed lists. When present they replace, and cannot be bypassed by, BothInitializedLists.</summary>
    public AiHumanAbilityLists? AbilityLists { get; init; }
}
public enum AccumulateAiEntryKind { Attack, Accumulate, ThrowStone }
public sealed record AccumulateAiPreparedEntry(AccumulateAiEntryKind Kind, int Range, bool Usable,
    bool ExhaustiveOriginScan, bool ForceConsumeAim, bool ConsumeEvaluatedAimTiles);
public sealed record AccumulateAiPreparedUnit(BattleUnitId UnitId, int InitializedEntryCount, int EvadeQuarterShare,
    int SilenceQuarterShare, int MpQuarterShare, int HarmfulCount, int HelpfulCount, bool HasRangedAbility,
    bool CanHealAllies, bool? MostlySupport);
public sealed record AccumulateAiPreparationOutcome(SwordAiPreparationOutcome Common, SwordAiScenarioTeam Team,
    ImmutableArray<AccumulateAiPreparedUnit> Units, ImmutableArray<AccumulateAiPreparedEntry> ActorEntries,
    int EnemyHarmfulCount, int EnemyPhysicalCount);
public enum AccumulateAiPreparationRejection { None, UnsupportedProfile, UnsupportedTeam, MismatchedProfiles,
    UnsupportedAttack, AbilityRejected, RefreshRejected, UnsupportedList }
public sealed record AccumulateAiPreparationFailure(AccumulateAiPreparationRejection Reason,
    AccumulateAiCandidateRejection AccumulateReason = AccumulateAiCandidateRejection.None,
    AttackCommandRejection ThrowStoneReason = AttackCommandRejection.None,
    SwordAiRefreshRejection RefreshReason = SwordAiRefreshRejection.None);
public sealed record AccumulateAiPreparationResult(bool Accepted, AccumulateAiPreparationFailure? Failure,
    AccumulateAiPreparationOutcome? Outcome, RandomState RngState);

/// <summary>Fresh closed-list preparation before or after ACT; no usefulness coin or source-scenario/command authority.</summary>
/// <remarks>Source 11464943bbdbd2a0af022d90cb7f10455cf911df; ZOD-229/231/243/244.</remarks>
public static class AccumulateAiPreparation
{
    public static AccumulateAiPreparationResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration, RandomState decisionRng)
        => EvaluateCore(state, request, configuration, decisionRng, alreadyActed: false);

    /// <summary>Fresh ACT-used/Move-unused preparation from actual PA/HP/positions; all actor actions become unusable.</summary>
    /// <remarks>FreshRefreshEntry certifies the earlier no-progress guard. Content/grants remain validated even with ACT spent.</remarks>
    public static AccumulateAiPreparationResult EvaluateAfterAct(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration, RandomState decisionRng)
        => EvaluateCore(state, request, configuration, decisionRng, alreadyActed: true);

    private static AccumulateAiPreparationResult EvaluateCore(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration, RandomState decisionRng, bool alreadyActed)
    {
        ValidateArguments(state, request, configuration);
        AccumulateAiPreparationResult Reject(AccumulateAiPreparationFailure failure) => new(false, failure, null, decisionRng);
        var p = configuration.Preparation;
        if ((configuration.AbilityLists is null && !p.BothInitializedLists) || !p.NoSpecialBehavior || !p.CleanActorDelayCache)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedProfile));
        if (configuration.AbilityLists is { } lists && !ListsMatch(state, request, configuration, lists))
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedList));
        var team = configuration.Team;
        if (team is not (SwordAiScenarioTeam.NonBlue or SwordAiScenarioTeam.Blue) || team == SwordAiScenarioTeam.Blue && !p.BlueAiControlled)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedTeam));
        var attack = configuration.Attack; var self = configuration.Accumulate;
        if (request.AbilityId == request.ThrowStoneAbilityId ||
            !AttackThrowStoneAiComposition.ProfilesMatch(attack, configuration.ThrowStone) ||
            attack.Movement.Ground != self.Movement.Ground ||
            !attack.Units.OrderBy(u => u.UnitId.Value).SequenceEqual(self.Units.OrderBy(u => u.UnitId.Value)))
            return Reject(new(AccumulateAiPreparationRejection.MismatchedProfiles));
        var turn = request.Turn;
        if (!state.TurnBattle.Battle.TryGetUnit(turn.ActorId, out var body))
            return Reject(new(AccumulateAiPreparationRejection.AbilityRejected, AccumulateAiCandidateRejection.UnknownActor));
        var invalid = AccumulateAiCandidateProjection.Validate(state,
            new(turn.ActorId, request.AbilityId, turn.TargetId, body!.Position, true), self, alreadyActed);
        if (invalid != AccumulateAiCandidateRejection.None) return Reject(new(AccumulateAiPreparationRejection.AbilityRejected, invalid));
        if (!self.Ability.Units[turn.TargetId].Abilities.Contains(request.AbilityId))
            return Reject(new(AccumulateAiPreparationRejection.AbilityRejected, AccumulateAiCandidateRejection.AbilityNotGranted));
        var actor = state.Combatants.Single(u => u.UnitId == turn.ActorId);
        if (body.Move > 124 || team == SwordAiScenarioTeam.Blue && actor.Hp * 128 / actor.MaxHp < 45)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedProfile));
        // The reachable PA envelope, rather than initial PA, protects later battle boosts.
        if (request.Weapon is not { } weapon || actor.RightHandWeaponId != weapon.Id || 197 * weapon.Power > short.MaxValue)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedAttack));
        var stone = configuration.ThrowStone.Ability;
        var ability = alreadyActed
            ? TargetedAbilityBattleSimulator.ValidateAbilitySetup(state, turn.ActorId, request.ThrowStoneAbilityId, stone)
            : TargetedAbilityBattleSimulator.ValidateCommand(state, new(turn.ActorId, request.ThrowStoneAbilityId, turn.TargetId), stone);
        if (ability is not (AttackCommandRejection.None or AttackCommandRejection.OutOfRange))
            return Reject(new(AccumulateAiPreparationRejection.AbilityRejected, ThrowStoneReason: ability));
        if (!stone.Units.TryGetValue(turn.TargetId, out var peer) || !peer.Abilities.Contains(request.ThrowStoneAbilityId))
            return Reject(new(AccumulateAiPreparationRejection.AbilityRejected, ThrowStoneReason: AttackCommandRejection.AbilityNotGranted));
        var refresh = alreadyActed
            ? SwordAiRefresh.EvaluateAfterAct(state, turn, attack, configuration.Scoring, configuration.Refresh, decisionRng)
            : SwordAiRefresh.EvaluateUnmoved(state, turn, attack, configuration.Scoring, configuration.Refresh, decisionRng);
        if (!refresh.Accepted) return Reject(new(AccumulateAiPreparationRejection.RefreshRejected, RefreshReason: refresh.Rejection));
        var common = SwordAiPreparation.DeriveRestoredState(state, turn, attack, refresh.Outcome!, alreadyMoved: false, team);
        if (common is null) return Reject(new(AccumulateAiPreparationRejection.UnsupportedProfile));
        // Four initialized entries, including unusable Frog Attack. Accumulate only changes the denominator/helpful count.
        var units = state.TurnBattle.Turns.Units.OrderBy(u => u.TieKey).Select(u =>
            // Source updates helpful-share policy for allies only. The peer's retained bit is unrepresented.
            configuration.AbilityLists is { } supplied ? Summary(u.Id, supplied.Units[u.Id], turn.ActorId) :
            new AccumulateAiPreparedUnit(u.Id, 4, 2, 0, 0, 2, 1, true, false, u.Id == turn.ActorId ? false : null)).ToImmutableArray();
        ImmutableArray<AccumulateAiPreparedEntry> entries = configuration.AbilityLists is { } owned
            ? owned.Units[turn.ActorId].Entries.Where(e => e.OrdinaryUsable).Select(e => new AccumulateAiPreparedEntry(
                e.Kind == AiHumanAbilityKind.Attack ? AccumulateAiEntryKind.Attack : e.Kind == AiHumanAbilityKind.Accumulate ? AccumulateAiEntryKind.Accumulate : AccumulateAiEntryKind.ThrowStone,
                e.Range, !alreadyActed, e.ExhaustiveOriginScan, e.ForceConsumeAim, e.Kind != AiHumanAbilityKind.Attack)).ToImmutableArray() : [
            new(AccumulateAiEntryKind.Attack, 1, !alreadyActed, true, false, false),
            new(AccumulateAiEntryKind.Accumulate, 0, !alreadyActed, false, true, true),
            new(AccumulateAiEntryKind.ThrowStone, 4, !alreadyActed, false, false, true)];
        if (configuration.AbilityLists is not null)
        {
            // Reach is derived before ACT-used clears actor usability, just as the source's second pass does.
            var source = configuration.AbilityLists.Units[turn.ActorId].Entries;
            common = common with { EnemyAbilityReach = body.Move + source.Where(e => e.Harmful).Min(e => e.Range),
                AllyAbilityReach = body.Move + source.Where(e => e.Helpful).Min(e => e.Range) };
        }
        int harmful = units.Single(u => u.UnitId == turn.TargetId).HarmfulCount;
        return new(true, null, new(common, team, units, entries, harmful, harmful), refresh.RngState);
    }

    private static AccumulateAiPreparedUnit Summary(BattleUnitId id, AiHumanInitializedList list, BattleUnitId actorId) =>
        new(id, list.InitializedEntryCount, list.EvadeQuarterShare, list.SilenceQuarterShare, list.MpQuarterShare,
            list.HarmfulCount, list.HelpfulCount, list.HasRangedAbility, false,
            id == actorId ? list.HelpfulCount * 4 / (list.HelpfulCount + list.HarmfulCount) >= 2 : null);

    private static bool ListsMatch(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration, AiHumanAbilityLists lists)
    {
        if (lists.Units.Count != state.Combatants.Length) return false;
        foreach (var unit in state.Combatants)
        {
            if (!lists.Units.TryGetValue(unit.UnitId, out var list) ||
                !list.Entries.Select(e => e.Kind).SequenceEqual(new[] { AiHumanAbilityKind.Attack, AiHumanAbilityKind.Accumulate, AiHumanAbilityKind.ThrowStone, AiHumanAbilityKind.FrogAttack }) ||
                list.Entries[1].AbilityId != request.AbilityId || list.Entries[2].AbilityId != request.ThrowStoneAbilityId ||
                !configuration.Accumulate.Ability.Units.TryGetValue(unit.UnitId, out var self) || !self.Abilities.SetEquals([request.AbilityId]) ||
                !configuration.ThrowStone.Ability.Units.TryGetValue(unit.UnitId, out var stone) || !stone.Abilities.SetEquals([request.ThrowStoneAbilityId])) return false;
        }
        return true;
    }

    internal static void ValidateArguments(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Turn);
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(configuration.Attack);
        ArgumentNullException.ThrowIfNull(configuration.ThrowStone); ArgumentNullException.ThrowIfNull(configuration.Accumulate);
        ArgumentNullException.ThrowIfNull(configuration.Scoring); ArgumentNullException.ThrowIfNull(configuration.Refresh);
        ArgumentNullException.ThrowIfNull(configuration.Preparation);
    }
}
