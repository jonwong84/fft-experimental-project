using System.Collections.Immutable;

namespace Zodiac.Framework;

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
