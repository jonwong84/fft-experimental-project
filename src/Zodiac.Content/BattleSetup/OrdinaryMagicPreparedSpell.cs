using Zodiac.Framework;

namespace Zodiac.Content.BattleSetup;

public sealed record OrdinaryMagicPreparedSpell(string Id, string Name, short OriginalId, FaithMagicSpell Formula,
    int MpCost, byte ChargeTime, bool AffectedBySilence, OrdinarySpellGeometry Targeting);
