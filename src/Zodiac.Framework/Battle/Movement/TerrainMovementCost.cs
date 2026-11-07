namespace Zodiac.Framework;

/// <summary>A resolved movement cost for one terrain surface.</summary>
public readonly record struct TerrainMovementCost
{
    public TerrainMovementCost(int movePoints, bool isBlocked = false)
    {
        if (movePoints < 0 || (!isBlocked && movePoints == 0))
        {
            throw new ArgumentOutOfRangeException(nameof(movePoints), movePoints,
                "A traversable surface needs a positive cost; blocked surfaces may use zero.");
        }

        MovePoints = movePoints;
        IsBlocked = isBlocked;
    }

    public int MovePoints { get; }

    public bool IsBlocked { get; }

    public static TerrainMovementCost Blocked => new(0, isBlocked: true);
}
