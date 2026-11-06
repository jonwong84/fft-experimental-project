using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record StoredSpellCastRequest(PreparedMagicCast Cast, BattleMapDefinition Map, StoredSpellAim Aim,
    ImmutableArray<SpellTargetCombatant> Roster, SpellTargetingProfile? GeometryProfile,
    SpellTargetRosterProfile? RosterProfile, SpellAimResolutionProfile? AimProfile, SpellTargetCastProfile? Entry);
