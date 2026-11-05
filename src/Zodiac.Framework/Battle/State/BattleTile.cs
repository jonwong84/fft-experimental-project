namespace Zodiac.Framework;

/// <summary>Immutable map-tile fields relevant to movement and terrain geometry.</summary>
/// <param name="Height">Base terrain height in whole units.</param>
/// <remarks>
/// <see cref="HalfHeight"/>, <see cref="WaterDepth"/>, and <see cref="CeilingDepth"/> are
/// managed values. The original importer must validate its packed source ranges at the file boundary.
/// Visual-only tile properties and transient map marks are not part of this movement model.
/// </remarks>
public readonly record struct BattleTile
{
    public BattleTile(int height)
        : this(TerrainSurfaceId.Unclassified, height)
    {
    }

    public BattleTile(
        TerrainSurfaceId surface,
        int height,
        int halfHeight = 0,
        int waterDepth = 0,
        TileSlope slope = default,
        int ceilingDepth = 0,
        BattleTileMovementFlags movementFlags = BattleTileMovementFlags.None)
    {
        if (string.IsNullOrWhiteSpace(surface.Value))
        {
            throw new ArgumentException("A tile must have a valid terrain surface ID.", nameof(surface));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(halfHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(waterDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(ceilingDepth);
        if ((movementFlags & ~(BattleTileMovementFlags.Blocked | BattleTileMovementFlags.Untargetable | BattleTileMovementFlags.CannotStop)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(movementFlags), movementFlags, "Unknown movement flag.");
        }

        Surface = surface;
        Height = height;
        HalfHeight = halfHeight;
        WaterDepth = waterDepth;
        Slope = slope;
        CeilingDepth = ceilingDepth;
        MovementFlags = movementFlags;
    }

    public TerrainSurfaceId Surface { get; }

    public int Height { get; }

    public int HalfHeight { get; }

    public int WaterDepth { get; }

    public TileSlope Slope { get; }

    public int CeilingDepth { get; }

    public BattleTileMovementFlags MovementFlags { get; }

    public bool IsBlocked => (MovementFlags & BattleTileMovementFlags.Blocked) != 0;

    public bool IsUntargetable => (MovementFlags & BattleTileMovementFlags.Untargetable) != 0;

    public bool CannotStop => (MovementFlags & BattleTileMovementFlags.CannotStop) != 0;
}
