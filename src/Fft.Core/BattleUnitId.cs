namespace Fft.Core;

/// <summary>Identifies one unit instance in a battle.</summary>
public readonly record struct BattleUnitId
{
    public BattleUnitId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
