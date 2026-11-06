namespace Zodiac.Framework;

/// <summary>Slot identity is separate from ability identity: duplicate abilities have distinct learned bits.</summary>
public sealed record AbilityLearningSlot(int Index, AssetId AbilityId);
