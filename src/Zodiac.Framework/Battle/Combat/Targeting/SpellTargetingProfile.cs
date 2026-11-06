namespace Zodiac.Framework;

public sealed record SpellTargetingProfile(bool OrdinaryMapAimedSpellKnown = false, bool NoSpecialAbilityRulesKnown = false,
    bool NoSelectableCrossSectionsKnown = false, bool UnusedSourcePanelsExcludedKnown = false);
