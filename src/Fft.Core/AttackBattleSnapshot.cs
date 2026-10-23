using System.Collections.Immutable;

namespace Fft.Core;

// Managed projections, not original packed facing or status encodings.
public enum BattleFacing { East, North, West, South }
[Flags]
public enum AttackUnitStatus { None = 0, Dead = 1, Critical = 2 }

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
}

/// <summary>Immutable attack state; death scheduling remains outside the bounded simulator.</summary>
public sealed class AttackBattleSnapshot
{
    public TurnBattleState TurnBattle { get; }
    public ImmutableArray<AttackUnitState> Combatants { get; }

    public AttackBattleSnapshot(TurnBattleState turnBattle, IEnumerable<AttackUnitState> combatants)
    {
        ArgumentNullException.ThrowIfNull(turnBattle);
        ArgumentNullException.ThrowIfNull(combatants);
        Combatants = combatants.ToImmutableArray();
        if (Combatants.Length != turnBattle.Battle.Units.Count) throw new ArgumentException("Every battle unit requires exactly one combat projection.");
        var seen = new HashSet<BattleUnitId>();
        foreach (AttackUnitState unit in Combatants)
        {
            ArgumentNullException.ThrowIfNull(unit);
            if (!seen.Add(unit.UnitId) || !turnBattle.Battle.TryGetUnit(unit.UnitId, out _))
                throw new ArgumentException($"Duplicate or unknown combat unit '{unit.UnitId}'.");
            if (unit.Status == AttackUnitStatus.Dead && turnBattle.Turns.ActiveUnitId == unit.UnitId)
                throw new ArgumentException("A dead unit cannot own the active turn.");
        }
        if (Combatants.Select(unit => unit.ZodiacSign).Distinct().Count() != 1)
            throw new NotSupportedException("The bounded snapshot requires the same zodiac sign for neutral compatibility.");
        TurnBattle = turnBattle;
    }

    public AttackBattleSnapshot WithTurnBattle(TurnBattleState state) => new(state, Combatants);

    /// <summary>Startup domain checks, distinct from representable Dead/Critical result snapshots.</summary>
    public void ValidateInitialSetup()
    {
        if (Combatants.Any(unit => unit.Status != AttackUnitStatus.None))
            throw new NotSupportedException("Initial attack setup requires healthy units; result snapshots may represent Dead/Critical.");
        ValidateSupportedGeometry();
    }

    internal void ValidateSupportedGeometry()
    {
        var positions = new HashSet<TilePosition>();
        foreach (BattleUnitState unit in TurnBattle.Battle.Units)
        {
            if (!positions.Add(unit.Position)) throw new ArgumentException("Attack setup cannot overlap units.");
            if (unit.MovementProfile.BodyHeight != 6 || unit.MovementProfile.Mode != UnitMovementMode.Normal || unit.MovementProfile.Traits != UnitMovementTraits.None)
                throw new NotSupportedException("Attack setup requires ordinary body height 6 and no movement modifiers.");
            TurnBattle.Battle.Map.TryGetTile(unit.Position, out BattleTile origin);
            if (origin.IsBlocked || origin.IsUntargetable || origin.CannotStop || unit.Position.Layer != 0)
                throw new NotSupportedException("Attack units require targetable, stoppable lower-layer starting tiles.");
        }
        var map = TurnBattle.Battle.Map;
        for (int layer = 0; layer < map.LayerCount; layer++)
        for (int y = 0; y < map.Depth; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.TryGetTile(new(x, y, layer), out BattleTile tile);
            if (!tile.IsBlocked && (layer != 0 || tile.Height != 10 || tile.HalfHeight != 0 || tile.WaterDepth != 0 || tile.CeilingDepth != 0 || tile.Slope.ToPackedByte() != 0))
                throw new NotSupportedException("Attack setup currently requires the traced dry flat lower-layer height-10 map.");
        }
    }
}

/// <summary>A bounded unit-target command, validated atomically before publishing effects.</summary>
public sealed record AttackUnitCommand(BattleUnitId ActorId, BattleUnitId TargetId);
public enum AttackCommandRejection { None, UnknownActor, UnknownTarget, InactiveActor, ActionAlreadyTaken, SelfTarget, DeadTarget, OutOfRange, UnsupportedSetup, RandomDrawCountExhausted }
public abstract record AttackResultEvent;
public sealed record AttackTurnBattleEvent(TurnBattleEvent Event) : AttackResultEvent;
public sealed record AttackResolvedEvent(BattleUnitId ActorId, BattleUnitId TargetId, bool Hit, bool Critical, int Damage) : AttackResultEvent;
public sealed record AttackHpChangedEvent(BattleUnitId UnitId, int Before, int After) : AttackResultEvent;
public sealed record AttackStatusChangedEvent(BattleUnitId UnitId, AttackUnitStatus Before, AttackUnitStatus After) : AttackResultEvent;
public sealed record AttackKnockedOutEvent(BattleUnitId UnitId, int KoCount) : AttackResultEvent;
