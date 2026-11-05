using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Typed executable self effect/grants plus explicit retail allied-stat/map-entry AI behavior.</summary>
public sealed record RosterAccumulateAiProfile(ImmediateAbilityBattleProfile Ability, bool UsableRetailAlliedMapStatEntry = false);
public sealed record RosterAccumulateAiUsefulnessRequest(BattleUnitId ActorId, AssetId AbilityId, ImmutableArray<BattleUnitId> ActiveUnits);
public enum RosterAccumulateAiRejection { None, UnsupportedContext, UnsupportedSimulation, UnknownActor, UnavailableAction,
    IncompleteBindings, UnsupportedNumericProfile, UnknownAbility, AbilityNotGranted, InvalidActiveMask,
    RandomDrawCountExhausted, TickCountExhausted }
public sealed record RosterAccumulateAiUsefulnessResult(bool Accepted, RosterAccumulateAiRejection Rejection,
    AccumulateAiUsefulnessOutcome? Outcome, RandomState RngState);

/// <summary>One supplied active-mask entry filter; every allied source slot draws, even beyond self range. No general prefix or candidate evaluation.</summary>
/// <remarks>ZOD-290/292; source 11464943bbdbd2a0af022d90cb7f10455cf911df. No actor-only shortcut, once-per-turn cache or command authority.</remarks>
public static class RosterAccumulateAiUsefulness
{
    public static RosterAccumulateAiUsefulnessResult Evaluate(AttackBattleSnapshot state, RosterAccumulateAiUsefulnessRequest request,
        RosterAiSimulationProfile simulation, RosterAccumulateAiProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(simulation); ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(profile.Ability);
        RosterAccumulateAiUsefulnessResult Reject(RosterAccumulateAiRejection reason) => new(false, reason, null, rng);
        if (!profile.UsableRetailAlliedMapStatEntry) return Reject(RosterAccumulateAiRejection.UnsupportedContext);
        var invalid = simulation.Validate(state, request.ActorId);
        if (invalid != RosterAiRejection.None) return Reject(invalid == RosterAiRejection.UnknownActor
            ? RosterAccumulateAiRejection.UnknownActor : RosterAccumulateAiRejection.UnsupportedSimulation);
        if (state.TurnBattle.Turns.Units.Single(u => u.Id == request.ActorId).ActionTaken) return Reject(RosterAccumulateAiRejection.UnavailableAction);
        if (!profile.Ability.Units.Keys.ToHashSet().SetEquals(simulation.Battle.Units.Keys)) return Reject(RosterAccumulateAiRejection.IncompleteBindings);
        if (state.Combatants.Any(u => u.PhysicalAttack is < 1 or > 99)) return Reject(RosterAccumulateAiRejection.UnsupportedNumericProfile);
        if (!profile.Ability.Abilities.ContainsKey(request.AbilityId)) return Reject(RosterAccumulateAiRejection.UnknownAbility);
        if (!profile.Ability.Units[request.ActorId].Abilities.Contains(request.AbilityId)) return Reject(RosterAccumulateAiRejection.AbilityNotGranted);
        if (request.ActiveUnits.IsDefault || request.ActiveUnits.Distinct().Count() != request.ActiveUnits.Length ||
            request.ActiveUnits.Any(id => !simulation.Battle.Units.ContainsKey(id))) return Reject(RosterAccumulateAiRejection.InvalidActiveMask);
        int team = simulation.Battle.Units[request.ActorId].StableTeam;
        var enemies = simulation.Battle.Members.Where(u => u.StableTeam != team).Select(u => u.UnitId).ToHashSet();
        var result = AccumulateAiUsefulness.FilterFresh(state, request.ActorId, request.ActiveUnits.ToHashSet(), enemies, rng);
        return result.Accepted ? new(true, RosterAccumulateAiRejection.None, result.Outcome, result.RngState)
            : Reject(RosterAccumulateAiRejection.RandomDrawCountExhausted);
    }
}
