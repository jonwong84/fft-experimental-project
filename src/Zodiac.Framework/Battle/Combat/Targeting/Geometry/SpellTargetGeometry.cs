using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>
/// Shared bounded geometry for ability_range_set and ability_area_set, reference
/// 11464943bbdbd2a0af022d90cb7f10455cf911df. Spread, vertical filtering and panel publication;
/// no target-choice UI, candidate search or formula work.
/// </summary>
internal static class SpellTargetGeometry
{
    internal static SpellPanelQueryResult Range(BattleMapDefinition map, TilePosition caster,
        DefaultAbilityGeometry geometry, SpellTargetingProfile? profile) => Build(map, caster, caster, geometry, profile, false);

    internal static SpellPanelQueryResult Area(BattleMapDefinition map, TilePosition caster, TilePosition aim,
        DefaultAbilityGeometry geometry, SpellTargetingProfile? profile) => Build(map, caster, aim, geometry, profile, true);

    private static SpellPanelQueryResult Build(BattleMapDefinition map, TilePosition caster, TilePosition center,
        DefaultAbilityGeometry geometry, SpellTargetingProfile? profile, bool area)
    {
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(geometry);
        SpellPanelQueryResult Reject(SpellTargetingRejection reason) => new(false, reason, null);
        if (profile is not { OrdinaryMapAimedSpellKnown: true, NoSpecialAbilityRulesKnown: true,
                NoSelectableCrossSectionsKnown: true, UnusedSourcePanelsExcludedKnown: true })
            return Reject(SpellTargetingRejection.UnsupportedContext);
        if (map.Width > 255 || map.Depth > 255 || (long)map.Width * map.Depth > 256 || map.LayerCount > 2)
            return Reject(SpellTargetingRejection.UnsupportedMap);
        for (int l = 0; l < map.LayerCount; l++)
            for (int y = 0; y < map.Depth; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    map.TryGetTile(new(x, y, l), out var t);
                    if (!SourceTile(t)) return Reject(SpellTargetingRejection.UnsupportedMap);
                }
        if (!map.TryGetTile(caster, out var actorTile) || actorTile.IsBlocked) return Reject(SpellTargetingRejection.InvalidOrigin);
        if (!map.TryGetTile(center, out var centerTile) || centerTile.IsBlocked) return Reject(SpellTargetingRejection.InvalidAim);
        int radius = area ? geometry.Area : geometry.Range;
        bool vertical = area ? radius != 255 : geometry.RangeUsesVertical;
        var panels = ImmutableArray.CreateBuilder<TilePosition>();
        // Manhattan distance is equivalent to the traced residual-budget spread; obstacles do not stop it.
        for (int l = 0; l < map.LayerCount; l++)
            for (int y = 0; y < map.Depth; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var pos = new TilePosition(x, y, l);
                    if (radius != 255 && Math.Abs(x - center.X) + Math.Abs(y - center.Y) > radius) continue;
                    if (!area && geometry.CannotTargetSelf && pos == caster) continue;
                    if (vertical)
                    {
                        map.TryGetTile(new(x, y, 0), out var lower);
                        BattleTile? upper = map.TryGetTile(new(x, y, 1), out var u) ? u : null;
                        if (SelectLayer(lower, upper, ReferenceHeight(centerTile), geometry.Vertical, area && geometry.TopDown) != l) continue;
                    }
                    if (area && geometry.CannotHitCaster && pos == caster) continue;
                    map.TryGetTile(pos, out var tile);
                    if (tile.IsBlocked || tile.IsUntargetable && (area || geometry.Area == 0)) continue;
                    panels.Add(pos);
                }
        return new(true, SpellTargetingRejection.None, new(map, caster, center, geometry, area, panels.ToImmutable()));
    }

    private static bool SourceTile(BattleTile tile) => tile.Height <= 255 && tile.HalfHeight <= 31 && tile.WaterDepth <= 7;
    private static int Height(BattleTile tile) => tile.Height * 2 + tile.HalfHeight + tile.WaterDepth * 2;

    /// <summary>The original vertical helper receives an unsigned-byte reference after addition.</summary>
    internal static byte ReferenceHeight(BattleTile tile)
    {
        if (!SourceTile(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
        return unchecked((byte)Height(tile));
    }

    /// <summary>Choose one sheet, preserving blocked -255 comparisons and lower-sheet ties.</summary>
    internal static int? SelectLayer(BattleTile lower, BattleTile? upper, byte reference, byte tolerance, bool topDown = false)
    {
        if (!SourceTile(lower) || upper is { } t && !SourceTile(t)) throw new ArgumentOutOfRangeException(nameof(lower));
        bool a = !lower.IsBlocked, b = upper is { IsBlocked: false };
        int ah = a ? Height(lower) : -255, bh = b ? Height(upper!.Value) : -255;
        if (topDown)
        {
            // Source substitutes only the comparison height, not the panel's remaining range.
            if (ah >= bh) bh = -255;
            else ah = -255;
        }
        int ad = Math.Abs(ah - reference), bd = Math.Abs(bh - reference);
        return bd < ad ? b && bd <= tolerance * 2 ? 1 : null : a && ad <= tolerance * 2 ? 0 : null;
    }
}
