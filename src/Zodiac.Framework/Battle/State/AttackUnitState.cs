using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Explicit effective combat inputs and immutable current state for the bounded sword scenario.</summary>
public sealed record AttackUnitState
{
    public BattleUnitId UnitId { get; }
    public int Hp { get; }
    public int MaxHp { get; }
    public int Mp { get; }
    public int MaxMp { get; }
    public int PhysicalAttack { get; }
    public int ClassPhysicalEvade { get; }
    public int RightHandEvade { get; }
    public int LeftHandEvade { get; }
    public int AccessoryEvade { get; }
    public int Brave { get; }
    public int ZodiacSign { get; }
    public BattleFacing Facing { get; }
    public AttackUnitStatus Status { get; }
    public int KoCount { get; }
    public AssetId? RightHandWeaponId { get; }

    public AttackUnitState(BattleUnitId unitId, int hp, int maxHp, int mp, int maxMp, int physicalAttack,
        int classPhysicalEvade, int rightHandEvade, int leftHandEvade, int accessoryEvade,
        int brave, int zodiacSign, BattleFacing facing, AttackUnitStatus status, int koCount, AssetId? rightHandWeaponId)
    {
        if (string.IsNullOrWhiteSpace(unitId.Value)) throw new ArgumentException("A combat unit requires an explicit ID.", nameof(unitId));
        if (maxHp is < 1 or > ushort.MaxValue || hp < 0 || hp > maxHp) throw new ArgumentOutOfRangeException(nameof(hp), "HP must be 0..maxHP and maxHP 1..65535.");
        if (mp != 0 || maxMp != 0) throw new NotSupportedException("The bounded sword profile requires explicit zero MP/maxMP.");
        if (physicalAttack is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(physicalAttack));
        if (classPhysicalEvade is < 0 or > 100) throw new NotSupportedException("The bounded profile supports class evade 0..100.");
        if (rightHandEvade != 0 || leftHandEvade != 0 || accessoryEvade != 0)
            throw new NotSupportedException("The bounded profile supports no equipment evasion modifiers.");
        if (brave is < 10 or > 100) throw new NotSupportedException("The bounded profile requires Brave 10..100.");
        if (zodiacSign is < 0 or > 11) throw new ArgumentOutOfRangeException(nameof(zodiacSign));
        if (!Enum.IsDefined(facing)) throw new ArgumentOutOfRangeException(nameof(facing));
        AttackUnitStatus expected = hp == 0 ? AttackUnitStatus.Dead : hp <= maxHp / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None;
        if (status != expected) throw new ArgumentException("HP and the supported Dead/Critical projection must agree.", nameof(status));
        if (koCount is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(koCount));
        if (rightHandWeaponId is { } weapon && string.IsNullOrWhiteSpace(weapon.Value))
            throw new ArgumentException("An equipped weapon must have a valid ID.", nameof(rightHandWeaponId));
        UnitId = unitId; Hp = hp; MaxHp = maxHp; Mp = mp; MaxMp = maxMp;
        PhysicalAttack = physicalAttack; ClassPhysicalEvade = classPhysicalEvade;
        RightHandEvade = rightHandEvade; LeftHandEvade = leftHandEvade; AccessoryEvade = accessoryEvade;
        Brave = brave; ZodiacSign = zodiacSign; Facing = facing; Status = status; KoCount = koCount;
        RightHandWeaponId = rightHandWeaponId;
    }

    internal AttackUnitState WithResult(int hp, BattleFacing facing, int koCount) => new(UnitId,
        hp, MaxHp, Mp, MaxMp, PhysicalAttack, ClassPhysicalEvade, RightHandEvade, LeftHandEvade,
        AccessoryEvade, Brave, ZodiacSign, facing,
        hp == 0 ? AttackUnitStatus.Dead : hp <= MaxHp / 5 ? AttackUnitStatus.Critical : AttackUnitStatus.None,
        koCount, RightHandWeaponId);

    internal AttackUnitState WithPhysicalAttack(int physicalAttack) => new(UnitId,
        Hp, MaxHp, Mp, MaxMp, physicalAttack, ClassPhysicalEvade, RightHandEvade, LeftHandEvade,
        AccessoryEvade, Brave, ZodiacSign, Facing, Status, KoCount, RightHandWeaponId);
}
