using System.Collections.Immutable;
using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record MagicReadyCastProfile(bool ChargeReadyKnown = false, bool ActionStateBookkeepingKnown = false,
    bool NoHamedoKnown = false, bool SingleOrdinaryDefaultStrikeKnown = false, bool FreshResultsKnown = false);
