using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Supported signed HP work and known flags, before or after bounded result finalization.</summary>
public sealed record MagicResultFrame(bool Hit, ushort Accuracy, MagicResultMissKind Miss,
    short HpDamage, short HpHealing, MagicResultTypes Types, MagicResultEffects Effects,
    bool ClearFirstCanceledStatusByte = false);
