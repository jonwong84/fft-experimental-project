using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>
/// Targeting entry points: selectable range, affected area, ordered recipients and cast calculation.
/// See README.md in this folder for the player/AI pathways and pinned source map.
/// </summary>
public static class DefaultAbilityTargeting
{
    public static SpellPanelQueryResult Range(BattleMapDefinition map, TilePosition caster, DefaultAbilityGeometry geometry,
        SpellTargetingProfile? profile) => SpellTargetGeometry.Range(map, caster, geometry, profile);

    /// <summary>Resolve an already chosen aim; initial selectable range is not checked again at readiness.</summary>
    public static SpellPanelQueryResult Area(BattleMapDefinition map, TilePosition caster, TilePosition aim,
        DefaultAbilityGeometry geometry, SpellTargetingProfile? profile) => SpellTargetGeometry.Area(map, caster, aim, geometry, profile);

    public static SpellTargetSelectionResult SelectTargets(SpellTargetPanels area, BattleUnitId casterId,
        ImmutableArray<SpellTargetOccupant> roster, SpellTargetRosterProfile? profile, bool nearestFirst = false) =>
        SpellTargetCollection.Select(area, casterId, roster, profile, nearestFirst);

    public static MagicCastResolutionResult ResolveCast(SpellGeometryCastRequest request, RandomState rng) =>
        SpellTargetCastResolution.Resolve(request, rng);

    public static byte ReferenceHeight(BattleTile tile) => SpellTargetGeometry.ReferenceHeight(tile);

    public static int? SelectLayer(BattleTile lower, BattleTile? upper, byte reference, byte tolerance, bool topDown = false) =>
        SpellTargetGeometry.SelectLayer(lower, upper, reference, tolerance, topDown);
}
