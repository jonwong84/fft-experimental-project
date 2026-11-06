using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Positive remaining facts. EntryProcContext's clear-flag fact describes entry, not post-proc flags.</summary>
public sealed record MagicResultFinalizationProfile(bool LivingTarget = false, bool NoPetrifyOrWall = false,
    bool NoGolem = false, bool EmptyStatusRecord = false, bool NoPendingKnockback = false,
    bool OnlySupportedHpResultWork = false, MagicConditionalProcProfile? EntryProcContext = null);
