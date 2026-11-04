namespace Zodiac.Content.Definitions.Job
{
    using Zodiac.Content.Configuration;

    /// <summary>Immutable job content values, initialized when the definition is created.</summary>
    public sealed class JobStats
    {
        public int HpGrowth { get; init; }
        public int HpMultiplier { get; init; }
        public int MpGrowth { get; init; }
        public int MpMultiplier { get; init; }
        public int SpeedGrowth { get; init; }
        public int SpeedMultiplier { get; init; }
        public int PaGrowth { get; init; }
        public int PaMultiplier { get; init; }
        public int MaGrowth { get; init; }
        public int MaMultiplier { get; init; }
        public int BaseMove { get; }
        public int BaseJump { get; }
        public int PhysicalEvade { get; init; }
        // New in Zodiac: Class-based M-Ev% was never used in the original game.
        public int MagicalEvade { get; init; }

        public JobStats(int baseMove, int baseJump, JobStatLimits? limits = null)
        {
            limits ??= JobStatLimits.Default;
            DefinitionValidation.Range(baseMove, limits.MaxMove, nameof(baseMove));
            DefinitionValidation.Range(baseJump, limits.MaxJump, nameof(baseJump));
            BaseMove = baseMove;
            BaseJump = baseJump;
        }
    }
}
