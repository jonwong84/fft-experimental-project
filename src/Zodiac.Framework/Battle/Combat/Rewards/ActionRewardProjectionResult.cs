namespace Zodiac.Framework;

/// <summary>Skipped/rejected have no reward data, equivalent to the source's cleared display.</summary>
public sealed record ActionRewardProjectionResult(ActionRewardDisposition Disposition, ActionRewardProjection? Projection);
