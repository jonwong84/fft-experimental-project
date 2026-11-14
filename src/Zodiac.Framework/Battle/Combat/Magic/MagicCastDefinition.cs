using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Explicit spell inputs, not a runtime original-table lookup or complete content package.</summary>
/// <remarks>MP cost is a nonnegative managed integer; original record packing is an import concern.</remarks>
public sealed record MagicCastDefinition(AssetId Id, short OriginalAbilityId, FaithMagicSpell Spell,
    int MpCost, byte RawChargeTime, bool AffectedBySilence, bool Persevere = false, DefaultAbilityGeometry? Targeting = null);
