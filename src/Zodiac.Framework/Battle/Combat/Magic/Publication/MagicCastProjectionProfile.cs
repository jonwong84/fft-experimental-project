namespace Zodiac.Framework;

/// <summary>Certifies the supplied eligible target order and absence of direct actor HP/MP result work.</summary>
public sealed record MagicCastProjectionProfile(bool EligibleSourceOrderKnown = false, bool NoActorVitalWorkKnown = false,
    MagicLivingApplicationProfile? Application = null);
