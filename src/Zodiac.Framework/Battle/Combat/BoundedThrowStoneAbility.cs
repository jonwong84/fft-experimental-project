using Zodiac.Framework.Random;
using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Explicit projection of Throw Stone's closed range-4, Y=2 immediate damage/knockback rule.</summary>
public sealed record BoundedThrowStoneAbility
{
    public AssetId Id { get; }
    public int Range => 4;
    public BoundedThrowStoneAbility(AssetId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("An ability ID is required.", nameof(id));
        Id = id;
    }
}
