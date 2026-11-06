using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Fresh result facts plus an Abandon diagnostic; ItemLost is a source blocking label, not an equipment mutation.</summary>
public sealed record MagicEvasionOutcome(bool Hit, ushort Accuracy, MagicResultMissKind Miss,
    byte ItemLost, bool AbandonTriggered);
