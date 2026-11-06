using System.Collections.Immutable;

namespace Zodiac.Framework.Events;

/// <summary>Caller-owned proof that the immutable logical definition is ready; no artwork/file readiness claim.</summary>
public sealed record ReadyDarkScreenBinding(AssetId DefinitionId, AssetId OwnerId);
