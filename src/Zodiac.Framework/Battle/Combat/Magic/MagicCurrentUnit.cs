using Zodiac.Framework.Random;

namespace Zodiac.Framework;

/// <summary>Immutable current input capture, not a mutable battle unit or base-stat/equipment refresh implementation.</summary>
public sealed record MagicCurrentUnit(BattleUnitId UnitId, byte MagicAttack, byte Faith, MagicZodiacIdentity Identity,
    MagicCurrentStatuses Statuses, MagicCurrentSupports Supports, MagicElement Strengthens = MagicElement.None,
    MagicElementAffinities Affinities = default, MagicEvadeEquipmentInput? Equipment = null,
    bool ActionUnrestricted = true, bool HasRider = false, ushort Mp = 0, bool IsMime = false);
