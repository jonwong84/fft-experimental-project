namespace Zodiac.Framework;

/// <summary>Certifies the ordinary preparation entry and stable actor/anchor mapping, excluding slot reuse.</summary>
public sealed record MagicChargePlanProfile(bool PreparationContextKnown = false, bool StableActorAndAnchorIdentityKnown = false);
