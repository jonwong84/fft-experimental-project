namespace Zodiac.Framework;

/// <summary>Pure ordinary area-centre selection; source11464943bbdbd2a0af022d90cb7f10455cf911df.</summary>
public static class SpellAimResolver
{
    public static SpellAimResolutionResult Resolve(BattleMapDefinition map, StoredSpellAim aim, SpellAimAnchor? anchor,
        SpellAimResolutionProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(aim);
        SpellAimResolutionResult Reject(SpellAimRejection reason) => new(false, reason, null);
        if (profile is not { OrdinaryNondirectionalContextKnown: true }) return Reject(SpellAimRejection.UnsupportedContext);
        if (map.Width > 255 || map.Depth > 255 || (long)map.Width * map.Depth > 256 || map.LayerCount > 2)
            return Reject(SpellAimRejection.UnsupportedMap);
        TilePosition position;
        if (aim.TargetingType == 6)
        {
            if (aim.TargetUnitId is not { } id || string.IsNullOrWhiteSpace(id.Value) || anchor is null)
                return Reject(SpellAimRejection.MissingAnchor);
            if (anchor.UnitId != id) return Reject(SpellAimRejection.ConflictingAnchor);
            if (!profile.AnchorIdentityAndPositionKnown) return Reject(SpellAimRejection.UnsupportedContext);
            position = anchor.Position;
        }
        else position = new(unchecked((byte)aim.X), unchecked((byte)aim.Y), unchecked((byte)aim.Layer));
        if (!map.TryGetTile(position, out var tile)) return Reject(SpellAimRejection.InvalidPosition);
        if (tile.IsBlocked) return Reject(SpellAimRejection.BlockedCenter);
        return new(true, SpellAimRejection.None, position);
    }
}
