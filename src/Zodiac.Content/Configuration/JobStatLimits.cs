namespace Zodiac.Content.Configuration;

/// <summary>Immutable per-game content settings, supplied by the application rather than read from global state.</summary>
/// <remarks>Defaults allow all nonnegative Int32 values. Original packed encodings belong to import/export adapters.</remarks>
public sealed record JobStatLimits
{
    public static JobStatLimits Default { get; } = new();
    public int MaxMove { get; }
    public int MaxJump { get; }

    public JobStatLimits(int maxMove = int.MaxValue, int maxJump = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxMove);
        ArgumentOutOfRangeException.ThrowIfNegative(maxJump);
        MaxMove = maxMove;
        MaxJump = maxJump;
    }
}
