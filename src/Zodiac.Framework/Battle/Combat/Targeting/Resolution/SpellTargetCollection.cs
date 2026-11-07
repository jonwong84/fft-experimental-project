using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Source-ordered panel/team mutations and recipient collection after area geometry.</summary>
internal static class SpellTargetCollection
{
    private static bool Eligible(SpellTargetOccupant u) => u.Active && !u.Crystal && !u.Treasure && !u.Jump && !u.Mount;

    /// <summary>Apply source per-panel team mutations, then collect occupants. No per-unit team refilter.</summary>
    internal static SpellTargetSelectionResult Select(SpellTargetPanels area, BattleUnitId casterId,
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

}
