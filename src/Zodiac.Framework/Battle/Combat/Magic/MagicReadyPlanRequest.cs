using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record MagicReadyPlanRequest(PendingMagicCast Plan, BattleMapDefinition Map,
    ImmutableArray<SpellTargetCombatant> Roster, MagicDamageWeather Weather, MagicFormulaEvaluationMode Mode,
    MagicResultFinalizationProfile? ResultProfile, MagicReadyCastProfile? ReadyProfile,
    SpellTargetingProfile? GeometryProfile, SpellTargetRosterProfile? RosterProfile,
    SpellAimResolutionProfile? AimProfile, SpellTargetCastProfile? Entry, MagicReadyPlanProfile? Profile);
