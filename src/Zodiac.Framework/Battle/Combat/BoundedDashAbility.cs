using System.Collections.Immutable;

namespace Zodiac.Framework;

/// <summary>Retail Dash: immediate range one, vertical tolerance one, formula 0x37/Y4, no evasion or trajectory.</summary>
public sealed record BoundedDashAbility
{
    public AssetId Id { get; }
    public BoundedDashAbility(AssetId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value)) throw new ArgumentException("An explicit ability ID is required.", nameof(id));
        Id = id;
    }
}
