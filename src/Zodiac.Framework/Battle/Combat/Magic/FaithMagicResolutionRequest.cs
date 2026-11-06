using Zodiac.Framework.Random;

namespace Zodiac.Framework;

public sealed record FaithMagicResolutionRequest(FaithMagicFormulaRequest Formula, FaithMagicEntryProfile? Entry,
    bool SecondaryEvadeable, MagicEvadeEquipmentInput? Equipment = null, MagicEvadeModifiers? EvadeModifiers = null);
