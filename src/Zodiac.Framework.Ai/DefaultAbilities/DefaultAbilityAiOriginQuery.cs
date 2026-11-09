using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework.Ai;

/// <summary>Fresh scenario/pass cast origins and unconsumed selectable aims. No spell formulas, candidate ranking or commands.</summary>
/// <remarks>ZOD-438; evaluation.c at 11464943bbdbd2a0af022d90cb7f10455cf911df.</remarks>
public static class DefaultAbilityAiOriginQuery
{
    public static DefaultAbilityAiOriginResult Evaluate(DefaultAbilityBattleState state, DefaultAbilityAiCatalog catalog,
        DefaultAbilityAiOriginRequest request, DefaultAbilityAiMovementProfile profile, RandomState rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profile);
        DefaultAbilityAiOriginResult Reject(string reason) => new(false, reason, null, rng);
        try
        {
            DefaultAbilityAiAimEnumeration.ValidateRoster(state, request.ActorId);
            var work = new DefaultAbilityAiWork(state, catalog, request.ActorId, request.PrimarySlot, followUnit: false);
            return Build(work, request.Scenario, profile, rng);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or OverflowException)
        {
            return Reject("Unsupported ordinary spell cast origins: " + e.Message);
        }
    }

    // Composed queries reuse one freshly owned binding, not a caller's cached origin result.
    internal static DefaultAbilityAiOriginResult Build(DefaultAbilityAiWork work, DefaultAbilityAiOriginScenario scenarioKind,
        DefaultAbilityAiMovementProfile profile, RandomState rng)
    {
        DefaultAbilityAiOriginResult Reject(string reason) => new(false, reason, null, rng);
        if (!Enum.IsDefined(scenarioKind)) return Reject("An explicit supported movement scenario is required.");
        var potential = DefaultAbilityAiAimEnumeration.BuildPotentialPasses(work);
        var movement = DefaultAbilityAiMovementQuery.Build(work, profile, rng);
        if (!movement.Accepted) return Reject(movement.Rejection!);
        var baseline = movement.Outcome!;
        var scenario = baseline.Scenario1;
        if (scenarioKind == DefaultAbilityAiOriginScenario.Complement) scenario = baseline.Scenario2;
        var passes = ImmutableArray.CreateBuilder<DefaultAbilityAiOriginPass>();
        foreach (var pass in potential.Passes)
        {
            var origins = AiCastAimTilesBuild(work, baseline, scenario, pass);
            bool ranked = !potential.Ability.Usage.ExhaustiveOrigins;
            var choices = ImmutableArray.CreateBuilder<DefaultAbilityAiOrigin>();
            if (ranked)
            {
                // Source's ranked while loop requires both nonzero byte counts before reading any priority.
                if (!pass.Aims.IsEmpty)
                {
                    var order = AiRosterCastGeometry.BindFresh(baseline.Cells, origins, out var missing);
                    if (missing is { } tile) return Reject($"Unwritten cast-origin priority at {tile}.");
                    foreach (var key in order)
                    {
                        var choice = OriginDestinationFinalize(work, baseline, key.Tile, key);
                        choices.Add(OriginAimTilesBuild(work, pass, choice));
                    }
                }
            }
            else
            {
                // The source scans masks directly for exhaustive origins and never reads tile priority here.
                foreach (var tile in origins.OrderBy(t => t.Layer).ThenBy(t => t.Y).ThenBy(t => t.X))
                {
                    var choice = OriginDestinationFinalize(work, baseline, tile, null);
                    choices.Add(OriginAimTilesBuild(work, pass, choice));
                }
            }
            passes.Add(new(pass, origins, ranked, choices.ToImmutable()));
        }
        return new(true, null, new(baseline, potential, scenarioKind, passes.ToImmutable()), movement.RngState);
    }

    // Aim rows come from the shared caller-pass builder; this completes their matching reachable-origin mask/counts.
    private static ImmutableArray<TilePosition> AiCastAimTilesBuild(DefaultAbilityAiWork work,
        DefaultAbilityAiMovementOutcome movement, ImmutableArray<TilePosition> scenario, DefaultAbilityAiAimPass pass)
    {
        var targets = pass.UsefulTargets.Where(id => id != work.Actor.Id)
            .Select(id => movement.Targets.ById[id]).ToImmutableArray();
        int radius = work.Ability.Geometry.Range;
        if (pass.Kind == DefaultAbilityAiAimPassKind.Tiles) radius += work.Ability.Geometry.Area;
        var origins = AiRosterCastGeometry.Origins(scenario, targets, radius);
        ValidateCounts(origins.Length, pass.Aims.Length);
        return origins;
    }

    private static DefaultAbilityAiOrigin OriginDestinationFinalize(DefaultAbilityAiWork work,
        DefaultAbilityAiMovementOutcome movement, TilePosition origin, AiCastOriginPriority? priority)
    {
        // These typed lower-layer coordinates certify the source word's fourth byte as zero.
        bool actFirst = origin == work.Actor.Position;
        var destination = origin;
        if (actFirst) destination = movement.ActFirstDestination; // Source candidate_coords[2] also copies candidate_coords[1].
        return new(origin, destination, actFirst, priority, []);
    }

    // Bounded mask portion of origin_aim_tiles_evaluate; no formula, consumption or processing_done branch runs here.
    private static DefaultAbilityAiOrigin OriginAimTilesBuild(DefaultAbilityAiWork work, DefaultAbilityAiAimPass pass,
        DefaultAbilityAiOrigin origin)
    {
        var range = DefaultAbilityTargeting.Range(work.State.Definition.Map, origin.CastOrigin, work.Ability.Geometry,
            new(true, true, true, true));
        if (!range.Accepted) throw new NotSupportedException("Selectable range rejected: " + range.Rejection);
        var selectable = range.Outcome!.Panels.ToHashSet();
        return origin with { Aims = pass.Aims.Where(a => selectable.Contains(a.Position)).ToImmutableArray() };
    }

    internal static void ValidateCounts(int origins, int aims)
    {
        if (origins is < 0 or > 255 || aims is < 0 or > 255)
            throw new NotSupportedException("Cast-origin and aim counts must fit the source unsigned-byte counters without aliasing.");
    }
}
