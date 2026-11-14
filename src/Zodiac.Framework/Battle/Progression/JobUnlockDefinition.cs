using System.Collections.Immutable;

namespace Zodiac.Framework;

public sealed record JobUnlockDefinition(AssetId JobId, ImmutableArray<JobLevelRequirement> Requirements);
