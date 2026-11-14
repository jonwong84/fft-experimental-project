namespace Zodiac.Framework;

/// <summary>Ordinary Default-menu blue choice classification; no UI, command or resource work.</summary>
public static class SpellTargetChoiceQuery
{
    public static SpellTargetChoiceResult Evaluate(BattleMapDefinition map, SpellTargetOccupant caster,
        MagicCastDefinition definition, SpellTargetingProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Spell);
        ArgumentOutOfRangeException.ThrowIfNegative(definition.MpCost, nameof(definition.MpCost));

        SpellTargetChoiceResult Reject(SpellTargetingRejection reason) => new(false, reason, null);
        if (string.IsNullOrWhiteSpace(definition.Id.Value) || string.IsNullOrWhiteSpace(caster.UnitId.Value))
            throw new ArgumentException("Explicit caster and spell identities are required.");
        if (!caster.Active || definition.OriginalAbilityId is < 1 or >= 368 || definition.OriginalAbilityId == 329 ||
            definition.Targeting is not { } g || profile is not { OrdinaryMapAimedSpellKnown: true, NoSpecialAbilityRulesKnown: true,
                NoSelectableCrossSectionsKnown: true, UnusedSourcePanelsExcludedKnown: true }) return Reject(SpellTargetingRejection.UnsupportedContext);
        if (map.Width > 255 || map.Depth > 255 || (long)map.Width * map.Depth > 256 || map.LayerCount > 2)
            return Reject(SpellTargetingRejection.UnsupportedMap);
        if (g.Auto && g.Range == 0) return new(true, SpellTargetingRejection.None, new(definition, caster, SpellTargetChoiceMode.NoTargeting, null));
        var range = DefaultAbilityTargeting.Range(map, caster.Position, g, profile);
        if (!range.Accepted) return Reject(range.Rejection);
        var mode = range.Outcome!.Panels.IsEmpty ? SpellTargetChoiceMode.None : g.NoFollowTarget ? SpellTargetChoiceMode.TileOnly : SpellTargetChoiceMode.UnitOrTile;
        return new(true, SpellTargetingRejection.None, new(definition, caster, mode, range.Outcome));
    }
}
