using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>
/// Executable projection of one range-1 striking, neutral, empty-proc sword with no active modifiers.
/// Content adapters validate their full definition before creating this projection; it is not a raw item record.
/// </summary>
public sealed record BoundedSwordWeapon
{
    public AssetId Id { get; }
    public int Power { get; }

    public BoundedSwordWeapon(AssetId id, int power)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("An explicit weapon ID is required.", nameof(id));
        if (power is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(power));
        Id = id;
        Power = power;
    }
}
