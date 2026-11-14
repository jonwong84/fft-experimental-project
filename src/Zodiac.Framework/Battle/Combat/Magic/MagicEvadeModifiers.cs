using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Explicit magic status facts; TargetActionUnrestricted is the already-classified source restriction.</summary>
public sealed record MagicEvadeModifiers(bool TargetActionUnrestricted, bool AttackerTransparent = false,
    bool TargetAbandon = false, bool TargetDontAct = false, bool TargetSleep = false, bool TargetStop = false,
    bool TargetConfusion = false, bool TargetCharging = false, bool TargetPerforming = false, bool TargetDefending = false);
