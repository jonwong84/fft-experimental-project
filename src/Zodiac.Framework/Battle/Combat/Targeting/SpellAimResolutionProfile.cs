namespace Zodiac.Framework;

/// <summary>Certifies a valid active ordinary nondirectional actor/ability and, when followed, current anchor identity/position.</summary>
public sealed record SpellAimResolutionProfile(bool OrdinaryNondirectionalContextKnown = false,
    bool AnchorIdentityAndPositionKnown = false);
