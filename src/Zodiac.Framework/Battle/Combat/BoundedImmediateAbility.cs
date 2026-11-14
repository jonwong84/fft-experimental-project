using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Validated projection of the closed immediate self PA+1 effect; not a raw formula record.</summary>
public sealed record BoundedImmediateAbility
{
    public AssetId Id { get; }
    public BoundedImmediateAbility(AssetId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("An ability ID is required.", nameof(id));
        Id = id;
    }
}
