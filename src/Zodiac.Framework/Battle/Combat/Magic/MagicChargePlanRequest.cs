using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record MagicChargePlanRequest(ConfirmedSpellAim Selection, MagicCurrentUnit Caster,
    ImmutableArray<SpellTargetOccupant> Roster, SpellTargetRosterProfile? RosterProfile, MagicChargePlanProfile? Profile);
