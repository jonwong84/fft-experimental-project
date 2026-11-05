using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record BasicSkillAiPreparedOccurrence(int Index, AiHumanInitializedEntry Source, bool Usable, bool ConsumeAim);
public sealed record BasicSkillAiListUnit(BattleUnitId UnitId, AiHumanInitializedList Initialization,
    int HelpfulShare, bool? MostlySupport);
public sealed record BasicSkillAiListPreparationOutcome(SwordAiPreparationOutcome Common, SwordAiScenarioTeam Team,
    ImmutableArray<BasicSkillAiListUnit> Units, ImmutableArray<BasicSkillAiPreparedOccurrence> ActorEntries,
    int EnemyHarmfulCount, int EnemyPhysicalCount);
public sealed record BasicSkillAiListPreparationResult(bool Accepted, AccumulateAiPreparationFailure? Failure,
    BasicSkillAiListPreparationOutcome? Outcome, RandomState RngState);

/// <summary>Fresh preparation for owned ordinary lists, preserving every supported occurrence and absent-action distinctions.</summary>
/// <remarks>ZOD-264/265. No movement scoring, action search or canonical-list admission bypass.</remarks>
public static class BasicSkillAiListPreparation
{
    public static BasicSkillAiListPreparationResult Evaluate(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration, RandomState decisionRng) => EvaluateCore(state, request, configuration, decisionRng, false);
    public static BasicSkillAiListPreparationResult EvaluateAfterAct(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration configuration, RandomState decisionRng) => EvaluateCore(state, request, configuration, decisionRng, true);

    private static BasicSkillAiListPreparationResult EvaluateCore(AttackBattleSnapshot state, AccumulateAiPreparationRequest request,
        AccumulateAiPreparationConfiguration p, RandomState rng, bool afterAct)
    {
        AccumulateAiPreparation.ValidateArguments(state, request, p);
        BasicSkillAiListPreparationResult Reject(AccumulateAiPreparationFailure reason) => new(false, reason, null, rng);
        if (!p.Preparation.NoSpecialBehavior || !p.Preparation.CleanActorDelayCache)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedProfile));
        if (p.Team is not (SwordAiScenarioTeam.NonBlue or SwordAiScenarioTeam.Blue) || p.Team == SwordAiScenarioTeam.Blue && !p.Preparation.BlueAiControlled)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedTeam));
        if (!AttackThrowStoneAiComposition.ProfilesMatch(p.Attack, p.ThrowStone) || p.Attack.Movement.Ground != p.Accumulate.Movement.Ground ||
            !p.Attack.Units.OrderBy(u => u.UnitId.Value).SequenceEqual(p.Accumulate.Units.OrderBy(u => u.UnitId.Value)))
            return Reject(new(AccumulateAiPreparationRejection.MismatchedProfiles));
        var turn = request.Turn;
        var invalid = AccumulateAiCandidateProjection.ValidateState(state, turn.ActorId, turn.TargetId, p.Accumulate, afterAct);
        if (invalid != AccumulateAiCandidateRejection.None) return Reject(new(AccumulateAiPreparationRejection.AbilityRejected, invalid));
        var actor = state.Combatants.Single(u => u.UnitId == turn.ActorId);
        var body = state.TurnBattle.Battle.Units.Single(u => u.Id == actor.UnitId);
        if (body.Move > 124 || p.Team == SwordAiScenarioTeam.Blue && actor.Hp * 128 / actor.MaxHp < 45)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedProfile));
        if (request.Weapon is not { } weapon || actor.RightHandWeaponId != weapon.Id || 197 * weapon.Power > short.MaxValue)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedAttack));
        var lists = p.AbilityLists;
        if (lists is null || request.AbilityId == request.ThrowStoneAbilityId || lists.Units.Count != state.Combatants.Length)
            return Reject(new(AccumulateAiPreparationRejection.UnsupportedList));
        foreach (var unit in state.Combatants)
        {
            if (!lists.Units.TryGetValue(unit.UnitId, out var list) || list.Entries.Length is < 2 or > 34 ||
                list.Entries[0].Kind != AiHumanAbilityKind.Attack || list.Entries[^1].Kind != AiHumanAbilityKind.FrogAttack ||
                list.Entries.Skip(1).SkipLast(1).Any(e => e.Kind is not (AiHumanAbilityKind.Accumulate or AiHumanAbilityKind.ThrowStone)))
                return Reject(new(AccumulateAiPreparationRejection.UnsupportedList));
            foreach (var entry in list.Entries)
            {
                if (entry.Kind == AiHumanAbilityKind.Accumulate &&
                    (entry.AbilityId != request.AbilityId || !p.Accumulate.Ability.Abilities.ContainsKey(request.AbilityId) ||
                     !p.Accumulate.Ability.Units[unit.UnitId].Abilities.Contains(request.AbilityId)))
                    return Reject(new(AccumulateAiPreparationRejection.AbilityRejected, AccumulateAiCandidateRejection.AbilityNotGranted));
                if (entry.Kind == AiHumanAbilityKind.ThrowStone)
                {
                    if (entry.AbilityId != request.ThrowStoneAbilityId) return Reject(new(AccumulateAiPreparationRejection.UnsupportedList));
                    var stone = TargetedAbilityBattleSimulator.ValidateAbilitySetup(state, unit.UnitId, request.ThrowStoneAbilityId, p.ThrowStone.Ability);
                    if (stone != AttackCommandRejection.None) return Reject(new(AccumulateAiPreparationRejection.AbilityRejected, ThrowStoneReason: stone));
                }
            }
        }
        var refresh = afterAct ? SwordAiRefresh.EvaluateAfterAct(state, turn, p.Attack, p.Scoring, p.Refresh, rng)
            : SwordAiRefresh.EvaluateUnmoved(state, turn, p.Attack, p.Scoring, p.Refresh, rng);
        if (!refresh.Accepted) return Reject(new(AccumulateAiPreparationRejection.RefreshRejected, RefreshReason: refresh.Rejection));
        var common = SwordAiPreparation.DeriveRestoredState(state, turn, p.Attack, refresh.Outcome!, false, p.Team);
        if (common is null) return Reject(new(AccumulateAiPreparationRejection.UnsupportedProfile));
        var actorList = lists.Units[actor.UnitId];
        var units = state.TurnBattle.Turns.Units.OrderBy(u => u.TieKey).Select(u => {
            var list = lists.Units[u.Id]; int share = list.HelpfulCount * 128 / (list.HelpfulCount + list.HarmfulCount);
            // The source's lowest-share ally override clears the sole allied actor, even when share >=77.
            return new BasicSkillAiListUnit(u.Id, list, share, u.Id == actor.UnitId ? false : null);
        }).ToImmutableArray();
        var entries = actorList.Entries.Select((e, i) => new BasicSkillAiPreparedOccurrence(i, e,
            e.OrdinaryUsable && !afterAct, e.OrdinaryUsable && (e.ForceConsumeAim || !e.ExhaustiveOriginScan))).ToImmutableArray();
        common = common with { EnemyAbilityReach = body.Move + actorList.Entries.Where(e => e.Harmful).Min(e => e.Range),
            AllyAbilityReach = body.Move + actorList.Entries.Where(e => e.Helpful).Select(e => e.Range).DefaultIfEmpty(0).Min() };
        int harmful = lists.Units[turn.TargetId].HarmfulCount;
        return new(true, null, new(common, p.Team, units, entries, harmful, lists.Units[turn.TargetId].PhysicalHarmfulCount), refresh.RngState);
    }
}
