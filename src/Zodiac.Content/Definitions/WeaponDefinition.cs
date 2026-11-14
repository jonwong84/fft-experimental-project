using Zodiac.Framework;
using System.Collections.Immutable;
using Zodiac.Content.Definitions.Common;
using Zodiac.Content.Definitions.Item;

namespace Zodiac.Content.Definitions;

public readonly record struct WeaponId
{
    public string Value { get; }
    public WeaponId(string value) => Value = DefinitionValidation.Id(value, "Weapon ID");
    public override string ToString() => Value ?? string.Empty;
}

public enum SwordDamageFormula : byte { WeaponDamage = 1 }
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
public sealed class WeaponDefinition
{
    public WeaponId Id { get; }
    public WeaponType Type { get; }
    public int Range { get; }
    public int Power { get; }
    public SwordDamageFormula Formula { get; }
    public WeaponBehaviorToggles Behavior { get; }
    public int Evade { get; }
    public ElementToggles WeaponElement { get; }
    public EmptySwordAddedEffect AddedEffect { get; }

    public WeaponDefinition(WeaponId id, WeaponType type, int range, int power,
        SwordDamageFormula formula, WeaponBehaviorToggles behavior, int evade,
        ElementToggles weaponElement, EmptySwordAddedEffect addedEffect)
    {
        DefinitionValidation.RequiredId(id.Value, nameof(id));
        if (type != WeaponType.Sword) throw new ArgumentOutOfRangeException(nameof(type), "Only swords are currently supported.");
        if (range != 1) throw new ArgumentOutOfRangeException(nameof(range), "Only range 1 is supported.");
        DefinitionValidation.Range(power, byte.MaxValue, nameof(power));
        if (formula != SwordDamageFormula.WeaponDamage)
            throw new ArgumentOutOfRangeException(nameof(formula), "Only weapon damage formula 1 is supported.");
        ArgumentNullException.ThrowIfNull(behavior);
        if (!behavior.Striking || behavior.Lunging || behavior.Direct || behavior.Arc || behavior.ForcedDoublehand)
            throw new ArgumentOutOfRangeException(nameof(behavior), "The current sword profile requires Striking without Lunging, Direct, Arc or ForcedDoublehand.");
        DefinitionValidation.Range(evade, byte.MaxValue, nameof(evade));
        ArgumentNullException.ThrowIfNull(weaponElement);
        if (!weaponElement.Neutral)
            throw new ArgumentOutOfRangeException(nameof(weaponElement), "The current sword profile supports only neutral elements.");
        ArgumentNullException.ThrowIfNull(addedEffect);
        Id = id;
        Type = type;
        Range = range;
        Power = power;
        Formula = formula;
        Behavior = behavior;
        Evade = evade;
        WeaponElement = weaponElement;
        AddedEffect = addedEffect;
    }
}
