using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Positive source-entry certificates; these do not perform earlier reaction or reflection work.</summary>
/// <remarks>FreshResultKnown includes zero supported work and a known-clear stored result reaction; the source initializer preserves that field.</remarks>
public sealed record FaithMagicEntryProfile(bool FreshResultKnown = false, bool NoEarlierReactionsOrReflection = false,
    bool NonzeroAbilityKnown = false);
