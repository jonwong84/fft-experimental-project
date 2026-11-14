using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>A resolved slot's primary-type qualification and byte fields, not a live equipment lookup.</summary>
public readonly record struct MagicEvadeItem(byte ItemId, byte MagicEvade, bool HasRequiredType);
