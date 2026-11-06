using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Remaining caller/work facts. False is uncertified; execution/AI require a known-clear added flag.</summary>
public sealed record MagicConditionalProcProfile(bool OrdinaryPrimaryContext = false,
    bool NoPendingWeaponProcOrReaction = false, bool AddedEffectFlagKnownClear = false);
