using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Source ordinary targeting rules; independent of spell amount and animation.</summary>
public sealed record OrdinarySpellGeometry(byte Range, byte Area, byte Vertical, bool RangeUsesVertical = false,
    bool CannotTargetSelf = false, bool CannotHitCaster = false, bool TopDown = false,
    bool CanHitAllies = false, bool CanHitEnemies = false);

public sealed record SpellTargetingProfile(bool OrdinaryMapAimedSpellKnown = false, bool NoSpecialAbilityRulesKnown = false,
    bool NoSelectableCrossSectionsKnown = false, bool UnusedSourcePanelsExcludedKnown = false);
public enum SpellTargetingRejection { None, UnsupportedContext, UnsupportedMap, InvalidOrigin, InvalidAim, UnsupportedRoster }

/// <summary>Opaque derived panel set; a blue selection range cannot stand in for a green resolution area.</summary>
public sealed class SpellTargetPanels
{
    public ImmutableArray<TilePosition> Panels { get; }
    public TilePosition CasterPosition { get; }
    public TilePosition Center { get; }
    public OrdinarySpellGeometry Geometry { get; }
    public bool IsArea { get; }
    internal BattleMapDefinition Map { get; }
    internal SpellTargetPanels(BattleMapDefinition map, TilePosition caster, TilePosition center,
        OrdinarySpellGeometry geometry, bool area, ImmutableArray<TilePosition> panels)
    { Map = map; CasterPosition = caster; Center = center; Geometry = geometry; IsArea = area; Panels = panels; }
}
public sealed record SpellPanelQueryResult(bool Accepted, SpellTargetingRejection Rejection, SpellTargetPanels? Outcome);

public enum SpellTargetTeam { Blue, Red, Green, LightBlue }
/// <summary>Source-order and map facts, separate from current formula input snapshots.</summary>
public sealed record SpellTargetOccupant(BattleUnitId UnitId, int SourceOrder, TilePosition Position,
    SpellTargetTeam InitialTeam, SpellTargetTeam CurrentTeam, bool Active = true, bool Crystal = false,
    bool Treasure = false, bool Jump = false, bool Mount = false);
public sealed record SpellTargetRosterProfile(bool CompleteRosterAndSourceOrderKnown = false);
public sealed record SpellTargetSelection(ImmutableArray<TilePosition> Panels, ImmutableArray<BattleUnitId> UnitIds);
public sealed record SpellTargetSelectionResult(bool Accepted, SpellTargetingRejection Rejection, SpellTargetSelection? Outcome);

/// <summary>Pure original-compatible map queries, with no transient marks or live state.</summary>
public static class OrdinarySpellTargeting
{
    private static bool Eligible(SpellTargetOccupant u) => u.Active && !u.Crystal && !u.Treasure && !u.Jump && !u.Mount;

    /// <summary>Apply source per-panel team mutations, then collect occupants. No per-unit team refilter.</summary>
    public static SpellTargetSelectionResult SelectTargets(SpellTargetPanels area, BattleUnitId casterId,
        ImmutableArray<SpellTargetOccupant> roster, SpellTargetRosterProfile? profile, bool nearestFirst = false)
    {
        ArgumentNullException.ThrowIfNull(area);
        SpellTargetSelectionResult Reject() => new(false, SpellTargetingRejection.UnsupportedRoster, null);
        if (!area.IsArea || profile is not { CompleteRosterAndSourceOrderKnown: true } || roster.IsDefaultOrEmpty) return Reject();
        var ids = new HashSet<BattleUnitId>(); var orders = new HashSet<int>();
        foreach (var u in roster)
        {
            if (u is null || string.IsNullOrWhiteSpace(u.UnitId.Value) || !ids.Add(u.UnitId) ||
                u.SourceOrder < 0 || !orders.Add(u.SourceOrder) || !Enum.IsDefined(u.InitialTeam) || !Enum.IsDefined(u.CurrentTeam) ||
                u.Active && !area.Map.TryGetTile(u.Position, out _)) return Reject();
        }
        var actor = roster.FirstOrDefault(u => u.UnitId == casterId);
        if (actor is null || !Eligible(actor) || actor.Position != area.CasterPosition) return Reject();
        var ordered = roster.OrderBy(u => u.SourceOrder).ToArray();
        var remaining = area.Panels.ToHashSet(); var marked = new HashSet<TilePosition>(); var g = area.Geometry;
        if (g.CanHitAllies || g.CanHitEnemies)
        {
            foreach (var u in ordered)
            {
                if (!Eligible(u) || !remaining.Contains(u.Position)) continue;
                bool ally = u.UnitId == casterId || u.InitialTeam == actor.CurrentTeam;
                if (ally ? g.CanHitAllies : g.CanHitEnemies) marked.Add(u.Position);
                else remaining.Remove(u.Position); // Earlier marks intentionally survive this mutation.
            }
            if (g.Area != 255) marked.UnionWith(remaining);
        }
        else marked.UnionWith(remaining);
        IEnumerable<SpellTargetOccupant> selected = ordered.Where(u => Eligible(u) && marked.Contains(u.Position));
        if (nearestFirst) selected = selected.OrderBy(u => Math.Abs(u.Position.X - actor.Position.X) + Math.Abs(u.Position.Y - actor.Position.Y));
        return new(true, SpellTargetingRejection.None, new(area.Panels.Where(marked.Contains).ToImmutableArray(),
            selected.Select(u => u.UnitId).ToImmutableArray()));
    }

    public static SpellPanelQueryResult Range(BattleMapDefinition map, TilePosition caster, OrdinarySpellGeometry geometry,
        SpellTargetingProfile? profile) => Build(map, caster, caster, geometry, profile, false);

    /// <summary>Resolve a previously validated normalized map aim. Does not repeat blue range validation.</summary>
    public static SpellPanelQueryResult Area(BattleMapDefinition map, TilePosition caster, TilePosition aim,
        OrdinarySpellGeometry geometry, SpellTargetingProfile? profile) => Build(map, caster, aim, geometry, profile, true);

    private static SpellPanelQueryResult Build(BattleMapDefinition map, TilePosition caster, TilePosition center,
        OrdinarySpellGeometry geometry, SpellTargetingProfile? profile, bool area)
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
    public static byte ReferenceHeight(BattleTile tile)
    {
        if (!SourceTile(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
        return unchecked((byte)Height(tile));
    }

    /// <summary>Choose one sheet, preserving blocked -255 comparisons and lower-sheet ties.</summary>
    public static int? SelectLayer(BattleTile lower, BattleTile? upper, byte reference, byte tolerance, bool topDown = false)
    {
        if (!SourceTile(lower) || upper is { } t && !SourceTile(t)) throw new ArgumentOutOfRangeException(nameof(lower));
        bool a = !lower.IsBlocked, b = upper is { IsBlocked: false };
        int ah = a ? Height(lower) : -255, bh = b ? Height(upper!.Value) : -255;
        if (topDown)
        {
            if (ah >= bh) { b = false; bh = -255; }
            else { a = false; ah = -255; }
        }
        int ad = Math.Abs(ah - reference), bd = Math.Abs(bh - reference);
        return bd < ad ? b && bd <= tolerance * 2 ? 1 : null : a && ad <= tolerance * 2 ? 0 : null;
    }
}
