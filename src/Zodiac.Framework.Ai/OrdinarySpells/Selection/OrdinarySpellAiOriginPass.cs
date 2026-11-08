using System.Collections.Immutable;

namespace Zodiac.Framework.Ai;

/// <summary>Unconsumed pass-wide masks and ordered potential origins, before candidate-dependent exits.</summary>
public sealed record OrdinarySpellAiOriginPass(OrdinarySpellAiAimPass Potential, ImmutableArray<TilePosition> CastOriginMask,
    bool RankedOrigins, ImmutableArray<OrdinarySpellAiOrigin> Origins);
