using System.Collections.Immutable;

namespace Zodiac.Content.Definitions;

public readonly record struct WeaponId
{
    public string Value { get; }
    public WeaponId(string value) => Value = DefinitionValidation.Id(value, "Weapon ID");
    public override string ToString() => Value ?? string.Empty;
}

public enum SwordWeaponType : byte { Sword = 3 }
public enum SwordDamageFormula : byte { WeaponDamage = 1 }
[Flags]
public enum SwordTargetingFlags : byte { Striking = 0x80 }
[Flags]
public enum SwordCapabilityFlags : byte
{
    None = 0,
    Throwable = 2,
    TwoHandsCompatible = 4,
    TwoSwordsCompatible = 8,
}
public enum SwordElement : byte { Neutral = 0 }
public enum SwordEmptyProcMode : byte { None = 0, AllOrNothing = 128 }

/// <summary>An explicitly empty status set retaining its source mode.</summary>
public sealed class EmptySwordAddedEffect
{
    public int TypeRaw { get; }
    public ImmutableArray<byte> StatusesRaw { get; }

    public EmptySwordAddedEffect(int typeRaw, IEnumerable<byte> statusesRaw)
        : this(LegacyMode(typeRaw), statusesRaw) { }

    public EmptySwordAddedEffect(SwordEmptyProcMode mode, IEnumerable<byte> statusesRaw)
    {
        if (mode is not (SwordEmptyProcMode.None or SwordEmptyProcMode.AllOrNothing))
            throw new ArgumentOutOfRangeException(nameof(mode), "Only empty modes 0 and 128 are supported.");
        ArgumentNullException.ThrowIfNull(statusesRaw);
        var values = statusesRaw.Take(6).ToImmutableArray();
        if (values.Length != 5)
            throw new ArgumentException("Exactly five status bytes are required.", nameof(statusesRaw));
        if (values.Any(value => value != 0))
            throw new ArgumentException("Only an explicitly empty status set is supported.", nameof(statusesRaw));
        TypeRaw = (byte)mode;
        StatusesRaw = values;
    }

    private static SwordEmptyProcMode LegacyMode(int typeRaw) => typeRaw == 128
        ? SwordEmptyProcMode.AllOrNothing
        : throw new ArgumentOutOfRangeException(nameof(typeRaw), "The legacy constructor requires type 128.");
}

/// <summary>Immutable supported sword data; compatibility flags do not activate unit abilities.</summary>
public sealed class SwordDefinition
{
    public WeaponId Id { get; }
    public SwordWeaponType Type { get; }
    public int Range { get; }
    public int Power { get; }
    public SwordDamageFormula Formula { get; }
    public SwordTargetingFlags TargetingFlags { get; }
    public SwordCapabilityFlags CapabilityFlags { get; }
    public byte RawWeaponFlags => (byte)((byte)TargetingFlags | (byte)CapabilityFlags);
    public int Evade { get; }
    public SwordElement Element { get; }
    public EmptySwordAddedEffect AddedEffect { get; }

    public SwordDefinition(WeaponId id, SwordWeaponType type, int range, int power,
        SwordDamageFormula formula, SwordTargetingFlags targetingFlags, int evade,
        SwordElement element, EmptySwordAddedEffect addedEffect)
        : this(id, type, range, power, formula, targetingFlags, evade, element, addedEffect,
            SwordCapabilityFlags.None) { }

    public SwordDefinition(WeaponId id, SwordWeaponType type, int range, int power,
        SwordDamageFormula formula, SwordTargetingFlags targetingFlags, int evade,
        SwordElement element, EmptySwordAddedEffect addedEffect, SwordCapabilityFlags capabilityFlags)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        if (type != SwordWeaponType.Sword) throw new ArgumentOutOfRangeException(nameof(type), "Only Sword (3) is supported.");
        if (range != 1) throw new ArgumentOutOfRangeException(nameof(range), "Only range 1 is supported.");
        DefinitionValidation.Range(power, byte.MaxValue, nameof(power));
        if (formula != SwordDamageFormula.WeaponDamage)
            throw new ArgumentOutOfRangeException(nameof(formula), "Only weapon damage formula 1 is supported.");
        if (targetingFlags != SwordTargetingFlags.Striking)
            throw new ArgumentOutOfRangeException(nameof(targetingFlags), "Only striking flag 128 without additional flags is supported.");
        DefinitionValidation.Range(evade, byte.MaxValue, nameof(evade));
        if (element != SwordElement.Neutral) throw new ArgumentOutOfRangeException(nameof(element), "Only neutral element 0 is supported.");
        ArgumentNullException.ThrowIfNull(addedEffect);
        if (((byte)capabilityFlags & ~0x0e) != 0)
            throw new ArgumentOutOfRangeException(nameof(capabilityFlags), "Unsupported sword capability bits.");
        Id = id;
        Type = type;
        Range = range;
        Power = power;
        Formula = formula;
        TargetingFlags = targetingFlags;
        CapabilityFlags = capabilityFlags;
        Evade = evade;
        Element = element;
        AddedEffect = addedEffect;
    }
}
