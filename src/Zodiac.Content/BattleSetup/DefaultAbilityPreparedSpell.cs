using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

public sealed record DefaultAbilityPreparedSpell(string Id, string Name, short OriginalId, FaithMagicSpell Formula,
    int MpCost, byte ChargeTime, bool AffectedBySilence, DefaultAbilityGeometry Targeting);
