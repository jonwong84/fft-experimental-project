namespace Zodiac.Framework;

/// <summary>Execution copy of the immutable content metadata's learning fields.</summary>
public sealed record AbilityLearningRule(AssetId AbilityId, bool LearnOnHit, byte Rate);
