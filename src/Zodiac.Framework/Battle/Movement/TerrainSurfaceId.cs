namespace Zodiac.Framework;

/// <summary>A stable identifier for a terrain surface, independent of its original numeric encoding.</summary>
public readonly record struct TerrainSurfaceId
{
    public TerrainSurfaceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _ = new AssetId(value);
        Value = value;
    }

    public string Value { get; }

    public static TerrainSurfaceId Unclassified { get; } = new("base:terrain/unclassified");

    public override string ToString() => Value ?? string.Empty;
}
