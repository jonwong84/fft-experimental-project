using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Range-one ordinary sword origin geometry, not an executable weapon/effect binding.</summary>
public sealed record RosterAiCastProfile(bool OrdinaryRangeOneAttack = false);
public sealed record RosterAiOccurrenceOrigins(BasicSkillAiPreparedOccurrence Entry,
    ImmutableArray<TilePosition> CastOrigins, ImmutableArray<AiCastOriginPriority> RankedOrigins);
public sealed record RosterAiCastOriginOutcome(RosterAiGeneralScenarioOutcome Scenarios,
    ImmutableArray<RosterAiOccurrenceOrigins> Occurrences);
public enum RosterAiCastOriginRejection { None, UnsupportedContext, PrefixRejected, UnknownPriority }
public sealed record RosterAiCastOriginFailure(RosterAiCastOriginRejection Reason, RosterAiGeneralFailure? Prefix = null,
    int? OccurrenceIndex = null, TilePosition? MissingTile = null);
public sealed record RosterAiCastOriginResult(bool Accepted, RosterAiCastOriginFailure? Failure,
    RosterAiCastOriginOutcome? Outcome, RandomState RngState, AiPriorityWorkspace Workspace);

/// <summary>One fresh roster prefix and actual ranked-read provenance across all active enemies; no action/aim evaluation.</summary>
/// <remarks>ZOD-290/291; source 11464943bbdbd2a0af022d90cb7f10455cf911df. Attack exhaustive and self map entries read no priority keys.</remarks>
public static class RosterAiCastOrigins
{
    public static RosterAiCastOriginResult Evaluate(AttackBattleSnapshot state, RosterAiRefreshRequest request,
        RosterAiSimulationProfile simulation, RosterAiRefreshProfile refresh, AiHumanAbilityLists lists,
        RosterAiGeneralProfile general, RosterAiMovementProfile scoring, RosterAiCastProfile profile,
        RandomState rng, AiPriorityWorkspace? workspace = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(profile);
        workspace ??= AiPriorityWorkspace.StartUnknown(state.TurnBattle.Battle.Map); workspace.ValidateMap(state);
        RosterAiCastOriginResult Reject(RosterAiCastOriginFailure failure) => new(false, failure, null, rng, workspace);
        if (!profile.OrdinaryRangeOneAttack) return Reject(new(RosterAiCastOriginRejection.UnsupportedContext));
        var prefix = RosterAiGeneralScenarios.Evaluate(state, request, simulation, refresh, lists, general, scoring, rng);
        if (!prefix.Accepted) return Reject(new(RosterAiCastOriginRejection.PrefixRejected, prefix.Failure));
        var m = prefix.Outcome!; var p = m.Preparation;
        var enemies = AiRosterCastGeometry.UsefulEnemies(p);
        var occurrences = ImmutableArray.CreateBuilder<RosterAiOccurrenceOrigins>();
        foreach (var entry in p.Preparation.ActorEntries)
        {
            var origins = entry.Usable && entry.Source.Harmful
                ? AiRosterCastGeometry.Origins(m.Scenario1, enemies, entry.Source.Range)
                : ImmutableArray<TilePosition>.Empty;
            ImmutableArray<AiCastOriginPriority> keys = [];
            if (entry.Usable && entry.Source.Kind == AiHumanAbilityKind.ThrowStone)
            {
                keys = AiRosterCastGeometry.BindRanked(m, origins, workspace, out var missing);
                if (missing is not null) return Reject(new(RosterAiCastOriginRejection.UnknownPriority, OccurrenceIndex: entry.Index, MissingTile: missing));
            }
            // Diagnostics report the complete initial ranking, not a claim that every origin is ultimately evaluated.
            occurrences.Add(new(entry, origins, keys));
        }
        var successor = workspace.Write(m.Movement.Select(t => KeyValuePair.Create(t.Outcome.Destination, t.TilePriority)));
        return new(true, null, new(m, occurrences.ToImmutable()), prefix.RngState, successor);
    }
}
