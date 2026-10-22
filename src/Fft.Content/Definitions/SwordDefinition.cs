using System.Collections.Immutable;

namespace Fft.Content.Definitions;

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
public enum SwordElement : byte { Neutral = 0 }

/// <summary>The supported empty all-or-nothing added effect, separate from ability learning metadata.</summary>
public sealed class EmptySwordAddedEffect
{
    public int TypeRaw { get; }
    public ImmutableArray<byte> StatusesRaw { get; }

    public EmptySwordAddedEffect(int typeRaw, IEnumerable<byte> statusesRaw)
    {
        if (typeRaw != 0x80)
            throw new ArgumentOutOfRangeException(nameof(typeRaw), "Only all-or-nothing (128) is supported.");
        ArgumentNullException.ThrowIfNull(statusesRaw);
        var values = statusesRaw.Take(6).ToImmutableArray();
        if (values.Length != 5)
            throw new ArgumentException("Exactly five status bytes are required.", nameof(statusesRaw));
        if (values.Any(value => value != 0))
            throw new ArgumentException("Only an explicitly empty status set is supported.", nameof(statusesRaw));
        TypeRaw = typeRaw;
        StatusesRaw = values;
    }
}

/// <summary>Immutable supported synthetic sword content; no retail row or combat state is implied.</summary>
public sealed class SwordDefinition
{
    public WeaponId Id { get; }
    public SwordWeaponType Type { get; }
    public int Range { get; }
    public int Power { get; }
    public SwordDamageFormula Formula { get; }
    public SwordTargetingFlags TargetingFlags { get; }
    public int Evade { get; }
    public SwordElement Element { get; }
    public EmptySwordAddedEffect AddedEffect { get; }

    public SwordDefinition(WeaponId id, SwordWeaponType type, int range, int power,
        SwordDamageFormula formula, SwordTargetingFlags targetingFlags, int evade,
        SwordElement element, EmptySwordAddedEffect addedEffect)
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
        Id = id;
        Type = type;
        Range = range;
        Power = power;
        Formula = formula;
        TargetingFlags = targetingFlags;
        Evade = evade;
        Element = element;
        AddedEffect = addedEffect;
    }
}
