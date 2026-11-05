using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

public sealed record RosterAiListSummary(BattleUnitId UnitId, AiHumanInitializedList Initialization, int HelpfulShare, bool? MostlySupport);
public sealed record RosterAiTeamSupport(bool Available, bool SingleEnemyRemaining, bool RetainedPolicyExit,
    int? AllyCount, int? EnemyCount, int? ImpairedAllyCount, ImmutableArray<SwordAiRefreshPolicy> Policies);
public sealed record RosterAiPreparationOutcome(RosterAiRefreshOutcome Refresh, ImmutableArray<RosterAiListSummary> Units,
    ImmutableArray<BasicSkillAiPreparedOccurrence> ActorEntries, int EnemyHarmfulCount, int EnemyPhysicalCount,
    int EnemyAbilityReach, int AllyAbilityReach, RosterAiTeamSupport TeamSupport, AiTargetRoster Targets);
public sealed record RosterAiPreparationResult(bool Accepted, RosterAiRejection Rejection,
    RosterAiPreparationOutcome? Outcome, RandomState RngState);

/// <summary>Own fresh refresh, ordinary list summaries and support flags on restored live state; no policy/action search or executable grants.</summary>
public static class RosterAiPreparation
{
    public static RosterAiPreparationResult Evaluate(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refreshProfile, AiHumanAbilityLists lists, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(refreshProfile); ArgumentNullException.ThrowIfNull(lists);
        RosterAiPreparationResult Reject(RosterAiRejection reason) => new(false, reason, null, rng);
        if (lists.Units.Count != simulation.Battle.Units.Count || !lists.Units.Keys.ToHashSet().SetEquals(simulation.Battle.Units.Keys) ||
            lists.Units.Values.Any(l => l.Entries.Length is < 2 or > 34 || l.Entries[0].Kind != AiHumanAbilityKind.Attack ||
                l.Entries[^1].Kind != AiHumanAbilityKind.FrogAttack || l.Entries.Skip(1).SkipLast(1)
                    .Any(e => e.Kind is not (AiHumanAbilityKind.Accumulate or AiHumanAbilityKind.ThrowStone or AiHumanAbilityKind.Dash)))) return Reject(RosterAiRejection.UnsupportedList);
        var body = state.TurnBattle.Battle.Units.FirstOrDefault(u => u.Id == request.ActorId);
        if (body is not null && body.Move > 124) return Reject(RosterAiRejection.UnsupportedProfile);
        var refreshed = RosterAiRefresh.Evaluate(state, request, simulation, refreshProfile, rng);
        if (!refreshed.Accepted) return Reject(refreshed.Rejection);
        var r = refreshed.Outcome!; var flags = r.RefreshedUnits.ToDictionary(u => u.UnitId);
        BattleUnitId? lowest = null; int lowestShare = 256;
        var summaries = simulation.Battle.Members.Select(member => {
            var list = lists.Units[member.UnitId]; int share = list.HelpfulCount * 128 / (list.HelpfulCount + list.HarmfulCount);
            bool enemy = flags[member.UnitId].Enemy;
            if (!enemy && share < lowestShare) { lowest = member.UnitId; lowestShare = share; }
            return new RosterAiListSummary(member.UnitId, list, share, enemy ? null : share >= 77);
        }).ToImmutableArray();
        summaries = summaries.Select(u => u.UnitId == lowest ? u with { MostlySupport = false } : u).ToImmutableArray();
        var actorTurn = state.TurnBattle.Turns.Units.Single(u => u.Id == request.ActorId);
        var entries = lists.Units[request.ActorId].Entries.Select((e, i) => new BasicSkillAiPreparedOccurrence(i, e,
            e.OrdinaryUsable && !actorTurn.ActionTaken, e.OrdinaryUsable && (e.ForceConsumeAim || !e.ExhaustiveOriginScan))).ToImmutableArray();
        var policies = r.RefreshedUnits.Select(u => new SwordAiRefreshPolicy(u.UnitId, u.ConserveCt, u.TeamSupportUnavailable)).ToImmutableArray();
        bool blocked = flags[request.ActorId].TeamSupportUnavailable; int? allies = null, enemies = null, impaired = null;
        bool support = false, single = false;
        if (!blocked)
        {
            allies = r.RefreshedUnits.Count(u => !u.Enemy); enemies = r.RefreshedUnits.Count(u => u.Enemy);
            impaired = state.Combatants.Count(u => !flags[u.UnitId].Enemy && u.Status == AttackUnitStatus.Critical);
            support = allies >= 2 && allies != impaired; single = support && enemies == 1;
            if (!support && simulation.Battle.Units[request.ActorId].StableTeam != 0)
                policies = policies.Select(p => !flags[p.UnitId].Enemy ? p with { TeamSupportUnavailable = true } : p).ToImmutableArray();
        }
        var targets = new AiTargetRoster(request.ActorId, r.CachedActorDelay, simulation.Battle.Members.Select(member => {
            var combat = state.Combatants.Single(u => u.UnitId == member.UnitId);
            var turn = state.TurnBattle.Turns.Units.Single(u => u.Id == member.UnitId);
            var position = state.TurnBattle.Battle.Units.Single(u => u.Id == member.UnitId).Position;
            return new AiRosterUnit(member.UnitId, member.SourceOrder, position, new(Critical: combat.Status == AttackUnitStatus.Critical),
                new(Enemy: flags[member.UnitId].Enemy, Considered: flags[member.UnitId].Considered),
                AiRosterEligibility.OrdinaryTurnDelay((byte)turn.Ct, (byte)turn.Speed), false);
        }));
        int harmful = summaries.Where(u => flags[u.UnitId].Enemy).Sum(u => u.Initialization.HarmfulCount);
        return new(true, RosterAiRejection.None, new(r, summaries, entries, harmful, harmful, body!.Move + 1, body.Move,
            new(support, single, blocked, allies, enemies, impaired, policies), targets), refreshed.RngState);
    }
}
