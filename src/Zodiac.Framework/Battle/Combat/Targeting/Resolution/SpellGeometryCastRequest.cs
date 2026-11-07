using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record SpellGeometryCastRequest(PreparedMagicCast Cast, BattleMapDefinition Map, TilePosition SelectedMapAim,
    ImmutableArray<SpellTargetCombatant> Roster, SpellTargetingProfile? GeometryProfile,
    SpellTargetRosterProfile? RosterProfile, SpellTargetCastProfile? Entry);
