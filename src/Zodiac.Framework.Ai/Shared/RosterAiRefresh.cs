using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Owned roster simulation inputs; EffectFreeClock certifies no charges, mimes, upkeep, script or terrain effects.</summary>
public sealed class RosterAiSimulationProfile
{
    public FlatRosterBattleProfile Battle { get; }
    public ImmutableArray<AiUnitBaseline> Units { get; }
    public bool EffectFreeClock { get; }
    public RosterAiSimulationProfile(FlatRosterBattleProfile battle, IEnumerable<AiUnitBaseline> units, bool effectFreeClock = false)
    {
        ArgumentNullException.ThrowIfNull(battle); ArgumentNullException.ThrowIfNull(units);
        var copy = units.ToImmutableArray();
        if (copy.Any(u => u is null) || copy.Select(u => u.UnitId).Distinct().Count() != copy.Length ||
            !copy.Select(u => u.UnitId).ToHashSet().SetEquals(battle.Units.Keys))
            throw new ArgumentException("Specify one baseline for every owned roster unit.", nameof(units));
        Battle = battle; Units = copy; EffectFreeClock = effectFreeClock;
    }

    internal RosterAiRejection Validate(AttackBattleSnapshot state, BattleUnitId actorId)
    {
        if (!state.Combatants.Any(u => u.UnitId == actorId)) return RosterAiRejection.UnknownActor;
        if (!EffectFreeClock) return RosterAiRejection.UnsupportedContext;
        if (state.Combatants.Length > 16) return RosterAiRejection.UnsupportedProfile;
        if (state.Combatants.Any(u => u.Hp == 0)) return RosterAiRejection.TerminalInput;
        try { Battle.ValidateState(state); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException) { return RosterAiRejection.UnsupportedProfile; }
        if (state.TurnBattle.Turns.Phase != TurnCyclePhase.UnitTurn || state.TurnBattle.Turns.ActiveUnitId != actorId)
            return RosterAiRejection.UnavailableAction;
        return RosterAiRejection.None;
    }
}

public enum RosterAiRejection { None, UnsupportedContext, UnsupportedProfile, UnknownActor, TerminalInput,
    UnavailableAction, InvalidPolicies, UnsupportedList, InvalidDestination, RandomDrawCountExhausted, TickCountExhausted }
public sealed record RosterAiRefreshRequest(BattleUnitId ActorId, ImmutableArray<AiUnitPolicy> Policies);
public sealed record RosterAiRefreshProfile(bool FreshRefreshEntry = false, bool AllUnitsTargetable = false,
    bool ZeroCachedMpAbilityModifiers = false, bool CleanActorDelayCache = false);
public sealed record RosterAiRefreshOutcome(BattleUnitId ActorId, byte CachedActorDelay,
    ImmutableArray<SwordAiRefreshedUnit> RefreshedUnits, int TurnEndCt, ImmutableArray<AiProjectedUnit> Units,
    BattleUnitId MoveOpportunityUnitId, ulong ElapsedTicks, ulong Ticks, ImmutableArray<AiUpkeepPick> UpkeepPicks);
public sealed record RosterAiRefreshResult(bool Accepted, RosterAiRejection Rejection,
    RosterAiRefreshOutcome? Outcome, RandomState RngState);

/// <summary>One private fresh whole-roster refresh, with diagnostics only. Source ZOD-283/284 at 11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
internal static class RosterAiRefresh
{
    /// <summary>The admitted decision_flags_refresh: simulate the clock, derive flags and retain only decision facts/RNG.</summary>
    public static RosterAiRefreshResult DecisionFlagsRefresh(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(simulation); ArgumentNullException.ThrowIfNull(profile);
        RosterAiRefreshResult Reject(RosterAiRejection reason) => new(false, reason, null, rng);
        if (!profile.FreshRefreshEntry || !profile.AllUnitsTargetable || !profile.ZeroCachedMpAbilityModifiers || !profile.CleanActorDelayCache)
            return Reject(RosterAiRejection.UnsupportedContext);
        var invalid = simulation.Validate(state, request.ActorId); if (invalid != RosterAiRejection.None) return Reject(invalid);
        if (request.Policies.IsDefault || request.Policies.Any(p => p is null) || request.Policies.Length != simulation.Units.Length ||
            !request.Policies.Select(p => p.UnitId).ToHashSet().SetEquals(simulation.Battle.Units.Keys)) return Reject(RosterAiRejection.InvalidPolicies);
        var actor = state.TurnBattle.Turns.Units.Single(u => u.Id == request.ActorId);
        byte cachedDelay = (byte)AiRosterEligibility.OrdinaryTurnDelay((byte)actor.Ct, (byte)actor.Speed);
        var origin = state.TurnBattle.Battle.Units.Single(u => u.Id == request.ActorId).Position;
        var projection = AiEffectFreeProjection.Evaluate(state, request.ActorId, origin, simulation.Units, rng);
        if (projection.Rejection != AiSimulationClockRejection.None) return Reject(ClockReason(projection.Rejection));
        var clock = projection.Clock!; int team = simulation.Battle.Units[request.ActorId].StableTeam;
        var flags = clock.Units.Select(unit => {
            var policy = request.Policies.Single(p => p.UnitId == unit.UnitId);
            return new SwordAiRefreshedUnit(unit.UnitId,
                AiRosterEligibility.IsEnemy(request.ActorId, team, unit.UnitId, simulation.Battle.Units[unit.UnitId].StableTeam), true,
                false, false, false, unit.Hp < unit.MaxHp / 2, unit.Hp * 4 < unit.MaxHp * 3,
                false, false, false, false, policy.ConserveCt, policy.TeamSupportUnavailable);
        }).ToImmutableArray();
        return new(true, RosterAiRejection.None, new(request.ActorId, cachedDelay, flags, projection.TurnEndCt,
            clock.Units, clock.MoveOpportunityUnitId, clock.ElapsedTicks, clock.Ticks, clock.UpkeepPicks), clock.RngState);
    }

    internal static RosterAiRejection ClockReason(AiSimulationClockRejection reason) => reason == AiSimulationClockRejection.RandomDrawCountExhausted
        ? RosterAiRejection.RandomDrawCountExhausted : RosterAiRejection.TickCountExhausted;
}
